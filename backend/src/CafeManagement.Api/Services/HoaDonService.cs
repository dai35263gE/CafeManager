using System.Data;
using System.Globalization;
using System.Text;
using CafeManagement.Api.Contracts;
using CafeManagement.Domain.Common;
using CafeManagement.Domain.Entities;
using CafeManagement.Domain.Enums;
using CafeManagement.Domain.Rules;
using CafeManagement.Infrastructure;
using CafeManagement.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CafeManagement.Api.Services;

public sealed class HoaDonService(
    CafeDbContext db,
    IdGenerator ids,
    IHttpContextAccessor accessor,
    IConfiguration configuration) : IHoaDonService
{
    public async Task<PageResponse<InvoiceResponse>> TimKiemHoaDonAsync(
        ListRequest request, InvoiceStatus? status, DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct)
    {
        if (from.HasValue && to.HasValue && from >= to)
            throw new ApiException(400, "Invoice date range must have from < to.");
        var query = BillingQueries.Invoices(db).AsNoTracking();
        if (status.HasValue)
            query = query.Where(x => x.TrangThai == status);
        if (from.HasValue)
            query = query.Where(x => x.ThoiGianLap >= from.Value.UtcDateTime);
        if (to.HasValue)
            query = query.Where(x => x.ThoiGianLap < to.Value.UtcDateTime);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(x => x.MaHoaDon.Contains(search) || x.MaDon.Contains(search));
        }

        var total = await query.CountAsync(ct);
        var offset = ((long)request.Page - 1) * request.PageSize;
        var rows = offset > int.MaxValue ? [] : await query.OrderByDescending(x => x.ThoiGianLap)
            .ThenBy(x => x.MaHoaDon).Skip((int)offset).Take(request.PageSize).ToListAsync(ct);
        var audits = await BillingQueries.CancellationAuditsAsync(db, rows, ct);
        return new(rows.Select(invoice => BillingQueries.Map(invoice,
            audits.GetValueOrDefault($"HoaDon:{invoice.MaHoaDon}"))).ToList(),
            total, request.Page, request.PageSize);
    }

    public async Task<InvoiceResponse> LayHoaDonAsync(string id, CancellationToken ct)
    {
        var invoice = await BillingQueries.Invoices(db).AsNoTracking()
            .SingleOrDefaultAsync(x => x.MaHoaDon == id, ct) ?? throw ServiceHelpers.NotFound("Invoice");
        var audits = await BillingQueries.CancellationAuditsAsync(db, [invoice], ct);
        return BillingQueries.Map(invoice, audits.GetValueOrDefault($"HoaDon:{invoice.MaHoaDon}"));
    }

    public async Task<InvoiceQuoteResponse> TaoBaoGiaAsync(InvoiceQuoteRequest request, CancellationToken ct)
    {
        var order = await db.DonHangs.Include(x => x.ChiTiets)
            .SingleOrDefaultAsync(x => x.MaDon == request.OrderId, ct) ?? throw ServiceHelpers.NotFound("Order");
        ServiceHelpers.Version(db.Entry(order), order.RowVersion, request.RowVersion);
        RequirePayableOrder(order);
        if (await db.HoaDons.AnyAsync(x => x.MaDon == order.MaDon, ct))
            throw ServiceHelpers.Conflict("This order already has an invoice.");

        var subtotal = order.ChiTiets.Where(x => x.TrangThaiMon != OrderItemStatus.Cancelled)
            .Sum(x => x.DonGia * x.SoLuong);
        var promotion = string.IsNullOrWhiteSpace(request.PromotionCode)
            ? null
            : await BillingQueries.PromotionAsync(db, request.PromotionCode, ct);
        var discount = promotion is null
            ? 0m
            : KhuyenMaiService.CalculateDiscount(promotion, subtotal, DateOnly.FromDateTime(DateTime.UtcNow));
        var calculation = BillingCalculator.Calculate(configuration, subtotal, discount);
        return new(order.MaDon, Convert.ToBase64String(order.RowVersion), promotion?.MaKhuyenMai,
            promotion?.MaKhuyenMai, subtotal, calculation.VatRatePercent, calculation.VatAmount,
            calculation.ServiceFeeRatePercent, calculation.ServiceFeeAmount, discount,
            calculation.TotalAmount, calculation.Warnings);
    }

    public async Task<string> InHoaDonAsync(string id, CancellationToken ct)
    {
        var invoice = await BillingQueries.Invoices(db).AsNoTracking()
            .SingleOrDefaultAsync(x => x.MaHoaDon == id, ct) ?? throw ServiceHelpers.NotFound("Invoice");
        var culture = CultureInfo.GetCultureInfo("vi-VN");
        var receipt = new StringBuilder()
            .AppendLine("HOA DON QUAN CA PHE")
            .AppendLine($"Ma hoa don: {invoice.MaHoaDon}")
            .AppendLine($"Ma don: {invoice.MaDon}")
            .AppendLine($"Thoi gian: {invoice.ThoiGianLap:yyyy-MM-dd HH:mm:ss} UTC")
            .AppendLine($"Thu ngan: {invoice.NhanVien.HoTen}")
            .AppendLine("----------------------------------------");
        foreach (var line in invoice.DonHang.ChiTiets.Where(x => x.TrangThaiMon != OrderItemStatus.Cancelled))
        {
            receipt.AppendLine($"{line.Mon.TenMon} x {line.SoLuong}  {(line.DonGia * line.SoLuong).ToString("N0", culture)}");
            if (line.KichCo is not null)
                receipt.AppendLine($"  Co: {line.KichCo.TenKichCo}");
            foreach (var topping in line.ChiTietToppings)
                receipt.AppendLine($"  + {topping.Topping.TenTopping}");
        }
        receipt.AppendLine("----------------------------------------")
            .AppendLine($"Tien hang: {invoice.TongTienHang.ToString("N0", culture)}")
            .AppendLine($"Thue VAT: {invoice.ThueVAT.ToString("N0", culture)}")
            .AppendLine($"Phi dich vu: {invoice.PhiDichVu.ToString("N0", culture)}")
            .AppendLine($"Giam gia: {invoice.TienGiamGia.ToString("N0", culture)}")
            .AppendLine($"Tong thanh toan: {invoice.TongThanhToan.ToString("N0", culture)}")
            .AppendLine($"Phuong thuc: {invoice.PhuongThucThanhToan}")
            .AppendLine($"Khach dua: {invoice.TienKhachDua.ToString("N0", culture)}")
            .AppendLine($"Tien thua: {invoice.TienThua.ToString("N0", culture)}");
        return receipt.ToString();
    }

    public async Task<InvoiceResponse> LapHoaDonAsync(CreateInvoiceRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var order = await db.DonHangs.Include(x => x.Ban)
            .Include(x => x.DonHangBans).ThenInclude(x => x.Ban)
            .Include(x => x.ChiTiets).ThenInclude(x => x.ChiTietToppings)
            .SingleOrDefaultAsync(x => x.MaDon == request.OrderId, ct) ?? throw ServiceHelpers.NotFound("Order");
        ServiceHelpers.Version(db.Entry(order), order.RowVersion, request.RowVersion);
        RequirePayableOrder(order);
        if (await db.HoaDons.AnyAsync(x => x.MaDon == order.MaDon, ct))
            throw ServiceHelpers.Conflict("This order already has an invoice.");

        var subtotal = order.ChiTiets.Where(x => x.TrangThaiMon != OrderItemStatus.Cancelled)
            .Sum(x => x.DonGia * x.SoLuong);
        var promotion = string.IsNullOrWhiteSpace(request.PromotionCode)
            ? null
            : await BillingQueries.PromotionAsync(db, request.PromotionCode, ct);
        var discount = promotion is null
            ? 0m
            : KhuyenMaiService.CalculateDiscount(promotion, subtotal, DateOnly.FromDateTime(DateTime.UtcNow));
        var calculation = BillingCalculator.Calculate(configuration, subtotal, discount);
        PaymentAmounts amounts;
        try
        {
            amounts = PaymentRules.Calculate(subtotal, discount, request.PaymentMethod,
                request.ReceivedAmount, calculation.VatAmount, calculation.ServiceFeeAmount);
        }
        catch (DomainException exception)
        {
            throw new ApiException(400, exception.Message);
        }

        var cashierId = ServiceHelpers.Actor(accessor);
        var now = DateTime.UtcNow;
        var invoice = new HoaDon
        {
            MaHoaDon = await ids.HoaDonAsync(ct),
            MaDon = order.MaDon,
            DonHang = order,
            MaNhanVien = cashierId,
            MaKhuyenMai = promotion?.MaKhuyenMai,
            KhuyenMai = promotion,
            TongTienHang = subtotal,
            ThueVAT = calculation.VatAmount,
            PhiDichVu = calculation.ServiceFeeAmount,
            TienGiamGia = discount,
            TongThanhToan = amounts.TotalAmount,
            PhuongThucThanhToan = request.PaymentMethod,
            TienKhachDua = amounts.ReceivedAmount,
            TienThua = amounts.ChangeAmount,
            ThoiGianLap = now,
            TrangThai = InvoiceStatus.Paid
        };
        await ConsumeRecipeIngredientsAsync(order, cashierId, invoice.MaHoaDon, ct);
        order.TamTinh = subtotal;
        order.TrangThai = OrderStatus.Paid;
        ServiceHelpers.CloseTableLinks(order, TableStatus.NeedsCleaning);
        db.HoaDons.Add(invoice);
        ServiceHelpers.Audit(db, cashierId, "InvoicePaid", "HoaDon", invoice.MaHoaDon,
            $"Order {order.MaDon}; amount {invoice.TongThanhToan}; method {invoice.PhuongThucThanhToan}.");
        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw ServiceHelpers.Conflict("The order or inventory changed during payment. Reload and retry.");
        }
        catch (DbUpdateException exception) when (exception.GetBaseException() is SqlException { Number: 1205 })
        {
            throw ServiceHelpers.Conflict("Inventory changed during payment. Reload and retry.");
        }
        catch (SqlException exception) when (exception.Number == 1205)
        {
            throw ServiceHelpers.Conflict("Inventory changed during payment. Reload and retry.");
        }
        return BillingQueries.Map(invoice);
    }

    public async Task<InvoiceResponse> HuyHoaDonAsync(string id, CancelInvoiceRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var invoice = await BillingQueries.Invoices(db)
            .SingleOrDefaultAsync(x => x.MaHoaDon == id, ct) ?? throw ServiceHelpers.NotFound("Invoice");
        if (invoice.TrangThai == InvoiceStatus.Cancelled)
            throw ServiceHelpers.Conflict("The invoice is already cancelled.");
        var rowVersion = invoice.RowVersion;
        if (rowVersion.Length == 0)
            throw new InvalidOperationException("The invoice row version was not loaded.");
        ServiceHelpers.Version(db.Entry(invoice), rowVersion, request.RowVersion);
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new ApiException(400, "A cancellation reason is required.");
        invoice.TrangThai = InvoiceStatus.Cancelled;
        ServiceHelpers.Audit(db, ServiceHelpers.Actor(accessor), "InvoiceCancelled", $"HoaDon:{id}", id,
            request.Reason.Trim());
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        var audits = await BillingQueries.CancellationAuditsAsync(db, [invoice], ct);
        return BillingQueries.Map(invoice, audits.GetValueOrDefault($"HoaDon:{invoice.MaHoaDon}"));
    }

    private static void RequirePayableOrder(DonHang order)
    {
        if (order.TrangThai != OrderStatus.Served ||
            !order.ChiTiets.Any(x => x.TrangThaiMon != OrderItemStatus.Cancelled) ||
            order.ChiTiets.Any(x => x.TrangThaiMon is not (OrderItemStatus.Served or OrderItemStatus.Cancelled)))
            throw ServiceHelpers.Conflict("All active items must be served before payment.");
    }

    private async Task ConsumeRecipeIngredientsAsync(
        DonHang order, string cashierId, string invoiceId, CancellationToken ct)
    {
        var lines = order.ChiTiets.Where(x => x.TrangThaiMon != OrderItemStatus.Cancelled).ToList();
        var menuItemIds = lines.Select(x => x.MaMon).Distinct().ToArray();
        var recipes = await db.DinhLuongs.Where(x => menuItemIds.Contains(x.MaMon)).ToListAsync(ct);
        var recipeMenuIds = recipes.Select(x => x.MaMon).ToHashSet(StringComparer.Ordinal);
        var missingRecipes = menuItemIds.Where(id => !recipeMenuIds.Contains(id)).ToArray();
        if (missingRecipes.Length > 0)
            throw ServiceHelpers.Conflict(
                $"Payment requires ingredient recipes for every sold menu item. Missing: {string.Join(", ", missingRecipes)}.");

        var requiredByIngredient = lines
            .SelectMany(line => recipes.Where(recipe => recipe.MaMon == line.MaMon)
                .Select(recipe => new { recipe.MaNguyenLieu, Quantity = recipe.SoLuongSuDung * line.SoLuong }))
            .GroupBy(item => item.MaNguyenLieu, StringComparer.Ordinal)
            .Select(group => new { IngredientId = group.Key, Quantity = group.Sum(item => item.Quantity) })
            .OrderBy(item => item.IngredientId, StringComparer.Ordinal)
            .ToArray();
        var ingredientIds = requiredByIngredient.Select(item => item.IngredientId).ToArray();
        var ingredients = await db.NguyenLieus
            .Where(x => ingredientIds.Contains(x.MaNguyenLieu))
            .OrderBy(x => x.MaNguyenLieu)
            .ToDictionaryAsync(x => x.MaNguyenLieu, StringComparer.Ordinal, ct);

        foreach (var requirement in requiredByIngredient)
        {
            if (!ingredients.TryGetValue(requirement.IngredientId, out var ingredient))
                throw ServiceHelpers.Conflict("A recipe refers to an ingredient that no longer exists.");
            if (ingredient.SoLuongTon < requirement.Quantity)
                throw ServiceHelpers.Conflict(
                    $"Insufficient stock for {ingredient.TenNguyenLieu}; required {requirement.Quantity} {ingredient.DonViTinh}.");

            ingredient.SoLuongTon -= requirement.Quantity;
            var movement = new BienDongKho
            {
                MaBienDong = await ids.BienDongAsync(ct),
                MaNguyenLieu = ingredient.MaNguyenLieu,
                NguyenLieu = ingredient,
                MaNhanVien = cashierId,
                LoaiBienDong = InventoryTransactionType.Export,
                SoLuong = -requirement.Quantity,
                ThoiGian = DateTime.UtcNow,
                LyDo = $"Invoice {invoiceId}; automatic recipe consumption for order {order.MaDon}."
            };
            db.BienDongKhos.Add(movement);
            ServiceHelpers.Audit(db, cashierId, "InventoryConsumed", "NguyenLieu", ingredient.MaNguyenLieu,
                $"Invoice {invoiceId}; {requirement.Quantity} {ingredient.DonViTinh} consumed.");
        }
    }
}
