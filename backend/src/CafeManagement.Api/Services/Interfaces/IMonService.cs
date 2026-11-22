using CafeManagement.Api.Contracts;
using CafeManagement.Domain.Enums;

namespace CafeManagement.Api.Services;

public interface IMonService
{
    Task<PageResponse<MenuItemResponse>> TimKiemMonAsync(
        ListRequest request, string? categoryId, MenuItemStatus? status, bool orderableOnly, CancellationToken ct);
    Task<MenuItemResponse> LayMonAsync(string id, CancellationToken ct);
    Task<MenuItemResponse> ThemMonAsync(SaveMenuItemRequest request, CancellationToken ct);
    Task<MenuItemResponse> SuaThongTinMonAsync(string id, SaveMenuItemRequest request, CancellationToken ct);
    Task<MenuItemResponse> CapNhatTrangThaiKinhDoanhAsync(string id, MenuStatusRequest request, CancellationToken ct);
}
