using CafeManagement.Api.Contracts;
using CafeManagement.Domain.Enums;

namespace CafeManagement.Api.Services;

public interface IDonHangService
{
    Task<PageResponse<OrderResponse>> TimKiemDonHangAsync(ListRequest request, OrderStatus? status, string? tableId, CancellationToken ct);
    Task<OrderResponse> LayDonHangAsync(string id, CancellationToken ct);
    Task<OrderResponse> TaoDonHangAsync(CreateOrderRequest request, CancellationToken ct);
    Task<OrderResponse> ThemMonAsync(string id, AddOrderItemsRequest request, CancellationToken ct);
    Task<OrderResponse> CapNhatChiTietMonAsync(string id, string itemId, UpdateOrderItemRequest request, CancellationToken ct);
    Task<OrderResponse> HuyMonAsync(string id, string itemId, CancelItemRequest request, CancellationToken ct);
    Task<OrderResponse> CapNhatTrangThaiDonHangAsync(string id, OrderStatusRequest request, CancellationToken ct);
    Task<OrderResponse> CapNhatTrangThaiMonAsync(string id, string itemId, ItemStatusRequest request, CancellationToken ct);
    Task<OrderResponse> MoLaiMonAsync(string id, string itemId, ReopenOrderItemRequest request, CancellationToken ct);
    Task<OrderResponse> ChuyenBanAsync(string id, TransferOrderRequest request, CancellationToken ct);
    Task<OrderResponse> ChuyenBanTheoBanAsync(TransferTableRequest request, CancellationToken ct);
    Task<OrderResponse> GhepDonAsync(string id, MergeOrderRequest request, CancellationToken ct);
    Task<MergeTablesResponse> GhepDonTheoBanAsync(MergeTableRequest request, CancellationToken ct);
    Task<PageResponse<KitchenItemResponse>> XemHangDoiBepAsync(ListRequest request, OrderItemStatus? status, CancellationToken ct);
    Task<KitchenNoticeResponse> BaoThieuAsync(ShortageReportRequest request, CancellationToken ct);
    Task<PageResponse<KitchenNoticeResponse>> TimKiemThongBaoAsync(ListRequest request, DateTimeOffset? since, CancellationToken ct);
    Task<KitchenNoticeResponse> DanhDauThongBaoDaDocAsync(string id, CancellationToken ct);
}
