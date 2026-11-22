using System.Security.Claims;
using CafeManagement.Domain.Enums;
using CafeManagement.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

namespace CafeManagement.Api.Security;

public sealed class AccountTokenValidator(CafeDbContext db)
{
    public async Task ValidateAsync(TokenValidatedContext context)
    {
        var ct = context.HttpContext.RequestAborted;
        var username = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(username))
        {
            context.Fail("Invalid account identifier.");
            return;
        }
        var account = await db.TaiKhoans.Include(x => x.NhanVien).Include(x => x.VaiTro)
            .SingleOrDefaultAsync(x => x.TenDangNhap == username, ct);
        if (account is null || account.TrangThai != AccountStatus.Active ||
            account.NhanVien.TrangThai != EmployeeStatus.Working ||
            account.VaiTro.TenVaiTro != context.Principal?.FindFirstValue(ClaimTypes.Role) ||
            account.MaNhanVien != context.Principal?.FindFirstValue("employee_id"))
        {
            context.Fail("Account access has changed.");
        }
    }
}
