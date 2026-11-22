using CafeManagement.Api.Contracts;
using CafeManagement.Api.Services;
using CafeManagement.Domain.Common;
using CafeManagement.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeManagement.Api.Controllers;

[Route("api/nhan-vien"), Authorize(Roles = Roles.Manager + "," + Roles.Admin)]
public sealed class NhanVienController(INhanVienService employees, ITaiKhoanService accounts) : ApiController
{
    /// <summary>Search employee profiles and account linkage.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PageResponse<EmployeeResponse>>>> TimKiem(
        [FromQuery] ListRequest request, EmployeeStatus? status, string? position, CancellationToken ct) =>
        Success(await employees.TimKiemNhanVienAsync(request, status, position, ct));

    /// <summary>Get an employee.</summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<EmployeeResponse>>> Lay(string id, CancellationToken ct) =>
        Success(await employees.LayNhanVienAsync(id, ct));

    /// <summary>Create an employee profile with a system-generated code.</summary>
    [HttpPost, Authorize(Roles = Roles.Manager)]
    public async Task<ActionResult<ApiResponse<EmployeeResponse>>> Them(CreateEmployeeRequest request, CancellationToken ct)
    {
        var x = await employees.ThemNhanVienAsync(request, ct);
        return CreatedAtAction(nameof(Lay), new { id = x.Id }, new ApiResponse<EmployeeResponse>(true, "Employee created.", x));
    }

    /// <summary>Update employee profile; resignation locks the linked account in the same transaction.</summary>
    [HttpPut("{id}"), Authorize(Roles = Roles.Manager)]
    public async Task<ActionResult<ApiResponse<EmployeeResponse>>> CapNhatHoSo(string id, SaveEmployeeRequest request, CancellationToken ct) =>
        Success(await employees.CapNhatHoSoAsync(id, request, ct));

    /// <summary>Soft-delete an employee and lock the linked account.</summary>
    [HttpDelete("{id}"), Authorize(Roles = Roles.Manager)]
    public async Task<IActionResult> Ngung(string id, CancellationToken ct)
    {
        await employees.NgungNhanVienAsync(id, ct);
        return NoContent();
    }

    /// <summary>Create an account for a working employee. Managers cannot assign the Admin role.</summary>
    [HttpPost("{id}/account"), Authorize(Roles = Roles.Manager + "," + Roles.Admin)]
    public async Task<ActionResult<ApiResponse<UserResponse>>> Account(
        string id, CreateEmployeeAccountRequest request, CancellationToken ct)
    {
        if (User.IsInRole(Roles.Manager) && request.Role == Roles.Admin)
            throw new ApiException(403, "Managers cannot assign the Admin role.");
        var x = await accounts.ThemTaiKhoanNhanVienAsync(id, request, ct);
        return Created($"/api/nhan-vien/{id}", new ApiResponse<UserResponse>(true, "Employee account created.", x));
    }

    /// <summary>Lock or unlock an account linked to a working employee.</summary>
    [HttpPatch("{id}/account/lock"), Authorize(Roles = Roles.Manager + "," + Roles.Admin)]
    public async Task<ActionResult<ApiResponse<UserResponse>>> KhoaTaiKhoan(
        string id, LockUserRequest request, CancellationToken ct)
    {
        var employee = await employees.LayNhanVienAsync(id, ct);
        if (employee.Status != EmployeeStatus.Working)
            throw ServiceHelpers.Conflict("An account linked to a resigned employee cannot be changed.");
        if (employee.UserId is null)
            throw ServiceHelpers.Conflict("This employee does not have an account.");
        return Success(await accounts.KhoaTaiKhoanAsync(employee.UserId, request, ct));
    }
}
