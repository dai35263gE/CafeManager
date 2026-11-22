using CafeManagement.Api.Contracts;

namespace CafeManagement.Api.Services;

public sealed class ThongBaoService(IDonHangService orders) : IThongBaoService
{
    public Task<KitchenNoticeResponse> GuiThongBaoThieuHangAsync(
        ShortageReportRequest request, CancellationToken ct) =>
        orders.BaoThieuAsync(request, ct);

    public Task<PageResponse<KitchenNoticeResponse>> TimKiemThongBaoAsync(
        ListRequest request, DateTimeOffset? since, CancellationToken ct) =>
        orders.TimKiemThongBaoAsync(request, since, ct);

    public Task<KitchenNoticeResponse> DanhDauDaDocAsync(string id, CancellationToken ct) =>
        orders.DanhDauThongBaoDaDocAsync(id, ct);
}
