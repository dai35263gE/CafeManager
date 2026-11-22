using CafeManagement.Api.Contracts;
using CafeManagement.Domain.Entities;
using CafeManagement.Infrastructure;
using CafeManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CafeManagement.Api.Services;

public sealed class DinhLuongService(CafeDbContext db, IdGenerator ids, IHttpContextAccessor accessor) : IDinhLuongService
{
    public async Task<PageResponse<RecipeResponse>> TimKiemDinhLuongAsync(
        ListRequest request, string? menuItemId, CancellationToken ct)
    {
        var query = Query().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(menuItemId))
            query = query.Where(x => x.MaMon == menuItemId);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(x => x.Mon.TenMon.Contains(search) || x.NguyenLieu.TenNguyenLieu.Contains(search));
        }
        var total = await query.CountAsync(ct);
        var offset = ((long)request.Page - 1) * request.PageSize;
        var rows = offset > int.MaxValue ? [] : await query.OrderBy(x => x.MaMon)
            .ThenBy(x => x.MaNguyenLieu).Skip((int)offset).Take(request.PageSize).ToListAsync(ct);
        return new(rows.Select(Map).ToList(), total, request.Page, request.PageSize);
    }

    public async Task<RecipeResponse> LayDinhLuongAsync(string id, CancellationToken ct) =>
        Map(await Query().SingleOrDefaultAsync(x => x.MaDinhLuong == id, ct)
            ?? throw ServiceHelpers.NotFound("Recipe"));

    public async Task<RecipeResponse> CapNhatDinhLuongAsync(string? id, SaveRecipeRequest request, CancellationToken ct)
    {
        if (request.SoLuongSuDung <= 0 || request.SoLuongSuDung > 9999999.999m ||
            decimal.Round(request.SoLuongSuDung, 3) != request.SoLuongSuDung)
            throw new ApiException(400, "Recipe quantity must be positive, within the database range, and have at most three decimal places.");
        var menu = await db.Mons.SingleOrDefaultAsync(x => x.MaMon == request.MaMon, ct)
            ?? throw new ApiException(400, "Menu item does not exist.");
        var ingredient = await db.NguyenLieus.SingleOrDefaultAsync(x => x.MaNguyenLieu == request.MaNguyenLieu, ct)
            ?? throw new ApiException(400, "Ingredient does not exist.");

        var recipe = id is null
            ? new DinhLuong { MaDinhLuong = await ids.DinhLuongAsync(ct) }
            : await db.DinhLuongs.SingleOrDefaultAsync(x => x.MaDinhLuong == id, ct)
                ?? throw ServiceHelpers.NotFound("Recipe");
        if (id is null)
            db.DinhLuongs.Add(recipe);
        if (await db.DinhLuongs.AnyAsync(x =>
                x.MaDinhLuong != recipe.MaDinhLuong && x.MaMon == menu.MaMon && x.MaNguyenLieu == ingredient.MaNguyenLieu, ct))
            throw ServiceHelpers.Conflict("A menu item can have only one recipe entry per ingredient.");

        recipe.MaMon = menu.MaMon;
        recipe.Mon = menu;
        recipe.MaNguyenLieu = ingredient.MaNguyenLieu;
        recipe.NguyenLieu = ingredient;
        recipe.SoLuongSuDung = request.SoLuongSuDung;
        ServiceHelpers.Audit(db, ServiceHelpers.Actor(accessor), "RecipeSaved", "DinhLuong",
            recipe.MaDinhLuong, $"{menu.TenMon} uses {recipe.SoLuongSuDung} {ingredient.DonViTinh}.");
        await db.SaveChangesAsync(ct);
        return Map(await Query().SingleAsync(x => x.MaDinhLuong == recipe.MaDinhLuong, ct));
    }

    public async Task XoaDinhLuongAsync(string id, CancellationToken ct)
    {
        var recipe = await db.DinhLuongs.SingleOrDefaultAsync(x => x.MaDinhLuong == id, ct)
            ?? throw ServiceHelpers.NotFound("Recipe");
        db.DinhLuongs.Remove(recipe);
        ServiceHelpers.Audit(db, ServiceHelpers.Actor(accessor), "RecipeDeleted", "DinhLuong",
            recipe.MaDinhLuong, "Recipe entry deleted.");
        await db.SaveChangesAsync(ct);
    }

    private IQueryable<DinhLuong> Query() =>
        db.DinhLuongs.Include(x => x.Mon).Include(x => x.NguyenLieu);

    private static RecipeResponse Map(DinhLuong x) => new(
        x.MaDinhLuong, x.MaMon, x.Mon.TenMon, x.MaNguyenLieu,
        x.NguyenLieu.TenNguyenLieu, x.NguyenLieu.DonViTinh, x.SoLuongSuDung);
}
