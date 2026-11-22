using CafeManagement.Api.Contracts;

namespace CafeManagement.Api.Services;

public interface IDanhMucService
{
    Task<PageResponse<CategoryResponse>> TimKiemDanhMucAsync(ListRequest request, bool? active, CancellationToken ct);
    Task<CategoryResponse> LayDanhMucAsync(string id, CancellationToken ct);
    Task<CategoryResponse> ThemDanhMucAsync(SaveCategoryRequest request, CancellationToken ct);
    Task<CategoryResponse> SuaDanhMucAsync(string id, SaveCategoryRequest request, CancellationToken ct);
    Task NgungDanhMucAsync(string id, CancellationToken ct);
}
