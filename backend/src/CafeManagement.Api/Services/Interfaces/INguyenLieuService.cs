using CafeManagement.Api.Contracts;

namespace CafeManagement.Api.Services;

public interface INguyenLieuService
{
    Task<PageResponse<IngredientResponse>> TimKiemNguyenLieuAsync(ListRequest request, bool warningsOnly, CancellationToken ct);
    Task<IngredientResponse> LayNguyenLieuAsync(string id, CancellationToken ct);
    Task<IngredientResponse> ThemNguyenLieuAsync(SaveIngredientRequest request, CancellationToken ct);
    Task<IngredientResponse> CapNhatNguyenLieuAsync(string id, SaveIngredientRequest request, CancellationToken ct);
    Task XoaNguyenLieuAsync(string id, string rowVersion, CancellationToken ct);
}
