using CafeManagement.Api.Contracts;
using CafeManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CafeManagement.Api.Services;

public sealed class NhatKyThaoTacService(CafeDbContext db) : INhatKyThaoTacService
{
    public async Task<PageResponse<AuditLogResponse>> TimKiemNhatKyAsync(
        ListRequest request, DateTimeOffset? from, DateTimeOffset? to, string? action, CancellationToken ct)
    {
        if (from.HasValue && to.HasValue && from >= to)
            throw new ApiException(400, "Audit date range must have from < to.");
        var query = db.NhatKyThaoTacs.AsNoTracking().Include(x => x.NhanVien).AsQueryable();
        if (from.HasValue)
            query = query.Where(x => x.ThoiGian >= from.Value.UtcDateTime);
        if (to.HasValue)
            query = query.Where(x => x.ThoiGian < to.Value.UtcDateTime);
        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(x => x.LoaiThaoTac == action.Trim());
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(x => x.NoiDung.Contains(search) ||
                x.DoiTuongTacDong.Contains(search) || x.NhanVien.HoTen.Contains(search));
        }
        var total = await query.CountAsync(ct);
        var offset = ((long)request.Page - 1) * request.PageSize;
        var rows = offset > int.MaxValue ? [] : await query.OrderByDescending(x => x.ThoiGian)
            .ThenBy(x => x.MaNhatKy).Skip((int)offset).Take(request.PageSize).ToListAsync(ct);
        return new(rows.Select(x => new AuditLogResponse(
            x.MaNhatKy, x.MaNhanVien, x.NhanVien.HoTen, x.LoaiThaoTac,
            x.DoiTuongTacDong, x.NoiDung,
            new DateTimeOffset(DateTime.SpecifyKind(x.ThoiGian, DateTimeKind.Utc)))).ToList(),
            total, request.Page, request.PageSize);
    }
}
