using CafeManagement.Api.Contracts;
using CafeManagement.Api.Services;
using CafeManagement.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeManagement.Api.Controllers;

[Route("api/dinh-luong"), Authorize(Roles = Roles.Manager + "," + Roles.Warehouse)]
public sealed class DinhLuongController(IDinhLuongService service) : ApiController
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PageResponse<RecipeResponse>>>> TimKiem(
        [FromQuery] ListRequest request, string? menuItemId, CancellationToken ct) =>
        Success(await service.TimKiemDinhLuongAsync(request, menuItemId, ct));

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<RecipeResponse>>> Lay(string id, CancellationToken ct) =>
        Success(await service.LayDinhLuongAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<ApiResponse<RecipeResponse>>> Them(
        SaveRecipeRequest request, CancellationToken ct)
    {
        var recipe = await service.CapNhatDinhLuongAsync(null, request, ct);
        return CreatedAtAction(nameof(Lay), new { id = recipe.MaDinhLuong },
            new ApiResponse<RecipeResponse>(true, "Recipe created.", recipe));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<RecipeResponse>>> CapNhat(
        string id, SaveRecipeRequest request, CancellationToken ct) =>
        Success(await service.CapNhatDinhLuongAsync(id, request, ct));

    [HttpDelete("{id}")]
    public async Task<IActionResult> Xoa(string id, CancellationToken ct)
    {
        await service.XoaDinhLuongAsync(id, ct);
        return NoContent();
    }
}
