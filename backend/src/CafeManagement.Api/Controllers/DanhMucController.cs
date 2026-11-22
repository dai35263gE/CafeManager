using CafeManagement.Api.Contracts;
using CafeManagement.Api.Security;
using CafeManagement.Api.Services;
using CafeManagement.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeManagement.Api.Controllers;

[Route("api/danh-muc"), Authorize(Roles = AccessRoles.Operations)]
public sealed class DanhMucController(IDanhMucService service) : ApiController
{
    /// <summary>Search menu categories.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PageResponse<CategoryResponse>>>> TimKiem(
        [FromQuery] ListRequest request, bool? active, CancellationToken ct) =>
        Success(await service.TimKiemDanhMucAsync(request, active, ct));

    /// <summary>Get a category.</summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<CategoryResponse>>> Lay(string id, CancellationToken ct) =>
        Success(await service.LayDanhMucAsync(id, ct));

    /// <summary>Create a category.</summary>
    [HttpPost, Authorize(Roles = Roles.Manager)]
    public async Task<ActionResult<ApiResponse<CategoryResponse>>> Them(SaveCategoryRequest request, CancellationToken ct)
    {
        var x = await service.ThemDanhMucAsync(request, ct);
        return CreatedAtAction(nameof(Lay), new { id = x.Id }, new ApiResponse<CategoryResponse>(true, "Category created.", x));
    }

    /// <summary>Update a category.</summary>
    [HttpPut("{id}"), Authorize(Roles = Roles.Manager)]
    public async Task<ActionResult<ApiResponse<CategoryResponse>>> Sua(string id, SaveCategoryRequest request, CancellationToken ct) =>
        Success(await service.SuaDanhMucAsync(id, request, ct));

    /// <summary>Deactivate a category while preserving historical references.</summary>
    [HttpDelete("{id}"), Authorize(Roles = Roles.Manager)]
    public async Task<IActionResult> Ngung(string id, CancellationToken ct)
    {
        await service.NgungDanhMucAsync(id, ct);
        return NoContent();
    }
}
