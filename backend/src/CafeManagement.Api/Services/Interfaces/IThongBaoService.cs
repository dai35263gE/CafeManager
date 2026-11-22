using CafeManagement.Api.Contracts;

namespace CafeManagement.Api.Services;

public interface IThongBaoService
{
    Task<KitchenNoticeResponse> GuiThongBaoThieuHangAsync(ShortageReportRequest request, CancellationToken ct);
    Task<PageResponse<KitchenNoticeResponse>> TimKiemThongBaoAsync(
        ListRequest request, DateTimeOffset? since, CancellationToken ct);
    Task<KitchenNoticeResponse> DanhDauDaDocAsync(string id, CancellationToken ct);
}
