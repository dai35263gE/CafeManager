using CafeManagement.Api.Contracts;
using CafeManagement.Domain.Entities;
using CafeManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CafeManagement.Api.Services;

internal static class BillingQueries
{
    public static async Task<KhuyenMai> PromotionAsync(CafeDbContext db, string code, CancellationToken ct)
    {
        var normalized = code.Trim();
        return await db.KhuyenMais.SingleOrDefaultAsync(x =>
                x.MaKhuyenMai == normalized || x.TenChuongTrinh == normalized, ct)
            ?? throw new ApiException(400, "Promotion code does not exist.");
    }

    public static IQueryable<HoaDon> Invoices(CafeDbContext db) => db.HoaDons
        .Include(x => x.DonHang).ThenInclude(x => x.Ban)
        .Include(x => x.DonHang).ThenInclude(x => x.ChiTiets).ThenInclude(x => x.Mon)
        .Include(x => x.DonHang).ThenInclude(x => x.ChiTiets).ThenInclude(x => x.KichCo)
        .Include(x => x.DonHang).ThenInclude(x => x.ChiTiets).ThenInclude(x => x.ChiTietToppings).ThenInclude(x => x.Topping)
        .Include(x => x.KhuyenMai).Include(x => x.NhanVien).AsSplitQuery();

    public static async Task<Dictionary<string, NhatKyThaoTac>> CancellationAuditsAsync(
        CafeDbContext db, IEnumerable<HoaDon> invoices, CancellationToken ct)
    {
        var targets = invoices.Where(invoice => invoice.TrangThai == Domain.Enums.InvoiceStatus.Cancelled)
            .Select(invoice => $"HoaDon:{invoice.MaHoaDon}").ToArray();
        if (targets.Length == 0)
            return new(StringComparer.Ordinal);
        var audits = await db.NhatKyThaoTacs.AsNoTracking()
            .Where(x => x.LoaiThaoTac == "InvoiceCancelled" && targets.Contains(x.DoiTuongTacDong))
            .ToListAsync(ct);
        return audits.GroupBy(x => x.DoiTuongTacDong, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(x => x.ThoiGian).First(),
                StringComparer.Ordinal);
    }

    public static InvoiceResponse Map(HoaDon invoice, NhatKyThaoTac? cancellation = null)
    {
        var paidAt = new DateTimeOffset(DateTime.SpecifyKind(invoice.ThoiGianLap, DateTimeKind.Utc));
        var rowVersion = invoice.RowVersion;
        if (rowVersion.Length == 0)
            throw new InvalidOperationException("The invoice row version was not loaded.");
        var cancelReason = cancellation?.NoiDung;
        if (cancelReason is not null)
        {
            var separator = cancelReason.IndexOf(": ", StringComparison.Ordinal);
            if (separator >= 0)
                cancelReason = cancelReason[(separator + 2)..];
        }
        DateTimeOffset? cancelledAt = cancellation is null
            ? null
            : new DateTimeOffset(DateTime.SpecifyKind(cancellation.ThoiGian, DateTimeKind.Utc));
        return new InvoiceResponse(
            invoice.MaHoaDon,
            invoice.MaHoaDon,
            invoice.MaDon,
            invoice.MaDon,
            invoice.DonHang.MaBan,
            invoice.DonHang.Ban?.MaBan,
            invoice.MaKhuyenMai,
            invoice.KhuyenMai?.MaKhuyenMai,
            invoice.TongTienHang,
            invoice.ThueVAT,
            invoice.PhiDichVu,
            invoice.TienGiamGia,
            invoice.TongThanhToan,
            invoice.PhuongThucThanhToan,
            invoice.TienKhachDua,
            invoice.TienThua,
            invoice.MaNhanVien,
            paidAt,
            invoice.TrangThai,
            cancelReason,
            cancellation?.MaNhanVien,
            cancelledAt,
            Convert.ToBase64String(rowVersion),
            invoice.DonHang.ChiTiets.OrderBy(x => x.MaChiTiet).Select(DonHangService.MapLine).ToList());
    }
}
