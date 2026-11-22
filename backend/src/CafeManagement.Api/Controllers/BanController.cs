using CafeManagement.Api.Contracts;
using CafeManagement.Api.Security;
using CafeManagement.Api.Services;
using CafeManagement.Domain.Common;
using CafeManagement.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeManagement.Api.Controllers;

[Route("api/ban"), Authorize(Roles = AccessRoles.Floor)]
public sealed class BanController(IBanService tables, IDonHangService orders) : ApiController
{
    /// <summary>Search and filter floor tables.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PageResponse<TableResponse>>>> XemSoDoBan(
        [FromQuery] ListRequest request, TableStatus? status, string? area, CancellationToken ct) =>
        Success(await tables.XemSoDoBanAsync(request, status, area, ct));

    /// <summary>Get a table and current concurrency token.</summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<TableResponse>>> Lay(string id, CancellationToken ct) =>
        Success(await tables.LayBanAsync(id, ct));

    /// <summary>Create an available table.</summary>
    [HttpPost, Authorize(Roles = Roles.Manager)]
    public async Task<ActionResult<ApiResponse<TableResponse>>> Them(SaveTableRequest request, CancellationToken ct)
    {
        var x = await tables.ThemBanAsync(request, ct);
        return CreatedAtAction(nameof(Lay), new { id = x.Id }, new ApiResponse<TableResponse>(true, "Table created.", x));
    }

    /// <summary>Update table code, area and note.</summary>
    [HttpPut("{id}"), Authorize(Roles = Roles.Manager)]
    public async Task<ActionResult<ApiResponse<TableResponse>>> SuaThongTin(string id, SaveTableRequest request, CancellationToken ct) =>
        Success(await tables.SuaThongTinAsync(id, request, ct));

    /// <summary>Seat a guest without creating an order.</summary>
    [HttpPost("{id}/seat"), Authorize(Roles = AccessRoles.Floor)]
    public async Task<ActionResult<ApiResponse<TableResponse>>> XepBan(
        string id, SeatTableRequest request, CancellationToken ct) =>
        Success(await tables.XepBanAsync(id, request, ct));

    /// <summary>Mark cleaning/availability/disabled; occupancy is managed by orders.</summary>
    [HttpPatch("{id}/status"), Authorize(Roles = AccessRoles.Floor)]
    public async Task<ActionResult<ApiResponse<TableResponse>>> CapNhatTrangThaiVeSinh(
        string id, TableStatusRequest request, CancellationToken ct) =>
        Success(await tables.CapNhatTrangThaiBanAsync(id, request, ct));

    /// <summary>Soft-delete an unoccupied table using its token.</summary>
    [HttpDelete("{id}"), Authorize(Roles = Roles.Manager)]
    public async Task<IActionResult> NgungSuDung(string id, [FromQuery] string rowVersion, CancellationToken ct)
    {
        await tables.NgungSuDungAsync(id, rowVersion, ct);
        return NoContent();
    }

    /// <summary>Transfer a table's active order atomically.</summary>
    [HttpPost("transfer"), Authorize(Roles = AccessRoles.Floor)]
    public async Task<ActionResult<ApiResponse<OrderResponse>>> ChuyenBan(TransferTableRequest request, CancellationToken ct) =>
        Success(await orders.ChuyenBanTheoBanAsync(request, ct));

    /// <summary>Merge multiple tables and their active dine-in orders into a selected main table atomically.</summary>
    [HttpPost("merge"), Authorize(Roles = AccessRoles.Floor)]
    public async Task<ActionResult<ApiResponse<MergeTablesResponse>>> GhepBan(MergeTableRequest request, CancellationToken ct) =>
        Success(await orders.GhepDonTheoBanAsync(request, ct));
}
