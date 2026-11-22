using CafeManagement.Api.Contracts;
using CafeManagement.Domain.Enums;

namespace CafeManagement.Api.Services;

internal interface ICatalogOperations
{
    Task<PageResponse<CategoryResponse>> CategoriesAsync(ListRequest request, bool? active, CancellationToken ct);
    Task<CategoryResponse> CategoryAsync(string id, CancellationToken ct);
    Task<CategoryResponse> SaveCategoryAsync(string? id, SaveCategoryRequest request, CancellationToken ct);
    Task DeleteCategoryAsync(string id, CancellationToken ct);
    Task<PageResponse<SizeResponse>> SizesAsync(ListRequest request, bool? active, string? menuItemId, CancellationToken ct);
    Task<SizeResponse> SizeAsync(string id, CancellationToken ct);
    Task<SizeResponse> SaveSizeAsync(string? id, SaveSizeRequest request, CancellationToken ct);
    Task DeleteSizeAsync(string id, CancellationToken ct);
    Task<PageResponse<ToppingResponse>> ToppingsAsync(ListRequest request, bool? active, string? menuItemId, CancellationToken ct);
    Task<ToppingResponse> ToppingAsync(string id, CancellationToken ct);
    Task<ToppingResponse> SaveToppingAsync(string? id, SaveToppingRequest request, CancellationToken ct);
    Task DeleteToppingAsync(string id, CancellationToken ct);
    Task<PageResponse<MenuItemResponse>> MenuAsync(ListRequest request, string? categoryId, MenuItemStatus? status,
        bool orderableOnly, CancellationToken ct);
    Task<MenuItemResponse> MenuItemAsync(string id, CancellationToken ct);
    Task<MenuItemResponse> SaveMenuItemAsync(string? id, SaveMenuItemRequest request, CancellationToken ct);
    Task<MenuItemResponse> SetMenuStatusAsync(string id, MenuStatusRequest request, CancellationToken ct);
}
