using CafeManagement.Api.Contracts;
using CafeManagement.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CafeManagement.Api.Controllers;

[Route("api/auth")]
public sealed class TaiKhoanXacThucController(ITaiKhoanService service) : ApiController
{
    /// <summary>Sign in and receive an expiring JWT.</summary>
    [AllowAnonymous, HttpPost("login"), EnableRateLimiting("Login")]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> DangNhap(LoginRequest request, CancellationToken ct) =>
        Success(await service.DangNhapAsync(request, ct));

    /// <summary>Return the authenticated account.</summary>
    [HttpGet("me")]
    public async Task<ActionResult<ApiResponse<UserResponse>>> XacThuc(CancellationToken ct) =>
        Success(await service.XacThucTaiKhoanAsync(ct));

    /// <summary>Change password and invalidate every issued JWT, including this session.</summary>
    [HttpPost("change-password")]
    public async Task<ActionResult<ApiResponse<MessageResponse>>> DoiMatKhau(ChangePasswordRequest request, CancellationToken ct)
    {
        await service.DoiMatKhauAsync(request, ct);
        return Success(new MessageResponse("Password changed. Sign in again."));
    }
}
