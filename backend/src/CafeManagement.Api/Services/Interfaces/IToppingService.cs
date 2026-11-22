using CafeManagement.Api.Contracts;

namespace CafeManagement.Api.Services;

public interface IToppingService
{
    Task<PageResponse<ToppingResponse>> TimKiemToppingAsync(
        ListRequest request, bool? active, string? menuItemId, CancellationToken ct);
    Task<ToppingResponse> LayToppingAsync(string id, CancellationToken ct);
    Task<ToppingResponse> ThemToppingAsync(SaveToppingRequest request, CancellationToken ct);
    Task<ToppingResponse> CapNhatToppingAsync(string id, SaveToppingRequest request, CancellationToken ct);
    Task XoaToppingAsync(string id, CancellationToken ct);
}
