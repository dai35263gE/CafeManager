using CafeManagement.Api.Contracts;
using CafeManagement.Api.Security;
using CafeManagement.Api.Services;
using CafeManagement.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeManagement.Api.Controllers;

[Route("api/don-hang"), Authorize(Roles = AccessRoles.Operations)]
public sealed class DonHangController(IDonHangService service) : ApiController
{
    /// <summary>Search orders by code, status and table.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PageResponse<OrderResponse>>>> TimKiem(
        [FromQuery] ListRequest request, OrderStatus? status, string? tableId, CancellationToken ct) =>
        Success(await service.TimKiemDonHangAsync(request, status, tableId, ct));

    /// <summary>Get an order with snapshots, line states and concurrency tokens.</summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<OrderResponse>>> Lay(string id, CancellationToken ct) =>
        Success(await service.LayDonHangAsync(id, ct));

    /// <summary>Create a draft order and occupy its table in one transaction.</summary>
    [HttpPost, Authorize(Roles = AccessRoles.Floor)]
    public async Task<ActionResult<ApiResponse<OrderResponse>>> TaoDon(CreateOrderRequest request, CancellationToken ct)
    {
        var x = await service.TaoDonHangAsync(request, ct);
        return CreatedAtAction(nameof(Lay), new { id = x.Id }, new ApiResponse<OrderResponse>(true, "Order created.", x));
    }

    /// <summary>Confirm or cancel an order; payment must use the Phase 3 payment service.</summary>
    [HttpPatch("{id}/status"), Authorize(Roles = AccessRoles.Floor)]
    public async Task<ActionResult<ApiResponse<OrderResponse>>> CapNhatTrangThaiDonHang(
        string id, OrderStatusRequest request, CancellationToken ct) =>
        Success(await service.CapNhatTrangThaiDonHangAsync(id, request, ct));

    /// <summary>Cancel the order with rowVersion and reason in the request body.</summary>
    [HttpDelete("{id}"), Authorize(Roles = AccessRoles.Floor)]
    public async Task<IActionResult> HuyDon(string id, [FromBody] CancelItemRequest request, CancellationToken ct)
    {
        await service.CapNhatTrangThaiDonHangAsync(id, new OrderStatusRequest
        {
            Status = OrderStatus.Cancelled, RowVersion = request.RowVersion, Reason = request.Reason
        }, ct);
        return NoContent();
    }

    /// <summary>Move an active dine-in order to an available table.</summary>
    [HttpPost("{id}/transfer"), Authorize(Roles = AccessRoles.Floor)]
    public async Task<ActionResult<ApiResponse<OrderResponse>>> ChuyenBan(string id, TransferOrderRequest request, CancellationToken ct) =>
        Success(await service.ChuyenBanAsync(id, request, ct));

    /// <summary>Merge two drafts or two confirmed dine-in orders, retaining line snapshots.</summary>
    [HttpPost("{id}/merge"), Authorize(Roles = AccessRoles.Floor)]
    public async Task<ActionResult<ApiResponse<OrderResponse>>> GhepDon(string id, MergeOrderRequest request, CancellationToken ct) =>
        Success(await service.GhepDonAsync(id, request, ct));

}
