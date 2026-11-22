using CafeManagement.Api.Contracts;
using CafeManagement.Domain.Enums;

namespace CafeManagement.Api.Services;

public sealed class ChiTietDonHangService(IDonHangService orders) : IChiTietDonHangService
{
    public Task<OrderResponse> ThemMonAsync(string orderId, AddOrderItemsRequest request, CancellationToken ct) =>
        orders.ThemMonAsync(orderId, request, ct);

    public Task<OrderResponse> CapNhatChiTietMonAsync(
        string orderId, string itemId, UpdateOrderItemRequest request, CancellationToken ct) =>
        orders.CapNhatChiTietMonAsync(orderId, itemId, request, ct);

    public Task<OrderResponse> HuyMonAsync(
        string orderId, string itemId, CancelItemRequest request, CancellationToken ct) =>
        orders.HuyMonAsync(orderId, itemId, request, ct);

    public Task<OrderResponse> CapNhatTrangThaiMonAsync(
        string orderId, string itemId, ItemStatusRequest request, CancellationToken ct) =>
        orders.CapNhatTrangThaiMonAsync(orderId, itemId, request, ct);

    public Task<OrderResponse> MoLaiMonAsync(
        string orderId, string itemId, ReopenOrderItemRequest request, CancellationToken ct) =>
        orders.MoLaiMonAsync(orderId, itemId, request, ct);

    public Task<PageResponse<KitchenItemResponse>> XemHangDoiBepAsync(
        ListRequest request, OrderItemStatus? status, CancellationToken ct) =>
        orders.XemHangDoiBepAsync(request, status, ct);
}
