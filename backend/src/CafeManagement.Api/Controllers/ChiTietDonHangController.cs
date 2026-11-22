using CafeManagement.Api.Contracts;
using CafeManagement.Api.Security;
using CafeManagement.Api.Services;
using CafeManagement.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeManagement.Api.Controllers;

[Route("api/don-hang"), Authorize(Roles = AccessRoles.Operations)]
public sealed class ChiTietDonHangController(IChiTietDonHangService service) : ApiController
{
    [HttpPost("{id}/items"), Authorize(Roles = AccessRoles.Floor)]
    public async Task<ActionResult<ApiResponse<OrderResponse>>> ThemMon(
        string id, AddOrderItemsRequest request, CancellationToken ct) =>
        Success(await service.ThemMonAsync(id, request, ct));

    [HttpPatch("{id}/items/{itemId}"), Authorize(Roles = AccessRoles.Floor)]
    public async Task<ActionResult<ApiResponse<OrderResponse>>> CapNhatChiTietMon(
        string id, string itemId, UpdateOrderItemRequest request, CancellationToken ct) =>
        Success(await service.CapNhatChiTietMonAsync(id, itemId, request, ct));

    [HttpDelete("{id}/items/{itemId}"), Authorize(Roles = AccessRoles.Floor)]
    public async Task<IActionResult> HuyMon(
        string id, string itemId, [FromBody] CancelItemRequest request, CancellationToken ct)
    {
        await service.HuyMonAsync(id, itemId, request, ct);
        return NoContent();
    }

    [HttpPatch("{id}/items/{itemId}/status"), Authorize(Roles = AccessRoles.Floor + ",Barista")]
    public async Task<ActionResult<ApiResponse<OrderResponse>>> CapNhatTrangThaiMon(
        string id, string itemId, ItemStatusRequest request, CancellationToken ct) =>
        Success(await service.CapNhatTrangThaiMonAsync(id, itemId, request, ct));

    [HttpPatch("{id}/items/{itemId}/reopen"), Authorize(Roles = AccessRoles.Manager)]
    public async Task<ActionResult<ApiResponse<OrderResponse>>> MoLaiMon(
        string id, string itemId, ReopenOrderItemRequest request, CancellationToken ct) =>
        Success(await service.MoLaiMonAsync(id, itemId, request, ct));

    [HttpGet("kitchen-queue"), Authorize(Roles = AccessRoles.Floor + ",Barista")]
    public async Task<ActionResult<ApiResponse<PageResponse<KitchenItemResponse>>>> XemHangDoiBep(
        [FromQuery] ListRequest request, OrderItemStatus? status, CancellationToken ct) =>
        Success(await service.XemHangDoiBepAsync(request, status, ct));
}
