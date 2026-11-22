using CafeManagement.Api.Contracts;

namespace CafeManagement.Api.Services;

public interface IKichCoService
{
    Task<PageResponse<SizeResponse>> TimKiemKichCoAsync(
        ListRequest request, bool? active, string? menuItemId, CancellationToken ct);
    Task<SizeResponse> LayKichCoAsync(string id, CancellationToken ct);
    Task<SizeResponse> ThemKichCoAsync(SaveSizeRequest request, CancellationToken ct);
    Task<SizeResponse> CapNhatKichCoAsync(string id, SaveSizeRequest request, CancellationToken ct);
    Task XoaKichCoAsync(string id, CancellationToken ct);
}
