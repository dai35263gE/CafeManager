using CafeManagement.Api.Contracts;
using CafeManagement.Api.Services;
using CafeManagement.Domain.Common;
using CafeManagement.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeManagement.Api.Controllers;

[Route("api/nguyen-lieu"), Authorize(Roles = Roles.Manager + "," + Roles.Warehouse + "," + Roles.Barista)]
public sealed class BienDongKhoController(IBienDongKhoService service) : ApiController
{
    [HttpGet("{id}/history"), Authorize(Roles = Roles.Manager + "," + Roles.Warehouse)]
    public async Task<ActionResult<ApiResponse<PageResponse<InventoryTransactionResponse>>>> TimKiem(
        string id, [FromQuery] ListRequest request, CancellationToken ct) =>
        Success(await service.TimKiemBienDongAsync(id, request, ct));

    [HttpGet("{id}/history/{transactionId}"), Authorize(Roles = Roles.Manager + "," + Roles.Warehouse)]
    public async Task<ActionResult<ApiResponse<InventoryTransactionResponse>>> Lay(
        string id, string transactionId, CancellationToken ct) =>
        Success(await service.LayBienDongAsync(id, transactionId, ct));

    [HttpPost("{id}/import"), Authorize(Roles = Roles.Manager + "," + Roles.Warehouse)]
    public Task<ActionResult<ApiResponse<InventoryMovementResponse>>> Nhap(
        string id, InventoryMovementRequest request, CancellationToken ct) =>
        Lap(id, InventoryTransactionType.Import, request, ct);

    [HttpPost("{id}/export"), Authorize(Roles = Roles.Manager + "," + Roles.Warehouse)]
    public Task<ActionResult<ApiResponse<InventoryMovementResponse>>> Xuat(
        string id, InventoryMovementRequest request, CancellationToken ct) =>
        Lap(id, InventoryTransactionType.Export, request, ct);

    [HttpPost("{id}/adjust"), Authorize(Roles = Roles.Manager + "," + Roles.Warehouse)]
    public Task<ActionResult<ApiResponse<InventoryMovementResponse>>> DieuChinh(
        string id, InventoryMovementRequest request, CancellationToken ct) =>
        Lap(id, InventoryTransactionType.Adjust, request, ct);

    [HttpPost("{id}/stocktake"), Authorize(Roles = Roles.Manager + "," + Roles.Warehouse)]
    public Task<ActionResult<ApiResponse<InventoryMovementResponse>>> KiemKe(
        string id, InventoryMovementRequest request, CancellationToken ct) =>
        Lap(id, InventoryTransactionType.Stocktake, request, ct);

    private async Task<ActionResult<ApiResponse<InventoryMovementResponse>>> Lap(
        string id, InventoryTransactionType type, InventoryMovementRequest request, CancellationToken ct)
    {
        var movement = await service.LapBienDongAsync(id, type, request, ct);
        return CreatedAtAction(nameof(Lay), new { id, transactionId = movement.Transaction.Id },
            new ApiResponse<InventoryMovementResponse>(true, "Inventory movement recorded.", movement));
    }
}
