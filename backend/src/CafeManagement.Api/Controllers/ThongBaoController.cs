using CafeManagement.Api.Contracts;
using CafeManagement.Api.Security;
using CafeManagement.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeManagement.Api.Controllers;

[Route("api/don-hang")]
public sealed class ThongBaoController(IThongBaoService service) : ApiController
{
    [HttpPost("shortage-reports"), Authorize(Roles = AccessRoles.Kitchen)]
    public async Task<ActionResult<ApiResponse<KitchenNoticeResponse>>> GuiThongBaoThieuHang(
        ShortageReportRequest request, CancellationToken ct)
    {
        var notice = await service.GuiThongBaoThieuHangAsync(request, ct);
        return Created("/api/don-hang/kitchen-notices",
            new ApiResponse<KitchenNoticeResponse>(true, "Shortage reported.", notice));
    }

    [HttpGet("kitchen-notices"), Authorize(Roles = AccessRoles.Operations)]
    public async Task<ActionResult<ApiResponse<PageResponse<KitchenNoticeResponse>>>> TimKiem(
        [FromQuery] ListRequest request, DateTimeOffset? since, CancellationToken ct) =>
        Success(await service.TimKiemThongBaoAsync(request, since, ct));

    [HttpPatch("kitchen-notices/{id}/read"), Authorize(Roles = AccessRoles.Operations)]
    public async Task<ActionResult<ApiResponse<KitchenNoticeResponse>>> DanhDauDaDoc(
        string id, CancellationToken ct) =>
        Success(await service.DanhDauDaDocAsync(id, ct));
}
