using CafeManagement.Api.Contracts;

namespace CafeManagement.Api.Services;

internal sealed class ToppingService(ICatalogOperations operations) : IToppingService
{
    public Task<PageResponse<ToppingResponse>> TimKiemToppingAsync(
        ListRequest request, bool? active, string? menuItemId, CancellationToken ct) =>
        operations.ToppingsAsync(request, active, menuItemId, ct);

    public Task<ToppingResponse> LayToppingAsync(string id, CancellationToken ct) =>
        operations.ToppingAsync(id, ct);

    public Task<ToppingResponse> ThemToppingAsync(SaveToppingRequest request, CancellationToken ct) =>
        operations.SaveToppingAsync(null, request, ct);

    public Task<ToppingResponse> CapNhatToppingAsync(string id, SaveToppingRequest request, CancellationToken ct) =>
        operations.SaveToppingAsync(id, request, ct);

    public Task XoaToppingAsync(string id, CancellationToken ct) =>
        operations.DeleteToppingAsync(id, ct);
}
