using CafeManagement.Api.Contracts;
using CafeManagement.Api.Utils;
using CafeManagement.Domain.Entities;
using CafeManagement.Domain.Enums;
using CafeManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace CafeManagement.Api.Services;

public sealed class ReportService(CafeDbContext db) : IReportService
{
    public async Task<RevenueReportResponse> RevenueAsync(ReportRange range, ListRequest page, CancellationToken ct)
    {
        Validate(range);
        var invoices = Paid(range);
        var summary = await invoices.GroupBy(_ => 1).Select(group => new
        {
            Count = group.Count(),
            Revenue = group.Sum(x => x.TongThanhToan),
            Discount = group.Sum(x => x.TienGiamGia)
        }).SingleOrDefaultAsync(ct) ?? throw NoData();
        var days = await RevenueRows(range).ToListAsync(ct);
        return new(range.From, range.To, summary.Count, summary.Revenue, summary.Discount,
            Page(days.Select(x => new RevenueDayResponse(DateOnly.FromDateTime(x.Day), x.Count, x.Revenue, x.Discount))
                .ToList(), page));
    }

    public async Task<PageResponse<TopItemResponse>> TopItemsAsync(
        ReportRange range, ListRequest page, CancellationToken ct)
    {
        Validate(range);
        var result = await ServiceHelpers.PageAsync(TopRows(range, page.Search), page, x => x, ct);
        if (result.TotalCount == 0)
            throw NoData();
        return result;
    }

    public async Task<PageResponse<PaymentMethodResponse>> PaymentMethodsAsync(
        ReportRange range, ListRequest page, CancellationToken ct)
    {
        Validate(range);
        var rows = await MethodRows(range).ToListAsync(ct);
        if (rows.Count == 0)
            throw NoData();
        return Page(rows, page);
    }

    public async Task<PageResponse<IngredientResponse>> InventoryAsync(
        ListRequest page, string? filter, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        IQueryable<NguyenLieu> query = InventoryRows(page.Search, filter, today);
        var total = await query.CountAsync(ct);
        var offset = ((long)page.Page - 1) * page.PageSize;
        var rows = offset > int.MaxValue ? [] : await query.OrderBy(x => x.MaNguyenLieu)
            .Skip((int)offset).Take(page.PageSize)
            .Select(x => new
            {
                x.MaNguyenLieu,
                x.TenNguyenLieu,
                x.DonViTinh,
                x.SoLuongTon,
                x.HanSuDung,
                x.DinhMucToiThieu,
                RowVersion = EF.Property<byte[]>(x, "RowVersion")
            }).ToListAsync(ct);
        if (total == 0)
            throw NoData();
        var soon = today.AddDays(7);
        var items = rows.Select(x => new IngredientResponse(
            x.MaNguyenLieu, x.MaNguyenLieu, x.TenNguyenLieu, x.DonViTinh, x.SoLuongTon, x.HanSuDung,
            x.DinhMucToiThieu, x.SoLuongTon <= x.DinhMucToiThieu, x.SoLuongTon == 0,
            x.HanSuDung < today, x.HanSuDung >= today && x.HanSuDung <= soon,
            Convert.ToBase64String(x.RowVersion))).ToList();
        return new(items, total, page.Page, page.PageSize);
    }

