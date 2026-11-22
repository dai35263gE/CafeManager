using CafeManagement.Api.Contracts;
using CafeManagement.Api.Services;
using CafeManagement.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeManagement.Api.Controllers;

[Route("api/tai-khoan"), Authorize(Roles = Roles.Admin)]
public sealed class TaiKhoanController(ITaiKhoanService service) : ApiController
{
    /// <summary>Search and paginate accounts.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PageResponse<UserResponse>>>> TimKiem([FromQuery] ListRequest request, CancellationToken ct) =>
        Success(await service.TimKiemTaiKhoanAsync(request, ct));

    /// <summary>Get an account without sensitive fields.</summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<UserResponse>>> Lay(string id, CancellationToken ct) =>
        Success(await service.LayTaiKhoanAsync(id, ct));

    /// <summary>Create an account with exactly one supported role.</summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<UserResponse>>> Them(CreateUserRequest request, CancellationToken ct)
    {
        var result = await service.ThemTaiKhoanAsync(request, ct);
        return CreatedAtAction(nameof(Lay), new { id = result.Id }, new ApiResponse<UserResponse>(true, "Account created.", result));
    }

    /// <summary>Update account profile and role; invalidate its tokens.</summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<UserResponse>>> CapNhat(string id, UpdateUserRequest request, CancellationToken ct) =>
        Success(await service.CapNhatTaiKhoanAsync(id, request, ct));

    /// <summary>Lock or unlock an account; invalidate its tokens.</summary>
    [HttpPatch("{id}/lock")]
    public async Task<ActionResult<ApiResponse<UserResponse>>> Khoa(string id, LockUserRequest request, CancellationToken ct) =>
        Success(await service.KhoaTaiKhoanAsync(id, request, ct));
}
