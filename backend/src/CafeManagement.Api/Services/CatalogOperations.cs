using System.Data;
using CafeManagement.Api.Contracts;
using CafeManagement.Domain.Entities;
using CafeManagement.Domain.Enums;
using CafeManagement.Infrastructure;
using CafeManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CafeManagement.Api.Services;

internal sealed class CatalogOperations(
    CafeDbContext db,
    IdGenerator ids,
    IHttpContextAccessor accessor) : ICatalogOperations
{
    public async Task<PageResponse<CategoryResponse>> CategoriesAsync(
        ListRequest request, bool? active, CancellationToken ct)
    {
        var query = db.DanhMucs.AsNoTracking();
        if (active == false)
            query = query.Where(_ => false);
        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(x => x.TenDanhMuc.Contains(request.Search.Trim()));
        var total = await query.CountAsync(ct);
        var offset = ((long)request.Page - 1) * request.PageSize;
        var rows = offset > int.MaxValue ? [] : await query.OrderBy(x => x.TenDanhMuc)
            .Skip((int)offset).Take(request.PageSize).ToListAsync(ct);
        return new(rows.Select(x => new CategoryResponse(x.MaDanhMuc, x.TenDanhMuc, null, true)).ToList(),
            total, request.Page, request.PageSize);
    }

    public async Task<CategoryResponse> CategoryAsync(string id, CancellationToken ct)
    {
        var category = await FindCategoryAsync(id, ct);
        return new(category.MaDanhMuc, category.TenDanhMuc, null, true);
    }

    public async Task<CategoryResponse> SaveCategoryAsync(string? id, SaveCategoryRequest request, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(request.Description) || !request.IsActive)
            throw new ApiException(400, "Category descriptions and active flags are not part of the ERD model.");

        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw Validation("Name", "Category name is required.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (await db.DanhMucs.AnyAsync(x => x.MaDanhMuc != id && x.TenDanhMuc == name, ct))
            throw FieldConflict("Name", "A category with this name already exists.");

        var category = id is null
            ? new DanhMuc { MaDanhMuc = await ids.DanhMucAsync(ct) }
            : await FindCategoryAsync(id, ct);
        if (id is null)
            db.DanhMucs.Add(category);
        category.TenDanhMuc = name;
        await SaveAsync("CategorySaved", "DanhMuc", category.MaDanhMuc, ct);
        await transaction.CommitAsync(ct);
        return new(category.MaDanhMuc, category.TenDanhMuc, null, true);
    }

    public async Task DeleteCategoryAsync(string id, CancellationToken ct)
    {
        var category = await FindCategoryAsync(id, ct);
        if (await db.Mons.AnyAsync(x => x.MaDanhMuc == id, ct))
            throw ServiceHelpers.Conflict("A category containing menu items cannot be deleted.");
        db.DanhMucs.Remove(category);
        await SaveAsync("CategoryDeleted", "DanhMuc", id, ct);
    }

    public async Task<PageResponse<SizeResponse>> SizesAsync(
        ListRequest request, bool? active, string? menuItemId, CancellationToken ct)
    {
        var query = db.KichCos.AsNoTracking();
        if (active == false)
            query = query.Where(_ => false);
        if (!string.IsNullOrWhiteSpace(menuItemId))
            query = query.Where(x => x.MaMon == menuItemId);
        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(x => x.TenKichCo.Contains(request.Search.Trim()));
        var total = await query.CountAsync(ct);
        var offset = ((long)request.Page - 1) * request.PageSize;
        var rows = offset > int.MaxValue ? [] : await query.OrderBy(x => x.MaMon).ThenBy(x => x.TenKichCo)
            .Skip((int)offset).Take(request.PageSize).ToListAsync(ct);
        return new(rows.Select(Map).ToList(), total, request.Page, request.PageSize);
    }

    public async Task<SizeResponse> SizeAsync(string id, CancellationToken ct) => Map(await FindSizeAsync(id, ct));

    public async Task<SizeResponse> SaveSizeAsync(string? id, SaveSizeRequest request, CancellationToken ct)
    {
        ValidatePrice(request.PriceDelta, "PriceDelta");
        var menu = await FindMenuAsync(request.MenuItemId, ct);
        if (menu.TrangThai == MenuItemStatus.Discontinued)
            throw ServiceHelpers.Conflict("A discontinued menu item cannot receive new sizes.");
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw Validation("Name", "Size name is required.");
        if (await db.KichCos.AnyAsync(x => x.MaMon == menu.MaMon && x.MaKichCo != id && x.TenKichCo == name, ct))
            throw FieldConflict("Name", "A menu item cannot contain duplicate size names.");
        var size = id is null
            ? new KichCo { MaKichCo = await ids.KichCoAsync(ct), MaMon = menu.MaMon, Mon = menu }
            : await FindSizeAsync(id, ct);
        if (id is not null && size.MaMon != menu.MaMon)
            throw new ApiException(400, "A size cannot be moved between menu items.");
        if (id is not null && size.TenKichCo != name &&
            await db.ChiTietDonHangs.AnyAsync(x => x.MaKichCo == size.MaKichCo, ct))
            throw FieldConflict("Name", "A size used in an order cannot be renamed.");
        if (!request.IsActive)
            throw new ApiException(400, "Sizes are removed, not deactivated, in the ERD model.");
        if (id is null)
            db.KichCos.Add(size);
        size.TenKichCo = name;
        size.GiaBan = request.PriceDelta;
        await SaveAsync("SizeSaved", "KichCo", size.MaKichCo, ct);
        return Map(size);
    }

    public async Task DeleteSizeAsync(string id, CancellationToken ct)
    {
        var size = await FindSizeAsync(id, ct);
        if (await db.ChiTietDonHangs.AnyAsync(x => x.MaKichCo == id, ct))
            throw ServiceHelpers.Conflict("A size used in an order cannot be deleted.");
        db.KichCos.Remove(size);
        await SaveAsync("SizeDeleted", "KichCo", id, ct);
    }

    public async Task<PageResponse<ToppingResponse>> ToppingsAsync(
        ListRequest request, bool? active, string? menuItemId, CancellationToken ct)
    {
        var query = db.Toppings.AsNoTracking();
        if (active == false)
            query = query.Where(_ => false);
        if (!string.IsNullOrWhiteSpace(menuItemId))
            query = query.Where(x => x.MaMon == menuItemId);
        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(x => x.TenTopping.Contains(request.Search.Trim()));
        var total = await query.CountAsync(ct);
        var offset = ((long)request.Page - 1) * request.PageSize;
        var rows = offset > int.MaxValue ? [] : await query.OrderBy(x => x.MaMon).ThenBy(x => x.TenTopping)
            .Skip((int)offset).Take(request.PageSize).ToListAsync(ct);
        return new(rows.Select(Map).ToList(), total, request.Page, request.PageSize);
    }

    public async Task<ToppingResponse> ToppingAsync(string id, CancellationToken ct) =>
        Map(await FindToppingAsync(id, ct));

    public async Task<ToppingResponse> SaveToppingAsync(string? id, SaveToppingRequest request, CancellationToken ct)
    {
        ValidatePrice(request.Price, "Price");
        var menu = await FindMenuAsync(request.MenuItemId, ct);
        if (menu.TrangThai == MenuItemStatus.Discontinued)
            throw ServiceHelpers.Conflict("A discontinued menu item cannot receive new toppings.");
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw Validation("Name", "Topping name is required.");
        if (await db.Toppings.AnyAsync(x => x.MaMon == menu.MaMon && x.MaTopping != id && x.TenTopping == name, ct))
            throw FieldConflict("Name", "A menu item cannot contain duplicate topping names.");
        var topping = id is null
            ? new Topping { MaTopping = await ids.ToppingAsync(ct), MaMon = menu.MaMon, Mon = menu }
            : await FindToppingAsync(id, ct);
        if (id is not null && topping.MaMon != menu.MaMon)
            throw new ApiException(400, "A topping cannot be moved between menu items.");
        if (id is not null && topping.TenTopping != name &&
            await db.ChiTietToppings.AnyAsync(x => x.MaTopping == topping.MaTopping, ct))
            throw FieldConflict("Name", "A topping used in an order cannot be renamed.");
        if (!request.IsActive)
            throw new ApiException(400, "Toppings are removed, not deactivated, in the ERD model.");
        if (id is null)
            db.Toppings.Add(topping);
        topping.TenTopping = name;
        topping.GiaBan = request.Price;
        await SaveAsync("ToppingSaved", "Topping", topping.MaTopping, ct);
        return Map(topping);
    }

    public async Task DeleteToppingAsync(string id, CancellationToken ct)
    {
        var topping = await FindToppingAsync(id, ct);
        if (await db.ChiTietToppings.AnyAsync(x => x.MaTopping == id, ct))
            throw ServiceHelpers.Conflict("A topping used in an order cannot be deleted.");
        db.Toppings.Remove(topping);
        await SaveAsync("ToppingDeleted", "Topping", id, ct);
    }

    public async Task<PageResponse<MenuItemResponse>> MenuAsync(
        ListRequest request, string? categoryId, MenuItemStatus? status, bool orderableOnly, CancellationToken ct)
    {
        var query = MenuQuery();
        if (!string.IsNullOrWhiteSpace(categoryId))
            query = query.Where(x => x.MaDanhMuc == categoryId);
        if (status.HasValue)
            query = query.Where(x => x.TrangThai == status);
        if (orderableOnly)
            query = query.Where(x => x.TrangThai == MenuItemStatus.Available);
        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(x => x.TenMon.Contains(request.Search.Trim()));
        var total = await query.CountAsync(ct);
        var offset = ((long)request.Page - 1) * request.PageSize;
        var rows = offset > int.MaxValue ? [] : await query.OrderBy(x => x.TenMon)
            .Skip((int)offset).Take(request.PageSize).ToListAsync(ct);
        return new(rows.Select(Map).ToList(), total, request.Page, request.PageSize);
    }

    public async Task<MenuItemResponse> MenuItemAsync(string id, CancellationToken ct) =>
        Map(await FindMenuAsync(id, ct));

    public async Task<MenuItemResponse> SaveMenuItemAsync(string? id, SaveMenuItemRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var category = await FindCategoryAsync(request.CategoryId, ct);
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw Validation("Name", "Menu item name is required.");
        if (await db.Mons.AnyAsync(x => x.MaMon != id && x.TenMon == name, ct))
            throw FieldConflict("Name", "A menu item with this name already exists.");
        if (!Enum.IsDefined(request.Status))
            throw Validation("Status", "The selected menu status is invalid.");
        ValidatePrice(request.BasePrice, "BasePrice");
        if (!request.IsActive && request.Status != MenuItemStatus.Discontinued)
            throw new ApiException(400, "Use the menu status to discontinue a menu item.");

        var menu = id is null
            ? new Mon { MaMon = await ids.MonAsync(ct), MaDanhMuc = category.MaDanhMuc, DanhMuc = category }
            : await FindMenuAsync(id, ct);
        if (id is not null)
            RequireMenuVersion(menu, request.RowVersion);
        if (id is null)
            db.Mons.Add(menu);
        menu.MaDanhMuc = category.MaDanhMuc;
        menu.DanhMuc = category;
        menu.TenMon = name;
        menu.MoTa = request.Description?.Trim() ?? string.Empty;
        menu.HinhAnh = request.ImageUrl?.Trim() ?? string.Empty;
        menu.TrangThai = request.Status;
        if (id is not null)
            // Advance the aggregate token when only child options changed.
            db.Entry(menu).Property(x => x.TenMon).IsModified = true;

        if (request.Sizes is { Count: > 0 })
            await SaveSizesAsync(menu, request.Sizes, ct);
        else if (id is null && request.BasePrice > 0)
            menu.KichCos.Add(new KichCo
            {
                MaKichCo = await ids.KichCoAsync(ct),
                MaMon = menu.MaMon,
                Mon = menu,
                TenKichCo = "M",
                GiaBan = request.BasePrice
            });
        else if (request.Sizes is not null)
            await SaveSizesAsync(menu, request.Sizes, ct);

        if (request.Toppings is not null)
            await SaveToppingsAsync(menu, request.Toppings, ct);
        if (request.ToppingIds.Count > 0 &&
            request.ToppingIds.Any(toppingId => menu.Toppings.All(x => x.MaTopping != toppingId)))
            throw new ApiException(400, "Each selected topping must belong to this menu item.");
        if (menu.KichCos.Count == 0)
            throw Validation("Sizes", "A menu item must have at least one size with a price.");
        await SaveMenuAsync("MenuItemSaved", menu.MaMon, ct);
        await transaction.CommitAsync(ct);
        return Map(menu);
    }

    public async Task<MenuItemResponse> SetMenuStatusAsync(string id, MenuStatusRequest request, CancellationToken ct)
    {
        if (!ServiceHelpers.CanManageOperations(accessor))
            throw new ApiException(403, "Only Manager can change menu availability.");
        if (!Enum.IsDefined(request.Status))
            throw Validation("Status", "The selected menu status is invalid.");
        var menu = await FindMenuAsync(id, ct);
        RequireMenuVersion(menu, request.RowVersion);
        menu.TrangThai = request.Status;
        await SaveMenuAsync("MenuAvailabilityChanged", id, ct);
        return Map(menu);
    }

    private IQueryable<Mon> MenuQuery() => db.Mons.Include(x => x.DanhMuc)
        .Include(x => x.KichCos).Include(x => x.Toppings).AsSplitQuery();

    private async Task<DanhMuc> FindCategoryAsync(string id, CancellationToken ct) =>
        await db.DanhMucs.SingleOrDefaultAsync(x => x.MaDanhMuc == id, ct)
        ?? throw ServiceHelpers.NotFound("Category");

    private async Task<KichCo> FindSizeAsync(string id, CancellationToken ct) =>
        await db.KichCos.SingleOrDefaultAsync(x => x.MaKichCo == id, ct)
        ?? throw ServiceHelpers.NotFound("Size");

    private async Task<Topping> FindToppingAsync(string id, CancellationToken ct) =>
        await db.Toppings.SingleOrDefaultAsync(x => x.MaTopping == id, ct)
        ?? throw ServiceHelpers.NotFound("Topping");

    private async Task<Mon> FindMenuAsync(string id, CancellationToken ct) =>
        await MenuQuery().SingleOrDefaultAsync(x => x.MaMon == id, ct)
        ?? throw ServiceHelpers.NotFound("Menu item");

    private async Task SaveAsync(string action, string entity, string id, CancellationToken ct)
    {
        ServiceHelpers.Audit(db, ServiceHelpers.Actor(accessor), action, entity, id, action);
        await db.SaveChangesAsync(ct);
    }

    private async Task SaveMenuAsync(string action, string id, CancellationToken ct)
    {
        ServiceHelpers.Audit(db, ServiceHelpers.Actor(accessor), action, "Mon", id, action);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw ServiceHelpers.Conflict("The menu item changed. Reload it before trying again.");
        }
    }

    private void RequireMenuVersion(Mon menu, string? expected)
    {
        if (string.IsNullOrWhiteSpace(expected))
            throw Validation("RowVersion", "RowVersion is required when updating a menu item.");
        var current = db.Entry(menu).Property<byte[]>("RowVersion").CurrentValue
            ?? throw new InvalidOperationException("The menu concurrency token was not loaded.");
        ServiceHelpers.Version(db.Entry(menu), current, expected);
    }

    private async Task SaveSizesAsync(Mon menu, IReadOnlyList<MenuSizeRequest> requested, CancellationToken ct)
    {
        var existing = menu.KichCos.ToList();
        var retainedIds = new HashSet<string>(StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var option in requested)
        {
            ValidatePrice(option.Price, "Sizes.Price");
            KichCo size;
            var sizeId = string.IsNullOrWhiteSpace(option.SizeId) ? null : option.SizeId;
            if (sizeId is null)
            {
                var name = option.Name?.Trim();
                if (string.IsNullOrWhiteSpace(name))
                    throw Validation("Sizes.Name", "Every new size requires a name.");
                if (!names.Add(name))
                    throw FieldConflict("Sizes.Name", "A menu item cannot contain duplicate size names.");
                size = new KichCo
                {
                    MaKichCo = await ids.KichCoAsync(ct),
                    MaMon = menu.MaMon,
                    Mon = menu,
                    TenKichCo = name,
                    GiaBan = option.Price
                };
                menu.KichCos.Add(size);
                retainedIds.Add(size.MaKichCo);
                continue;
            }

            if (!retainedIds.Add(sizeId))
                throw Validation("Sizes", "A menu item cannot contain duplicate size selections.");
            size = existing.SingleOrDefault(x => x.MaKichCo == sizeId)
                ?? throw Validation("Sizes", "Each selected size must belong to this menu item.");
            var updatedName = option.Name?.Trim();
            if (!string.IsNullOrWhiteSpace(updatedName) &&
                !string.Equals(updatedName, size.TenKichCo, StringComparison.Ordinal))
            {
                if (await db.ChiTietDonHangs.AnyAsync(x => x.MaKichCo == size.MaKichCo, ct))
                    throw FieldConflict("Sizes.Name", "A size used in an order cannot be renamed.");
                size.TenKichCo = updatedName;
            }
            if (!names.Add(size.TenKichCo))
                throw FieldConflict("Sizes.Name", "A menu item cannot contain duplicate size names.");
            size.GiaBan = option.Price;
        }

        foreach (var size in existing.Where(x => !retainedIds.Contains(x.MaKichCo)))
        {
            if (await db.ChiTietDonHangs.AnyAsync(x => x.MaKichCo == size.MaKichCo, ct))
                throw FieldConflict("Sizes", "A size used in an order cannot be deleted.");
            db.KichCos.Remove(size);
        }
    }

    private async Task SaveToppingsAsync(Mon menu, IReadOnlyList<MenuToppingRequest> requested, CancellationToken ct)
    {
        var existing = menu.Toppings.ToList();
        var retainedIds = new HashSet<string>(StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var option in requested)
        {
            ValidatePrice(option.Price, "Toppings.Price");
            Topping topping;
            var toppingId = string.IsNullOrWhiteSpace(option.ToppingId) ? null : option.ToppingId;
            if (toppingId is null)
            {
                var name = option.Name?.Trim();
                if (string.IsNullOrWhiteSpace(name))
                    throw Validation("Toppings.Name", "Every new topping requires a name.");
                if (!names.Add(name))
                    throw FieldConflict("Toppings.Name", "A menu item cannot contain duplicate topping names.");
                topping = new Topping
                {
                    MaTopping = await ids.ToppingAsync(ct),
                    MaMon = menu.MaMon,
                    Mon = menu,
                    TenTopping = name,
                    GiaBan = option.Price
                };
                menu.Toppings.Add(topping);
                retainedIds.Add(topping.MaTopping);
                continue;
            }

            if (!retainedIds.Add(toppingId))
                throw Validation("Toppings", "A menu item cannot contain duplicate topping selections.");
            topping = existing.SingleOrDefault(x => x.MaTopping == toppingId)
                ?? throw Validation("Toppings", "Each selected topping must belong to this menu item.");
            var updatedName = option.Name?.Trim();
            if (!string.IsNullOrWhiteSpace(updatedName) &&
                !string.Equals(updatedName, topping.TenTopping, StringComparison.Ordinal))
            {
                if (await db.ChiTietToppings.AnyAsync(x => x.MaTopping == topping.MaTopping, ct))
                    throw FieldConflict("Toppings.Name", "A topping used in an order cannot be renamed.");
                topping.TenTopping = updatedName;
            }
            if (!names.Add(topping.TenTopping))
                throw FieldConflict("Toppings.Name", "A menu item cannot contain duplicate topping names.");
            topping.GiaBan = option.Price;
        }

        foreach (var topping in existing.Where(x => !retainedIds.Contains(x.MaTopping)))
        {
            if (await db.ChiTietToppings.AnyAsync(x => x.MaTopping == topping.MaTopping, ct))
                throw FieldConflict("Toppings", "A topping used in an order cannot be deleted.");
            db.Toppings.Remove(topping);
        }
    }

    private static SizeResponse Map(KichCo x) =>
        new(x.MaKichCo, x.MaMon, x.TenKichCo, x.GiaBan, true);

    private static ToppingResponse Map(Topping x) =>
        new(x.MaTopping, x.MaMon, x.TenTopping, x.GiaBan, true);

    private static ApiException Validation(string field, string message) =>
        new(400, "Validation failed.", new Dictionary<string, string[]> { [field] = [message] });

    private static ApiException FieldConflict(string field, string message) =>
        new(409, message, new Dictionary<string, string[]> { [field] = [message] });

    private static void ValidatePrice(decimal amount, string field)
    {
        if (amount < 0 || amount > 999999999m || decimal.Round(amount, 2) != amount)
            throw Validation(field, "Price must be nonnegative, at most 999999999, with at most two decimal places.");
    }

    private MenuItemResponse Map(Mon x) => new(
        x.MaMon,
        x.MaDanhMuc,
        x.DanhMuc.TenDanhMuc,
        x.TenMon,
        x.MoTa,
        x.HinhAnh,
        x.KichCos.Count == 0 ? 0 : x.KichCos.Min(size => size.GiaBan),
        x.TrangThai,
        x.TrangThai != MenuItemStatus.Discontinued,
        x.KichCos.OrderBy(size => size.GiaBan)
            .Select(size => new MenuSizeResponse(size.MaKichCo, size.TenKichCo, size.GiaBan, true)).ToList(),
        x.Toppings.OrderBy(topping => topping.TenTopping).Select(Map).ToList(),
        Convert.ToBase64String(db.Entry(x).Property<byte[]>("RowVersion").CurrentValue
            ?? throw new InvalidOperationException("The menu concurrency token was not loaded.")));
}
