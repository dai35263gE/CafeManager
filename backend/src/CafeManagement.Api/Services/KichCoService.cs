using CafeManagement.Api.Contracts;

namespace CafeManagement.Api.Services;

internal sealed class KichCoService(ICatalogOperations operations) : IKichCoService
{
    public Task<PageResponse<SizeResponse>> TimKiemKichCoAsync(
        ListRequest request, bool? active, string? menuItemId, CancellationToken ct) =>
        operations.SizesAsync(request, active, menuItemId, ct);

    public Task<SizeResponse> LayKichCoAsync(string id, CancellationToken ct) =>
        operations.SizeAsync(id, ct);

    public Task<SizeResponse> ThemKichCoAsync(SaveSizeRequest request, CancellationToken ct) =>
        operations.SaveSizeAsync(null, request, ct);

    public Task<SizeResponse> CapNhatKichCoAsync(string id, SaveSizeRequest request, CancellationToken ct) =>
        operations.SaveSizeAsync(id, request, ct);

    public Task XoaKichCoAsync(string id, CancellationToken ct) =>
        operations.DeleteSizeAsync(id, ct);
}
