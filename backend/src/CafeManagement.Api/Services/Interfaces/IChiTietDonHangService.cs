using CafeManagement.Api.Contracts;
using CafeManagement.Domain.Enums;

namespace CafeManagement.Api.Services;

public interface IChiTietDonHangService
{
    Task<OrderResponse> ThemMonAsync(string orderId, AddOrderItemsRequest request, CancellationToken ct);
    Task<OrderResponse> CapNhatChiTietMonAsync(string orderId, string itemId, UpdateOrderItemRequest request, CancellationToken ct);
    Task<OrderResponse> HuyMonAsync(string orderId, string itemId, CancelItemRequest request, CancellationToken ct);
    Task<OrderResponse> CapNhatTrangThaiMonAsync(string orderId, string itemId, ItemStatusRequest request, CancellationToken ct);
    Task<OrderResponse> MoLaiMonAsync(string orderId, string itemId, ReopenOrderItemRequest request, CancellationToken ct);
    Task<PageResponse<KitchenItemResponse>> XemHangDoiBepAsync(ListRequest request, OrderItemStatus? status, CancellationToken ct);
}
