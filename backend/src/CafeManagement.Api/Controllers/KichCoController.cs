using CafeManagement.Api.Contracts;
using CafeManagement.Api.Security;
using CafeManagement.Api.Services;
using CafeManagement.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeManagement.Api.Controllers;

[Route("api/sizes"), Authorize(Roles = AccessRoles.Operations)]
public sealed class KichCoController(IKichCoService service) : ApiController
{
    /// <summary>Search reusable sizes.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PageResponse<SizeResponse>>>> TimKiem(
        [FromQuery] ListRequest request, bool? active, string? menuItemId, CancellationToken ct) =>
        Success(await service.TimKiemKichCoAsync(request, active, menuItemId, ct));

    /// <summary>Get a reusable size.</summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<SizeResponse>>> Lay(string id, CancellationToken ct) =>
        Success(await service.LayKichCoAsync(id, ct));

    /// <summary>Create a reusable size.</summary>
    [HttpPost, Authorize(Roles = Roles.Manager)]
    public async Task<ActionResult<ApiResponse<SizeResponse>>> Them(SaveSizeRequest request, CancellationToken ct)
    {
        var x = await service.ThemKichCoAsync(request, ct);
        return CreatedAtAction(nameof(Lay), new { id = x.Id }, new ApiResponse<SizeResponse>(true, "Size created.", x));
    }

    /// <summary>Update size metadata without repricing past sales.</summary>
    [HttpPut("{id}"), Authorize(Roles = Roles.Manager)]
    public async Task<ActionResult<ApiResponse<SizeResponse>>> CapNhat(string id, SaveSizeRequest request, CancellationToken ct) =>
        Success(await service.CapNhatKichCoAsync(id, request, ct));

    /// <summary>Deactivate a size.</summary>
    [HttpDelete("{id}"), Authorize(Roles = Roles.Manager)]
    public async Task<IActionResult> Xoa(string id, CancellationToken ct)
    {
        await service.XoaKichCoAsync(id, ct);
        return NoContent();
    }
}
