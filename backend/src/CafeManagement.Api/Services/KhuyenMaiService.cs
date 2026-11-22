using System.Data;
using CafeManagement.Api.Contracts;
using CafeManagement.Domain.Entities;
using CafeManagement.Domain.Enums;
using CafeManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CafeManagement.Api.Services;

public sealed class KhuyenMaiService(
    CafeDbContext db, IHttpContextAccessor accessor, IConfiguration configuration) : IKhuyenMaiService
{
    public async Task<PageResponse<PromotionResponse>> TimKiemKhuyenMaiAsync(
        ListRequest request, PromotionStatus? status, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var query = db.KhuyenMais.AsNoTracking();
        if (status.HasValue)
        {
            query = status == PromotionStatus.Expired
                ? query.Where(x => x.TrangThai == PromotionStatus.Expired || x.NgayKetThuc < today)
                : query.Where(x => x.TrangThai == status && x.NgayKetThuc >= today);
        }
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(x => x.MaKhuyenMai.Contains(search) || x.TenChuongTrinh.Contains(search));
        }
        var total = await query.CountAsync(ct);
        var offset = ((long)request.Page - 1) * request.PageSize;
        var rows = offset > int.MaxValue ? [] : await query.OrderBy(x => x.MaKhuyenMai)
            .Skip((int)offset).Take(request.PageSize).ToListAsync(ct);
        return new PageResponse<PromotionResponse>(
            rows.Select(x => Map(x, today)).ToList(), total, request.Page, request.PageSize);
    }

    public async Task<PromotionResponse> LayKhuyenMaiAsync(string id, CancellationToken ct) =>
        Map(await FindAsync(id, ct), DateOnly.FromDateTime(DateTime.UtcNow));

    public Task<PromotionResponse> ThemKhuyenMaiAsync(SavePromotionRequest request, CancellationToken ct) =>
        SaveAsync(null, request, ct);

    public Task<PromotionResponse> SuaKhuyenMaiAsync(
        string id, SavePromotionRequest request, CancellationToken ct) =>
        SaveAsync(id, request, ct);

    private async Task<PromotionResponse> SaveAsync(string? id, SavePromotionRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ApiException(400, "Promotion name is required.");
        var code = request.Code.Trim().ToUpperInvariant();
        if (code.Length is 0 or > 10 || (id is not null && !string.Equals(id, code, StringComparison.Ordinal)))
            throw new ApiException(400, "Promotion code is the primary key and must contain at most 10 characters.");
        var startDate = DateOnly.FromDateTime(request.StartAt.UtcDateTime);
        var endDate = DateOnly.FromDateTime(request.EndAt.UtcDateTime);
        if (endDate <= startDate ||
            (request.Type == PromotionType.Percentage && request.DiscountValue > 100))
            throw new ApiException(400, "Promotion dates or percentage are invalid.");
        if (request.MaxDiscount.HasValue || !string.IsNullOrWhiteSpace(request.ConditionDescription))
            throw new ApiException(400, "Maximum discount and custom conditions are not part of the ERD promotion model.");
        ServiceHelpers.Money(request.DiscountValue);
        ServiceHelpers.Money(request.MinOrderAmount);

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var promotion = id is null
            ? new KhuyenMai { MaKhuyenMai = code }
            : await FindAsync(id, ct);
        if (id is null)
        {
            if (request.Status == PromotionStatus.Expired)
                throw new ApiException(400, "Create the promotion as Active or Inactive; end it through the promotion actions.");
            if (await db.KhuyenMais.AnyAsync(x => x.MaKhuyenMai == code, ct))
                throw ServiceHelpers.Conflict("The promotion code is already in use.");
            db.KhuyenMais.Add(promotion);
        }

        if (id is not null && (promotion.TrangThai == PromotionStatus.Expired ||
            DateOnly.FromDateTime(DateTime.UtcNow) >= promotion.NgayBatDau) &&
            (promotion.NgayBatDau != startDate || promotion.NgayKetThuc != endDate ||
             promotion.LoaiGiamGia != request.Type || promotion.GiaTriGiam != request.DiscountValue ||
             promotion.GiaTriDonToiThieu != request.MinOrderAmount))
            throw ServiceHelpers.Conflict("Promotion terms are locked after the start date or early ending; only its name can be changed.");

        promotion.TenChuongTrinh = request.Name.Trim();
        if (id is null || (promotion.TrangThai != PromotionStatus.Expired &&
            DateOnly.FromDateTime(DateTime.UtcNow) < promotion.NgayBatDau))
        {
            promotion.NgayBatDau = startDate;
            promotion.NgayKetThuc = endDate;
            promotion.LoaiGiamGia = request.Type;
            promotion.GiaTriGiam = request.DiscountValue;
            promotion.GiaTriDonToiThieu = request.MinOrderAmount;
        }
        if (id is null)
            promotion.TrangThai = request.Status;
        ServiceHelpers.Audit(db, ServiceHelpers.Actor(accessor), "PromotionSaved", "KhuyenMai",
            promotion.MaKhuyenMai, $"Saved promotion {promotion.TenChuongTrinh}.");
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Map(promotion, DateOnly.FromDateTime(DateTime.UtcNow));
    }

    public async Task TamNgungKhuyenMaiAsync(string id, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var promotion = await FindAsync(id, ct);
        if (promotion.TrangThai != PromotionStatus.Active ||
            promotion.NgayKetThuc < DateOnly.FromDateTime(DateTime.UtcNow))
            throw ServiceHelpers.Conflict("Only an active promotion can be paused.");
        promotion.TrangThai = PromotionStatus.Inactive;
        ServiceHelpers.Audit(db, ServiceHelpers.Actor(accessor), "PromotionStopped", "KhuyenMai", id,
            "Promotion paused.");
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task<PromotionResponse> KichHoatLaiKhuyenMaiAsync(string id, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var promotion = await FindAsync(id, ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (promotion.TrangThai != PromotionStatus.Inactive || promotion.NgayKetThuc < today)
            throw ServiceHelpers.Conflict("Only a paused promotion within its effective period can be resumed.");
        promotion.TrangThai = PromotionStatus.Active;
        ServiceHelpers.Audit(db, ServiceHelpers.Actor(accessor), "PromotionResumed", "KhuyenMai", id,
            "Promotion resumed.");
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Map(promotion, today);
    }

    public async Task<PromotionResponse> KetThucKhuyenMaiAsync(string id, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var promotion = await FindAsync(id, ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (promotion.TrangThai is not (PromotionStatus.Active or PromotionStatus.Inactive) ||
            promotion.NgayKetThuc < today)
            throw ServiceHelpers.Conflict("Only a current or paused promotion can be ended early.");
        promotion.TrangThai = PromotionStatus.Expired;
        ServiceHelpers.Audit(db, ServiceHelpers.Actor(accessor), "PromotionEnded", "KhuyenMai", id,
            "Promotion ended early.");
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Map(promotion, today);
    }

    public async Task<PromotionValidationResponse> KiemTraHopLeAsync(
        ValidatePromotionRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var order = await db.DonHangs.Include(x => x.ChiTiets).ThenInclude(x => x.ChiTietToppings)
            .SingleOrDefaultAsync(x => x.MaDon == request.OrderId, ct) ?? throw ServiceHelpers.NotFound("Order");
        if (order.TrangThai is OrderStatus.Paid or OrderStatus.Cancelled)
            throw ServiceHelpers.Conflict("A promotion can only be applied to an unpaid active order.");
        var promotion = await BillingQueries.PromotionAsync(db, request.Code, ct);
        var subtotal = order.ChiTiets.Where(x => x.TrangThaiMon != OrderItemStatus.Cancelled)
            .Sum(x => x.DonGia * x.SoLuong);
        var discount = CalculateDiscount(promotion, subtotal, DateOnly.FromDateTime(DateTime.UtcNow));
        var calculation = BillingCalculator.Calculate(configuration, subtotal, discount);
        await transaction.CommitAsync(ct);
        return new(promotion.MaKhuyenMai, promotion.MaKhuyenMai, subtotal, calculation.VatAmount,
            calculation.ServiceFeeAmount, discount, calculation.TotalAmount, calculation.Warnings);
    }

    public async Task<PageResponse<InvoiceResponse>> LayLichSuSuDungAsync(
        string id, ListRequest request, CancellationToken ct)
    {
        _ = await FindAsync(id, ct);
        var query = BillingQueries.Invoices(db).AsNoTracking()
            .Where(x => x.MaKhuyenMai == id && x.TrangThai == InvoiceStatus.Paid);
        var total = await query.CountAsync(ct);
        var offset = ((long)request.Page - 1) * request.PageSize;
        var invoices = offset > int.MaxValue ? [] : await query.OrderByDescending(x => x.ThoiGianLap)
            .Skip((int)offset).Take(request.PageSize).ToListAsync(ct);
        var audits = await BillingQueries.CancellationAuditsAsync(db, invoices, ct);
        return new(invoices.Select(invoice => BillingQueries.Map(invoice,
            audits.GetValueOrDefault($"HoaDon:{invoice.MaHoaDon}"))).ToList(),
            total, request.Page, request.PageSize);
    }

    public async Task<PromotionUsageSummaryResponse> LayThongKeSuDungAsync(string id, CancellationToken ct)
    {
        _ = await FindAsync(id, ct);
        var query = db.HoaDons.AsNoTracking()
            .Where(x => x.MaKhuyenMai == id && x.TrangThai == InvoiceStatus.Paid);
        var count = await query.CountAsync(ct);
        var totalDiscount = await query.SumAsync(x => (decimal?)x.TienGiamGia, ct) ?? 0m;
        return new(count, totalDiscount);
    }

    internal static decimal CalculateDiscount(KhuyenMai promotion, decimal subtotal, DateOnly today)
    {
        if (promotion.TrangThai != PromotionStatus.Active ||
            today < promotion.NgayBatDau || today > promotion.NgayKetThuc ||
            subtotal < promotion.GiaTriDonToiThieu)
            throw new ApiException(400, "The promotion is not valid for this order.");

        var discount = promotion.LoaiGiamGia == PromotionType.Percentage
            ? subtotal * promotion.GiaTriGiam / 100m
            : promotion.GiaTriGiam;
        return decimal.Min(decimal.Round(discount, 2, MidpointRounding.AwayFromZero), subtotal);
    }

    private async Task<KhuyenMai> FindAsync(string id, CancellationToken ct) =>
        await db.KhuyenMais.SingleOrDefaultAsync(x => x.MaKhuyenMai == id, ct)
        ?? throw ServiceHelpers.NotFound("Promotion");

    private static PromotionResponse Map(KhuyenMai x, DateOnly today) => new(
        x.MaKhuyenMai,
        x.MaKhuyenMai,
        x.TenChuongTrinh,
        new DateTimeOffset(x.NgayBatDau.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
        new DateTimeOffset(x.NgayKetThuc.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero),
        x.LoaiGiamGia,
        x.GiaTriGiam,
        null,
        x.GiaTriDonToiThieu,
        x.NgayKetThuc < today ? PromotionStatus.Expired : x.TrangThai,
        null,
        x.TrangThai == PromotionStatus.Expired || x.NgayKetThuc < today
            ? "Ended"
            : x.TrangThai == PromotionStatus.Inactive
                ? "Inactive"
                : x.NgayBatDau > today
                    ? "Scheduled"
                    : "Active");
}
