using CafeManagement.Api.Contracts;
using CafeManagement.Api.Services;
using CafeManagement.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeManagement.Api.Controllers;

[Route("api/nguyen-lieu"), Authorize(Roles = Roles.Manager + "," + Roles.Warehouse + "," + Roles.Barista)]
public sealed class NguyenLieuController(INguyenLieuService service) : ApiController
{
    /// <summary>Search ingredients and stock balances; metadata changes never replace stock.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PageResponse<IngredientResponse>>>> TimKiem(
        [FromQuery] ListRequest request, CancellationToken ct) =>
        Success(await service.TimKiemNguyenLieuAsync(request, false, ct));

    /// <summary>Get ingredient metadata, balance, warnings and concurrency token.</summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<IngredientResponse>>> Lay(string id, CancellationToken ct) =>
        Success(await service.LayNguyenLieuAsync(id, ct));

    /// <summary>Create an ingredient with zero opening stock; use import to record opening balance.</summary>
    [HttpPost, Authorize(Roles = Roles.Manager + "," + Roles.Warehouse)]
    public async Task<ActionResult<ApiResponse<IngredientResponse>>> Them(SaveIngredientRequest request, CancellationToken ct)
    {
        var x = await service.ThemNguyenLieuAsync(request, ct);
        return CreatedAtAction(nameof(Lay), new { id = x.Id }, new ApiResponse<IngredientResponse>(true, "Ingredient created.", x));
    }

    /// <summary>Update metadata with concurrency checks; unit cannot change after movements exist.</summary>
    [HttpPut("{id}"), Authorize(Roles = Roles.Manager + "," + Roles.Warehouse)]
    public async Task<ActionResult<ApiResponse<IngredientResponse>>> CapNhat(string id, SaveIngredientRequest request, CancellationToken ct) =>
        Success(await service.CapNhatNguyenLieuAsync(id, request, ct));

    /// <summary>Delete an unused ingredient; stock, movement history, and recipes prevent deletion.</summary>
    [HttpDelete("{id}"), Authorize(Roles = Roles.Manager + "," + Roles.Warehouse)]
    public async Task<IActionResult> Xoa(string id, [FromQuery] string rowVersion, CancellationToken ct)
    {
        await service.XoaNguyenLieuAsync(id, rowVersion, ct);
        return NoContent();
    }

    /// <summary>Get low-stock, expired, or seven-day expiry warnings.</summary>
    [HttpGet("warnings")]
    public async Task<ActionResult<ApiResponse<PageResponse<IngredientResponse>>>> CanhBao([FromQuery] ListRequest request, CancellationToken ct) =>
        Success(await service.TimKiemNguyenLieuAsync(request, true, ct));
}
