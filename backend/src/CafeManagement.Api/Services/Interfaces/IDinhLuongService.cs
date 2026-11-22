using CafeManagement.Api.Contracts;

namespace CafeManagement.Api.Services;

public interface IDinhLuongService
{
    Task<PageResponse<RecipeResponse>> TimKiemDinhLuongAsync(ListRequest request, string? menuItemId, CancellationToken ct);
    Task<RecipeResponse> LayDinhLuongAsync(string id, CancellationToken ct);
    Task<RecipeResponse> CapNhatDinhLuongAsync(string? id, SaveRecipeRequest request, CancellationToken ct);
    Task XoaDinhLuongAsync(string id, CancellationToken ct);
}
