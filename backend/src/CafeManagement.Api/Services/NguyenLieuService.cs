using System.Data;
using CafeManagement.Api.Contracts;
using CafeManagement.Domain.Entities;
using CafeManagement.Domain.Enums;
using CafeManagement.Infrastructure;
using CafeManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CafeManagement.Api.Services;

public sealed class NguyenLieuService(
    CafeDbContext db,
    IdGenerator ids,
    IHttpContextAccessor accessor) : INguyenLieuService
{
    public async Task<PageResponse<IngredientResponse>> TimKiemNguyenLieuAsync(
        ListRequest request, bool warningsOnly, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var soon = today.AddDays(7);
        IQueryable<NguyenLieu> query = db.NguyenLieus;
        if (warningsOnly)
            query = query.Where(x => x.SoLuongTon <= x.DinhMucToiThieu || x.HanSuDung <= soon);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(x => x.MaNguyenLieu.Contains(search) || x.TenNguyenLieu.Contains(search));
        }
        var total = await query.CountAsync(ct);
        var offset = ((long)request.Page - 1) * request.PageSize;
        var rows = offset > int.MaxValue ? [] : await query.OrderBy(x => x.MaNguyenLieu)
            .Skip((int)offset).Take(request.PageSize).ToListAsync(ct);
        return new(rows.Select(x => Map(x, today)).ToList(), total, request.Page, request.PageSize);
    }

    public async Task<IngredientResponse> LayNguyenLieuAsync(string id, CancellationToken ct) =>
        Map(await FindAsync(id, ct), DateOnly.FromDateTime(DateTime.UtcNow));

    public Task<IngredientResponse> ThemNguyenLieuAsync(SaveIngredientRequest request, CancellationToken ct) =>
        SaveAsync(null, request, ct);

    public Task<IngredientResponse> CapNhatNguyenLieuAsync(
        string id, SaveIngredientRequest request, CancellationToken ct) =>
        SaveAsync(id, request, ct);

    private async Task<IngredientResponse> SaveAsync(string? id, SaveIngredientRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        CheckQuantity(request.MinQuantity);
        if (request.ExpiryDate is null)
            throw new ApiException(400, "ExpiryDate is required by the ERD model.");
        var code = (id ?? request.Code).Trim();
        if (code.Length is 0 or > 10 || (id is not null && request.Code.Trim() != id))
            throw new ApiException(400, "Ingredient code is the primary key and cannot be changed.");
        if (request.Unit.Trim().Length > 20)
            throw new ApiException(400, "Ingredient unit cannot exceed 20 characters.");

        var ingredient = id is null
            ? new NguyenLieu { MaNguyenLieu = code }
            : await FindAsync(id, ct);
        if (id is null)
        {
            if (await db.NguyenLieus.AnyAsync(x => x.MaNguyenLieu == code, ct))
                throw ServiceHelpers.Conflict("The ingredient code is already in use.");
            db.NguyenLieus.Add(ingredient);
        }
        else if (ingredient.DonViTinh != request.Unit.Trim() &&
                 await db.BienDongKhos.AnyAsync(x => x.MaNguyenLieu == id, ct))
        {
            throw ServiceHelpers.Conflict("Units cannot change after stock history exists.");
        }
        if (id is not null)
            ServiceHelpers.Version(db.Entry(ingredient),
                db.Entry(ingredient).Property<byte[]>("RowVersion").CurrentValue!, request.RowVersion
                    ?? throw new ApiException(400, "RowVersion is required."));

        ingredient.TenNguyenLieu = request.Name.Trim();
        ingredient.DonViTinh = request.Unit.Trim();
        ingredient.DinhMucToiThieu = request.MinQuantity;
        ingredient.HanSuDung = request.ExpiryDate.Value;
        ServiceHelpers.Audit(db, ServiceHelpers.Actor(accessor), "IngredientSaved", "NguyenLieu",
            ingredient.MaNguyenLieu, "Ingredient metadata saved.");
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Map(ingredient, DateOnly.FromDateTime(DateTime.UtcNow));
    }

    public async Task XoaNguyenLieuAsync(string id, string rowVersion, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var ingredient = await FindAsync(id, ct);
        ServiceHelpers.Version(db.Entry(ingredient),
            db.Entry(ingredient).Property<byte[]>("RowVersion").CurrentValue!, rowVersion);
        if (ingredient.SoLuongTon != 0 ||
            await db.BienDongKhos.AnyAsync(x => x.MaNguyenLieu == id, ct) ||
            await db.DinhLuongs.AnyAsync(x => x.MaNguyenLieu == id, ct))
            throw ServiceHelpers.Conflict("An ingredient with stock, movement history, or recipes cannot be deleted.");
        db.NguyenLieus.Remove(ingredient);
        ServiceHelpers.Audit(db, ServiceHelpers.Actor(accessor), "IngredientDeleted", "NguyenLieu",
            id, "Ingredient deleted.");
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task<InventoryMovementResponse> LapBienDongAsync(
        string id, InventoryTransactionType type, InventoryMovementRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var ingredient = await FindAsync(id, ct);
        ServiceHelpers.Version(db.Entry(ingredient),
            db.Entry(ingredient).Property<byte[]>("RowVersion").CurrentValue!, request.RowVersion);
        if (decimal.Round(request.Quantity, 3) != request.Quantity || Math.Abs(request.Quantity) > 9999999.999m)
            throw new ApiException(400, "Movement quantity must have at most three decimal places and be within range.");
        if (type == InventoryTransactionType.Stocktake && request.Quantity < 0)
            throw new ApiException(400, "Stocktake quantity must be nonnegative.");
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new ApiException(400, "A stock movement reason is required.");
        var previous = ingredient.SoLuongTon;
        var delta = type switch
        {
            InventoryTransactionType.Import when request.Quantity > 0 => request.Quantity,
            InventoryTransactionType.Export when request.Quantity > 0 && request.Quantity <= previous => -request.Quantity,
            InventoryTransactionType.Stocktake => request.Quantity - previous,
            InventoryTransactionType.Adjust when request.Quantity != 0 => request.Quantity,
            InventoryTransactionType.Import => throw new ApiException(400, "Import quantity must be positive."),
            InventoryTransactionType.Export => throw new ApiException(400, "Export must be positive and cannot exceed stock."),
            InventoryTransactionType.Adjust => throw new ApiException(400, "Adjustment must be nonzero."),
            _ => throw new ApiException(400, "Invalid stock movement type.")
        };
        var result = previous + delta;
        if (result < 0)
            throw ServiceHelpers.Conflict("Stock cannot become negative.");
        if (result > 9999999.999m)
            throw new ApiException(400, "Stock cannot exceed the database range.");

        ingredient.SoLuongTon = result;
        var movement = new BienDongKho
        {
            MaBienDong = await ids.BienDongAsync(ct),
            MaNguyenLieu = ingredient.MaNguyenLieu,
            NguyenLieu = ingredient,
            MaNhanVien = ServiceHelpers.Actor(accessor),
            LoaiBienDong = type,
            SoLuong = delta,
            ThoiGian = DateTime.UtcNow,
            LyDo = request.Reason.Trim()
        };
        db.BienDongKhos.Add(movement);
        ServiceHelpers.Audit(db, movement.MaNhanVien, "InventoryMoved", "NguyenLieu", id,
            $"{type}: {delta}. {request.Reason.Trim()}");
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(Map(ingredient, DateOnly.FromDateTime(DateTime.UtcNow)), MapTransaction(movement, previous, result));
    }

    public async Task<PageResponse<InventoryTransactionResponse>> TimKiemBienDongAsync(
        string id, ListRequest request, CancellationToken ct)
    {
        if (!await db.NguyenLieus.AnyAsync(x => x.MaNguyenLieu == id, ct))
            throw ServiceHelpers.NotFound("Ingredient");
        var movements = await db.BienDongKhos.AsNoTracking().Where(x => x.MaNguyenLieu == id)
            .OrderBy(x => x.ThoiGian).ThenBy(x => x.MaBienDong).ToListAsync(ct);
        var balance = 0m;
        var rows = new List<InventoryTransactionResponse>(movements.Count);
        foreach (var movement in movements)
        {
            var previous = balance;
            balance += movement.SoLuong;
            rows.Add(MapTransaction(movement, previous, balance));
        }
        IEnumerable<InventoryTransactionResponse> query = rows;
        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(x => x.Reason.Contains(request.Search.Trim(), StringComparison.OrdinalIgnoreCase));
        var filtered = query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).ToList();
        var offset = ((long)request.Page - 1) * request.PageSize;
        var items = offset > int.MaxValue ? [] : filtered.Skip((int)offset).Take(request.PageSize).ToList();
        return new(items, filtered.Count, request.Page, request.PageSize);
    }

    public async Task<InventoryTransactionResponse> LayBienDongAsync(
        string id, string transactionId, CancellationToken ct)
    {
        if (!await db.NguyenLieus.AnyAsync(x => x.MaNguyenLieu == id, ct))
            throw ServiceHelpers.NotFound("Ingredient");
        var movements = await db.BienDongKhos.AsNoTracking().Where(x => x.MaNguyenLieu == id)
            .OrderBy(x => x.ThoiGian).ThenBy(x => x.MaBienDong).ToListAsync(ct);
        var balance = 0m;
        InventoryTransactionResponse? result = null;
        foreach (var movement in movements)
        {
            var previous = balance;
            balance += movement.SoLuong;
            if (movement.MaBienDong == transactionId)
                result = MapTransaction(movement, previous, balance);
        }
        return result
            ?? throw ServiceHelpers.NotFound("Inventory transaction");
    }

    private async Task<NguyenLieu> FindAsync(string id, CancellationToken ct) =>
        await db.NguyenLieus.SingleOrDefaultAsync(x => x.MaNguyenLieu == id, ct)
        ?? throw ServiceHelpers.NotFound("Ingredient");

    private IngredientResponse Map(NguyenLieu x, DateOnly today)
    {
        var soon = today.AddDays(7);
        return new(x.MaNguyenLieu, x.MaNguyenLieu, x.TenNguyenLieu, x.DonViTinh, x.SoLuongTon,
            x.HanSuDung, x.DinhMucToiThieu, x.SoLuongTon <= x.DinhMucToiThieu, x.SoLuongTon == 0,
            x.HanSuDung < today, x.HanSuDung >= today && x.HanSuDung <= soon,
            Convert.ToBase64String(db.Entry(x).Property<byte[]>("RowVersion").CurrentValue!));
    }

    private static InventoryTransactionResponse MapTransaction(BienDongKho x, decimal previous, decimal result) =>
        new(x.MaBienDong, x.MaNguyenLieu, x.LoaiBienDong, Math.Abs(x.SoLuong), previous, result,
            x.LyDo, x.MaNhanVien, new DateTimeOffset(DateTime.SpecifyKind(x.ThoiGian, DateTimeKind.Utc)));

    private static void CheckQuantity(decimal quantity)
    {
        if (quantity < 0 || quantity > 9999999.999m || decimal.Round(quantity, 3) != quantity)
            throw new ApiException(400, "Quantity must be nonnegative, within the database range, and have at most three decimal places.");
    }
}
