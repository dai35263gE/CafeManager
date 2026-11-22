using CafeManagement.Api.Contracts;
using CafeManagement.Domain.Entities;
using CafeManagement.Domain.Enums;
using CafeManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CafeManagement.Api.Services;

public sealed class BanService(CafeDbContext db, IHttpContextAccessor accessor) : IBanService
{
    public async Task<PageResponse<TableResponse>> XemSoDoBanAsync(
        ListRequest request, TableStatus? status, string? area, CancellationToken ct)
    {
        var query = db.Bans.AsNoTracking();
        if (status.HasValue)
            query = query.Where(x => x.TrangThai == status);
        if (!string.IsNullOrWhiteSpace(area))
            query = query.Where(x => x.KhuVuc == area.Trim());
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(x => x.MaBan.Contains(search) || x.KhuVuc.Contains(search));
        }

        var total = await query.CountAsync(ct);
        var offset = ((long)request.Page - 1) * request.PageSize;
        var rows = offset > int.MaxValue
            ? []
            : await query.OrderBy(x => x.KhuVuc).ThenBy(x => x.MaBan)
                .Include(x => x.DonHangBans.Where(link => link.ThoiGianKetThuc == null))
                    .ThenInclude(link => link.DonHang)
                .AsSplitQuery()
                .Skip((int)offset).Take(request.PageSize).ToListAsync(ct);
        return new(rows.Select(Map).ToList(), total, request.Page, request.PageSize);
    }

    public async Task<TableResponse> LayBanAsync(string id, CancellationToken ct) =>
        Map(await FindAsync(id, ct));

    public async Task<TableResponse> ThemBanAsync(SaveTableRequest request, CancellationToken ct)
    {
        var code = RequiredText(request.Code, "Table code", 10);
        var area = RequiredText(request.Area, "Area", 50);
        if (await db.Bans.AnyAsync(x => x.MaBan == code, ct))
            throw ServiceHelpers.Conflict("The table code is already in use.");

        var table = new Ban
        {
            MaBan = code,
            KhuVuc = area,
            GhiChu = OptionalText(request.Note, 255),
            TrangThai = TableStatus.Available
        };
        db.Bans.Add(table);
        await SaveAsync(table, "TableCreated", ct);
        return Map(table);
    }

    public async Task<TableResponse> SuaThongTinAsync(string id, SaveTableRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var table = await FindAsync(id, ct);
        ServiceHelpers.Version(db.Entry(table), table.RowVersion, request.RowVersion
            ?? throw new ApiException(400, "RowVersion is required."));
        if (!string.Equals(RequiredText(request.Code, "Table code", 10), id, StringComparison.Ordinal))
            throw new ApiException(400, "A table's primary code cannot be changed.");
        table.KhuVuc = RequiredText(request.Area, "Area", 50);
        table.GhiChu = OptionalText(request.Note, 255);
        await SaveAsync(table, "TableUpdated", ct);
        await transaction.CommitAsync(ct);
        return Map(table);
    }

    public async Task<TableResponse> XepBanAsync(string id, SeatTableRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var table = await FindAsync(id, ct);
        ServiceHelpers.Version(db.Entry(table), table.RowVersion, request.RowVersion);
        if (table.TrangThai != TableStatus.Available || await HasActiveOrderAsync(id, ct))
            throw ServiceHelpers.Conflict("Only an available table without an active order can be seated.");
        table.TrangThai = TableStatus.InUse;
        await SaveAsync(table, "TableSeated", ct);
        await transaction.CommitAsync(ct);
        return Map(table);
    }

    public async Task<TableResponse> CapNhatTrangThaiBanAsync(string id, TableStatusRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var table = await FindAsync(id, ct);
        ServiceHelpers.Version(db.Entry(table), table.RowVersion, request.RowVersion);
        if (request.Status is TableStatus.InUse or TableStatus.Disabled)
            throw new ApiException(400, "Use the seating or manager operation to change this table status.");
        if (table.TrangThai == TableStatus.Disabled)
            throw ServiceHelpers.Conflict("A disabled table cannot be cleaned or made available.");
        if (await HasActiveOrderAsync(id, ct))
            throw ServiceHelpers.Conflict("A table with an active order cannot be cleaned or released.");
        if (request.Status == TableStatus.Available && table.TrangThai != TableStatus.NeedsCleaning)
            throw ServiceHelpers.Conflict("Only a table marked for cleaning can be returned to available.");

        table.TrangThai = request.Status;
        await SaveAsync(table, "TableStatusChanged", ct);
        await transaction.CommitAsync(ct);
        return Map(table);
    }

    public async Task NgungSuDungAsync(string id, string rowVersion, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var table = await FindAsync(id, ct);
        ServiceHelpers.Version(db.Entry(table), table.RowVersion, rowVersion);
        if (table.TrangThai == TableStatus.Disabled)
            throw ServiceHelpers.Conflict("The table is already disabled.");
        if (table.TrangThai == TableStatus.InUse || await HasActiveOrderAsync(id, ct))
            throw ServiceHelpers.Conflict("A table cannot be disabled while it is occupied or linked to an active order.");
        table.TrangThai = TableStatus.Disabled;
        await SaveAsync(table, "TableDisabled", ct);
        await transaction.CommitAsync(ct);
    }

    private Task<bool> HasActiveOrderAsync(string id, CancellationToken ct) =>
        db.DonHangBans.AnyAsync(link => link.MaBan == id && link.ThoiGianKetThuc == null &&
            link.DonHang.TrangThai != OrderStatus.Paid && link.DonHang.TrangThai != OrderStatus.Cancelled, ct);

    private async Task<Ban> FindAsync(string id, CancellationToken ct) =>
        await db.Bans
            .Include(x => x.DonHangBans.Where(link => link.ThoiGianKetThuc == null))
                .ThenInclude(link => link.DonHang)
            .AsSplitQuery()
            .SingleOrDefaultAsync(x => x.MaBan == id, ct) ?? throw ServiceHelpers.NotFound("Table");

    private async Task SaveAsync(Ban table, string action, CancellationToken ct)
    {
        ServiceHelpers.Audit(db, ServiceHelpers.Actor(accessor), action, "Ban", table.MaBan, action);
        await db.SaveChangesAsync(ct);
    }

    private static TableResponse Map(Ban table)
    {
        var activeOrder = table.DonHangBans
            .Where(link => link.ThoiGianKetThuc is null &&
                link.DonHang.TrangThai is not (OrderStatus.Paid or OrderStatus.Cancelled))
            .Select(link => link.DonHang)
            .SingleOrDefault();
        return new(
            table.MaBan, table.MaBan, table.KhuVuc, table.TrangThai, table.GhiChu,
            Convert.ToBase64String(table.RowVersion),
            activeOrder is null ? null : new ActiveTableOrderResponse(activeOrder.MaDon, activeOrder.MaDon,
                activeOrder.TrangThai, Convert.ToBase64String(activeOrder.RowVersion)));
    }

    private static string RequiredText(string? value, string name, int maximumLength)
    {
        var text = value?.Trim() ?? string.Empty;
        if (text.Length == 0 || text.Length > maximumLength)
            throw new ApiException(400, $"{name} must contain between 1 and {maximumLength} characters.");
        return text;
    }

    private static string OptionalText(string? value, int maximumLength)
    {
        var text = value?.Trim() ?? string.Empty;
        if (text.Length > maximumLength)
            throw new ApiException(400, $"Text cannot exceed {maximumLength} characters.");
        return text;
    }
}