    public async Task<PageResponse<ReportInvoiceResponse>> DetailsAsync(
        ReportDetailRequest request, ListRequest page, CancellationToken ct)
    {
        var range = new ReportRange(request.From!.Value, request.To!.Value);
        Validate(range);
        var invoices = Paid(range);
        switch (request.Report.ToLowerInvariant())
        {
            case "revenue":
                if (!DateOnly.TryParseExact(request.Key, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var day))
                    throw new ApiException(400, "Revenue report keys must use yyyy-MM-dd.");
                var start = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                var end = day.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                invoices = invoices.Where(x => x.ThoiGianLap >= start && x.ThoiGianLap < end);
                break;
            case "top-items":
                invoices = invoices.Where(invoice => db.ChiTietDonHangs.AsNoTracking().Any(detail =>
                    detail.MaDon == invoice.MaDon && detail.MaMon == request.Key &&
                    detail.TrangThaiMon != OrderItemStatus.Cancelled));
                break;
            case "payment-methods":
                if (!Enum.TryParse<PaymentMethod>(request.Key, true, out var method) || !Enum.IsDefined(method))
                    throw new ApiException(400, "Unknown payment method report key.");
                invoices = invoices.Where(x => x.PhuongThucThanhToan == method);
                break;
            default:
                throw new ApiException(400, "Report must be revenue, top-items or payment-methods.");
        }

        var query =
            from invoice in invoices
            join order in db.DonHangs.AsNoTracking() on invoice.MaDon equals order.MaDon
            orderby invoice.ThoiGianLap descending, invoice.MaHoaDon
            select new ReportInvoiceRow(invoice.MaHoaDon, order.MaBan, invoice.ThoiGianLap,
                invoice.TongThanhToan, invoice.PhuongThucThanhToan);
        var total = await query.CountAsync(ct);
        if (total == 0)
            throw NoData();
        var offset = ((long)page.Page - 1) * page.PageSize;
        var rows = offset > int.MaxValue ? [] : await query.Skip((int)offset).Take(page.PageSize).ToListAsync(ct);
        return new(rows.Select(x => new ReportInvoiceResponse(x.Id, x.Id, x.TableCode,
            DateTime.SpecifyKind(x.PaidAt, DateTimeKind.Utc), x.TotalAmount, x.PaymentMethod)).ToList(),
            total, page.Page, page.PageSize);
    }

    public async Task<CsvDownload> ExportAsync(
        string report, ReportRange? range, string? filter, string? search, CancellationToken ct)
    {
        string[] headers;
        List<object?[]> rows;
        switch (report.ToLowerInvariant())
        {
            case "revenue":
                Validate(range);
                var revenue = await RevenueRows(range!).ToListAsync(ct);
                headers = ["Day", "InvoiceCount", "NetRevenue", "Discount"];
                rows = revenue.Select(x => new object?[]
                    { DateOnly.FromDateTime(x.Day), x.Count, x.Revenue, x.Discount }).ToList();
                break;
            case "top-items":
                Validate(range);
                var items = await TopRows(range!, search).ToListAsync(ct);
                headers = ["MenuItemId", "Name", "Quantity", "GrossRevenueBeforeDiscount"];
                rows = items.Select(x => new object?[] { x.MenuItemId, x.Name, x.Quantity, x.GrossRevenue }).ToList();
                break;
            case "payment-methods":
                Validate(range);
                var methods = await MethodRows(range!).ToListAsync(ct);
                headers = ["PaymentMethod", "InvoiceCount", "NetRevenue"];
                rows = methods.Select(x => new object?[] { x.PaymentMethod, x.InvoiceCount, x.Revenue }).ToList();
                break;
            case "inventory":
                var query = InventoryRows(search, filter, DateOnly.FromDateTime(DateTime.UtcNow));
                var stock = await query.OrderBy(x => x.MaNguyenLieu).ToListAsync(ct);
                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                headers = ["Code", "Name", "Unit", "Quantity", "MinQuantity", "ExpiryDate", "LowStock", "OutOfStock", "Expired", "ExpiringSoon"];
                rows = stock.Select(x => new object?[]
                {
                    x.MaNguyenLieu, x.TenNguyenLieu, x.DonViTinh, x.SoLuongTon, x.DinhMucToiThieu, x.HanSuDung,
                    x.SoLuongTon <= x.DinhMucToiThieu, x.SoLuongTon == 0, x.HanSuDung < today,
                    x.HanSuDung >= today && x.HanSuDung <= today.AddDays(7)
                }).ToList();
                break;
            default:
                throw new ApiException(400, "Report must be revenue, top-items, payment-methods or inventory.");
        }
        if (rows.Count == 0)
            throw NoData();
        return new($"cafe-{report.ToLowerInvariant()}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.csv",
            CsvWriter.Write(headers, rows));
    }

