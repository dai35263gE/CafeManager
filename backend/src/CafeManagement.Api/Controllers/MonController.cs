using System.ComponentModel.DataAnnotations;
using CafeManagement.Api.Contracts;
using CafeManagement.Api.Security;
using CafeManagement.Api.Services;
using CafeManagement.Domain.Common;
using CafeManagement.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeManagement.Api.Controllers;

[Route("api/mon"), Authorize(Roles = AccessRoles.Operations)]
public sealed class MonController(IMonService service) : ApiController
{
    /// <summary>Search menu; ordering clients use orderableOnly=true.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PageResponse<MenuItemResponse>>>> TimKiem(
        [FromQuery] ListRequest request, string? categoryId, MenuItemStatus? status, CancellationToken ct,
        bool orderableOnly = true) =>
        Success(await service.TimKiemMonAsync(request, categoryId, status, orderableOnly, ct));

    /// <summary>Get menu item and permitted sizes/toppings.</summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<MenuItemResponse>>> Lay(string id, CancellationToken ct) =>
        Success(await service.LayMonAsync(id, ct));

    /// <summary>Create menu item with priced sizes and permitted toppings.</summary>
    [HttpPost, Authorize(Roles = Roles.Manager)]
    public async Task<ActionResult<ApiResponse<MenuItemResponse>>> Them(SaveMenuItemRequest request, CancellationToken ct)
    {
        var x = await service.ThemMonAsync(request, ct);
        return CreatedAtAction(nameof(Lay), new { id = x.Id }, new ApiResponse<MenuItemResponse>(true, "Menu item created.", x));
    }

    /// <summary>Replace editable menu fields and permitted options.</summary>
    [HttpPut("{id}"), Authorize(Roles = Roles.Manager)]
    public async Task<ActionResult<ApiResponse<MenuItemResponse>>> SuaThongTinMon(string id, SaveMenuItemRequest request, CancellationToken ct) =>
        Success(await service.SuaThongTinMonAsync(id, request, ct));

    /// <summary>Change temporary availability; only Manager can discontinue or restore.</summary>
    [HttpPatch("{id}/status"), Authorize(Roles = Roles.Manager)]
    public async Task<ActionResult<ApiResponse<MenuItemResponse>>> CapNhatTrangThaiKinhDoanh(
        string id, MenuStatusRequest request, CancellationToken ct) =>
        Success(await service.CapNhatTrangThaiKinhDoanhAsync(id, request, ct));

    /// <summary>Discontinue rather than physically deleting a menu item with its current rowVersion.</summary>
    [HttpDelete("{id}"), Authorize(Roles = Roles.Manager)]
    public async Task<IActionResult> NgungKinhDoanh(string id, [FromQuery, Required] string rowVersion, CancellationToken ct)
    {
        await service.CapNhatTrangThaiKinhDoanhAsync(id, new MenuStatusRequest
        {
            Status = MenuItemStatus.Discontinued,
            RowVersion = rowVersion
        }, ct);
        return NoContent();
    }
}
