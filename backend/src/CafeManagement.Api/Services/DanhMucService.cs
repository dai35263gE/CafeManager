using CafeManagement.Api.Contracts;

namespace CafeManagement.Api.Services;

internal sealed class DanhMucService(ICatalogOperations operations) : IDanhMucService
{
    public Task<PageResponse<CategoryResponse>> TimKiemDanhMucAsync(
        ListRequest request, bool? active, CancellationToken ct) =>
        operations.CategoriesAsync(request, active, ct);

    public Task<CategoryResponse> LayDanhMucAsync(string id, CancellationToken ct) =>
        operations.CategoryAsync(id, ct);

    public Task<CategoryResponse> ThemDanhMucAsync(SaveCategoryRequest request, CancellationToken ct) =>
        operations.SaveCategoryAsync(null, request, ct);

    public Task<CategoryResponse> SuaDanhMucAsync(string id, SaveCategoryRequest request, CancellationToken ct) =>
        operations.SaveCategoryAsync(id, request, ct);

    public Task NgungDanhMucAsync(string id, CancellationToken ct) =>
        operations.DeleteCategoryAsync(id, ct);
}
