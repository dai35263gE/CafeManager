using CafeManagement.Api.Contracts;
using CafeManagement.Api.Security;
using CafeManagement.Api.Services;
using CafeManagement.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeManagement.Api.Controllers;

[Route("api/toppings"), Authorize(Roles = AccessRoles.Operations)]
public sealed class ToppingController(IToppingService service) : ApiController
{
    /// <summary>Search topping definitions.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PageResponse<ToppingResponse>>>> TimKiem(
        [FromQuery] ListRequest request, bool? active, string? menuItemId, CancellationToken ct) =>
        Success(await service.TimKiemToppingAsync(request, active, menuItemId, ct));

    /// <summary>Get a topping.</summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<ToppingResponse>>> Lay(string id, CancellationToken ct) =>
        Success(await service.LayToppingAsync(id, ct));

    /// <summary>Create a topping.</summary>
    [HttpPost, Authorize(Roles = Roles.Manager)]
    public async Task<ActionResult<ApiResponse<ToppingResponse>>> Them(SaveToppingRequest request, CancellationToken ct)
    {
        var x = await service.ThemToppingAsync(request, ct);
        return CreatedAtAction(nameof(Lay), new { id = x.Id }, new ApiResponse<ToppingResponse>(true, "Topping created.", x));
    }

    /// <summary>Update a topping without repricing past sales.</summary>
    [HttpPut("{id}"), Authorize(Roles = Roles.Manager)]
    public async Task<ActionResult<ApiResponse<ToppingResponse>>> CapNhat(string id, SaveToppingRequest request, CancellationToken ct) =>
        Success(await service.CapNhatToppingAsync(id, request, ct));

    /// <summary>Deactivate a topping.</summary>
    [HttpDelete("{id}"), Authorize(Roles = Roles.Manager)]
    public async Task<IActionResult> Xoa(string id, CancellationToken ct)
    {
        await service.XoaToppingAsync(id, ct);
        return NoContent();
    }
}
