using CafeManagement.Api.Contracts;
using CafeManagement.Domain.Enums;

namespace CafeManagement.Api.Services;

internal sealed class MonService(ICatalogOperations operations) : IMonService
{
    public Task<PageResponse<MenuItemResponse>> TimKiemMonAsync(
        ListRequest request, string? categoryId, MenuItemStatus? status, bool orderableOnly, CancellationToken ct) =>
        operations.MenuAsync(request, categoryId, status, orderableOnly, ct);

    public Task<MenuItemResponse> LayMonAsync(string id, CancellationToken ct) =>
        operations.MenuItemAsync(id, ct);

    public Task<MenuItemResponse> ThemMonAsync(SaveMenuItemRequest request, CancellationToken ct) =>
        operations.SaveMenuItemAsync(null, request, ct);

    public Task<MenuItemResponse> SuaThongTinMonAsync(string id, SaveMenuItemRequest request, CancellationToken ct) =>
        operations.SaveMenuItemAsync(id, request, ct);

    public Task<MenuItemResponse> CapNhatTrangThaiKinhDoanhAsync(
        string id, MenuStatusRequest request, CancellationToken ct) =>
        operations.SetMenuStatusAsync(id, request, ct);
}