    private IQueryable<HoaDon> Paid(ReportRange range) =>
        db.HoaDons.AsNoTracking().Where(x => x.TrangThai == InvoiceStatus.Paid &&
            x.ThoiGianLap >= range.From.UtcDateTime && x.ThoiGianLap < range.To.UtcDateTime);

    private IQueryable<RevenueRow> RevenueRows(ReportRange range) => Paid(range)
        .GroupBy(x => x.ThoiGianLap.Date).OrderBy(group => group.Key)
        .Select(group => new RevenueRow(group.Key, group.Count(),
            group.Sum(x => x.TongThanhToan), group.Sum(x => x.TienGiamGia)));

    private IQueryable<TopItemResponse> TopRows(ReportRange range, string? search = null)
    {
        var query =
            from invoice in Paid(range)
            join detail in db.ChiTietDonHangs.AsNoTracking() on invoice.MaDon equals detail.MaDon
            join menu in db.Mons.AsNoTracking() on detail.MaMon equals menu.MaMon
            where detail.TrangThaiMon != OrderItemStatus.Cancelled
            group detail by new { detail.MaMon, menu.TenMon } into details
            select new
            {
                MenuItemId = details.Key.MaMon,
                Name = details.Key.TenMon,
                Quantity = details.Sum(x => x.SoLuong),
                GrossRevenue = details.Sum(x => x.DonGia * x.SoLuong)
            };
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(x => x.Name.Contains(search.Trim()));
        return query.OrderByDescending(x => x.Quantity).ThenBy(x => x.Name).ThenBy(x => x.MenuItemId)
            .Select(x => new TopItemResponse(x.MenuItemId, x.Name, x.Quantity, x.GrossRevenue));
    }

    private IQueryable<PaymentMethodResponse> MethodRows(ReportRange range) =>
        Paid(range).GroupBy(x => x.PhuongThucThanhToan).OrderBy(group => group.Key)
            .Select(group => new PaymentMethodResponse(group.Key, group.Count(),
                group.Sum(x => x.TongThanhToan)));

    private IQueryable<NguyenLieu> InventoryRows(string? search, string? filter, DateOnly today)
    {
        IQueryable<NguyenLieu> query = db.NguyenLieus.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim();
            query = query.Where(x => x.MaNguyenLieu.Contains(value) || x.TenNguyenLieu.Contains(value));
        }
        switch (filter?.Trim().ToLowerInvariant())
        {
            case null:
            case "":
            case "all":
                break;
            case "low-stock":
                query = query.Where(x => x.SoLuongTon <= x.DinhMucToiThieu);
                break;
            case "expiring":
                var soon = today.AddDays(7);
                query = query.Where(x => x.HanSuDung >= today && x.HanSuDung <= soon);
                break;
            default:
                throw new ApiException(400, "Inventory filter must be all, low-stock or expiring.");
        }
        return query;
    }

    private static void Validate(ReportRange? range)
    {
        if (range is null || range.From == default || range.To == default || range.From >= range.To)
            throw new ApiException(400, "A reporting range with from < to is required; to is exclusive.");
    }

    private static ApiException NoData() => new(404, "No report data matches the selected filters.");

    private static PageResponse<T> Page<T>(IReadOnlyList<T> rows, ListRequest request)
    {
        var offset = ((long)request.Page - 1) * request.PageSize;
        IReadOnlyList<T> items = offset > int.MaxValue ? [] : rows.Skip((int)offset).Take(request.PageSize).ToList();
        return new(items, rows.Count, request.Page, request.PageSize);
    }

    private sealed record RevenueRow(DateTime Day, int Count, decimal Revenue, decimal Discount);
    private sealed record ReportInvoiceRow(
        string Id, string? TableCode, DateTime PaidAt, decimal TotalAmount, PaymentMethod PaymentMethod);
}
