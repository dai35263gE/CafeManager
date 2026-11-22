using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CafeManagement.Api.Contracts;
using CafeManagement.Domain.Common;
using CafeManagement.Domain.Entities;
using CafeManagement.Domain.Enums;
using CafeManagement.Infrastructure;
using CafeManagement.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace CafeManagement.Api.Services;

public sealed class TaiKhoanService(
    CafeDbContext db,
    IdGenerator ids,
    IPasswordHasher<TaiKhoan> passwordHasher,
    IHttpContextAccessor accessor,
    IConfiguration configuration) : ITaiKhoanService
{
    public async Task<LoginResponse> DangNhapAsync(LoginRequest request, CancellationToken ct)
    {
        var username = request.Username.Trim();
        var account = await db.TaiKhoans.Include(x => x.NhanVien).Include(x => x.VaiTro)
            .SingleOrDefaultAsync(x => x.TenDangNhap == username, ct);
        if (account is null ||
            account.TrangThai != AccountStatus.Active ||
            account.NhanVien.TrangThai != EmployeeStatus.Working)
            throw new ApiException(401, "Invalid credentials or unavailable account.");

        var verification = passwordHasher.VerifyHashedPassword(account, account.MatKhauHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
            throw new ApiException(401, "Invalid credentials or unavailable account.");
        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
            account.MatKhauHash = passwordHasher.HashPassword(account, request.Password);

        account.ThoiGianDangNhap = DateTime.UtcNow;
        await LogAsync(account, "Login", "TaiKhoan", "Account signed in.", ct);
        await db.SaveChangesAsync(ct);

        var expires = DateTimeOffset.UtcNow.AddMinutes(configuration.GetValue<int>("Jwt:ExpirationMinutes", 60));
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, account.TenDangNhap),
            new Claim(ClaimTypes.Name, account.TenDangNhap),
            new Claim(ClaimTypes.Role, account.VaiTro.TenVaiTro),
            new Claim("employee_id", account.MaNhanVien),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        var token = new JwtSecurityToken(configuration["Jwt:Issuer"], configuration["Jwt:Audience"], claims,
            expires: expires.UtcDateTime,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!)),
                SecurityAlgorithms.HmacSha256));
        return new LoginResponse(new JwtSecurityTokenHandler().WriteToken(token), expires, Map(account));
    }

    public async Task<UserResponse> XacThucTaiKhoanAsync(CancellationToken ct) =>
        Map(await FindAsync(Actor(), ct));

    public async Task DoiMatKhauAsync(ChangePasswordRequest request, CancellationToken ct)
    {
        var account = await FindAsync(Actor(), ct);
        if (passwordHasher.VerifyHashedPassword(account, account.MatKhauHash, request.CurrentPassword) ==
            PasswordVerificationResult.Failed)
            throw new ApiException(400, "Current password is incorrect.");
        account.MatKhauHash = passwordHasher.HashPassword(account, request.NewPassword);
        await LogAsync(account, "PasswordChanged", "TaiKhoan", "Account password changed.", ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task<PageResponse<UserResponse>> TimKiemTaiKhoanAsync(ListRequest request, CancellationToken ct)
    {
        var query = db.TaiKhoans.AsNoTracking().Include(x => x.NhanVien).Include(x => x.VaiTro).AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(x => x.TenDangNhap.Contains(search) || x.NhanVien.HoTen.Contains(search));
        }
        var total = await query.CountAsync(ct);
        var offset = ((long)request.Page - 1) * request.PageSize;
        var accounts = offset > int.MaxValue ? [] : await query.OrderBy(x => x.TenDangNhap)
            .Skip((int)offset).Take(request.PageSize).ToListAsync(ct);
        return new PageResponse<UserResponse>(accounts.Select(Map).ToList(), total, request.Page, request.PageSize);
    }

    public async Task<UserResponse> LayTaiKhoanAsync(string id, CancellationToken ct) => Map(await FindAsync(id, ct));

    public Task<UserResponse> ThemTaiKhoanAsync(CreateUserRequest request, CancellationToken ct) =>
        ThemTaiKhoanAsync(request.Username, request.Password, request.Role, request.EmployeeId,
            request.FullName, request.Email, ct);

    public Task<UserResponse> ThemTaiKhoanNhanVienAsync(
        string employeeId, CreateEmployeeAccountRequest request, CancellationToken ct) =>
        ThemTaiKhoanAsync(request.Username, request.Password, request.Role, employeeId, null, null, ct);

    private async Task<UserResponse> ThemTaiKhoanAsync(
        string usernameInput, string password, string roleName, string? employeeId,
        string? fullName, string? email, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        ValidateRole(roleName);
        var username = usernameInput.Trim();
        if (await db.TaiKhoans.AnyAsync(x => x.TenDangNhap == username, ct))
            throw ServiceHelpers.Conflict("Tên đăng nhập đã tồn tại.");

        NhanVien employee;
        if (employeeId is null)
        {
            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email))
                throw new ApiException(400, "Full name and email are required when creating an unlinked employee account.");
            employee = new NhanVien
            {
                MaNhanVien = await ids.NhanVienAsync(ct),
                HoTen = fullName.Trim(),
                ViTri = "Nhân viên",
                ThongTinLienHe = email.Trim(),
                TrangThai = EmployeeStatus.Working
            };
            db.NhanViens.Add(employee);
        }
        else
            employee = await FindWorkingEmployeeAsync(employeeId, ct);
        if (await db.TaiKhoans.AnyAsync(x => x.MaNhanVien == employee.MaNhanVien, ct))
            throw ServiceHelpers.Conflict("An employee can have only one account.");

        var account = new TaiKhoan
        {
            TenDangNhap = username,
            MaNhanVien = employee.MaNhanVien,
            NhanVien = employee,
            MaVaiTro = Roles.Definitions[roleName].Id,
            VaiTro = await RoleAsync(roleName, ct),
            MatKhauHash = string.Empty,
            TrangThai = AccountStatus.Active,
            ThoiGianDangNhap = AccountDefaults.NeverLoggedIn
        };
        account.MatKhauHash = passwordHasher.HashPassword(account, password);
        db.TaiKhoans.Add(account);
        await LogCurrentAsync("AccountCreated", "TaiKhoan", $"Created account with role {roleName}.", ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Map(account);
    }

    public async Task<UserResponse> CapNhatTaiKhoanAsync(string id, UpdateUserRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        ValidateRole(request.Role);
        var account = await db.TaiKhoans.Include(x => x.NhanVien).Include(x => x.VaiTro)
            .SingleOrDefaultAsync(x => x.TenDangNhap == id, ct) ?? throw ServiceHelpers.NotFound("Account");
        if (id == Actor() && request.Role != Roles.Admin)
            throw ServiceHelpers.Conflict("An administrator cannot remove their own Admin role.");
        await PreserveAdminAsync(account, request.Role == Roles.Admin && account.TrangThai == AccountStatus.Active, ct);

        var employee = request.EmployeeId is null
            ? account.NhanVien
            : await FindWorkingEmployeeAsync(request.EmployeeId, ct);
        if (employee.MaNhanVien != account.MaNhanVien &&
            await db.TaiKhoans.AnyAsync(x => x.MaNhanVien == employee.MaNhanVien && x.TenDangNhap != id, ct))
            throw ServiceHelpers.Conflict("An employee can have only one account.");
        account.MaNhanVien = employee.MaNhanVien;
        account.NhanVien = employee;
        account.NhanVien.HoTen = request.FullName.Trim();
        account.NhanVien.ThongTinLienHe = request.Email.Trim();
        account.MaVaiTro = Roles.Definitions[request.Role].Id;
        account.VaiTro = await RoleAsync(request.Role, ct);
        await LogCurrentAsync("AccountUpdated", "TaiKhoan", $"Updated account and assigned role {request.Role}.", ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Map(account);
    }

    public async Task<UserResponse> KhoaTaiKhoanAsync(string id, LockUserRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var account = await db.TaiKhoans.Include(x => x.NhanVien).Include(x => x.VaiTro)
            .SingleOrDefaultAsync(x => x.TenDangNhap == id, ct) ?? throw ServiceHelpers.NotFound("Account");
        if (request.Locked && id == Actor())
            throw ServiceHelpers.Conflict("You cannot lock your own account.");
        if (ServiceHelpers.IsManager(accessor) && account.VaiTro.TenVaiTro == Roles.Admin)
            throw new ApiException(403, "Managers cannot change an Admin account.");
        await PreserveAdminAsync(account, !request.Locked, ct);
        if (!request.Locked && account.NhanVien.TrangThai != EmployeeStatus.Working)
            throw new ApiException(400, "A resigned employee's account cannot be unlocked.");
        account.TrangThai = request.Locked ? AccountStatus.Locked : AccountStatus.Active;
        await LogCurrentAsync(request.Locked ? "AccountLocked" : "AccountUnlocked", "TaiKhoan",
            "Account access changed.", ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Map(account);
    }

    private async Task PreserveAdminAsync(TaiKhoan account, bool remainsAdmin, CancellationToken ct)
    {
        if (remainsAdmin || account.VaiTro.TenVaiTro != Roles.Admin)
            return;
        var otherAdminExists = await db.TaiKhoans.AnyAsync(x =>
            x.TenDangNhap != account.TenDangNhap &&
            x.TrangThai == AccountStatus.Active &&
            x.NhanVien.TrangThai == EmployeeStatus.Working &&
            x.VaiTro.TenVaiTro == Roles.Admin, ct);
        if (!otherAdminExists)
            throw ServiceHelpers.Conflict("At least one active administrator must remain.");
    }

    private async Task<NhanVien> FindWorkingEmployeeAsync(string id, CancellationToken ct) =>
        await db.NhanViens.SingleOrDefaultAsync(x => x.MaNhanVien == id && x.TrangThai == EmployeeStatus.Working, ct)
        ?? throw new ApiException(400, "The linked employee must be working.");

    private async Task<TaiKhoan> FindAsync(string id, CancellationToken ct) =>
        await db.TaiKhoans.Include(x => x.NhanVien).Include(x => x.VaiTro)
            .SingleOrDefaultAsync(x => x.TenDangNhap == id, ct) ?? throw ServiceHelpers.NotFound("Account");

    private async Task<VaiTro> RoleAsync(string name, CancellationToken ct) =>
        await db.VaiTros.SingleOrDefaultAsync(x => x.TenVaiTro == name, ct)
        ?? throw new ApiException(403, "The account role is not configured.");

    private static void ValidateRole(string role)
    {
        if (!Roles.All.Contains(role))
            throw new ApiException(400, "Unknown role.");
    }

    private string Actor() =>
        accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new ApiException(401, "Authentication required.");

    private async Task LogAsync(TaiKhoan account, string action, string entity, string message, CancellationToken ct)
    {
        db.NhatKyThaoTacs.Add(new NhatKyThaoTac
        {
            MaNhatKy = await ids.NhatKyAsync(ct),
            MaNhanVien = account.MaNhanVien,
            LoaiThaoTac = action,
            DoiTuongTacDong = entity,
            NoiDung = message,
            ThoiGian = DateTime.UtcNow
        });
    }

    private async Task LogCurrentAsync(string action, string entity, string message, CancellationToken ct)
    {
        var actor = await db.TaiKhoans.AsNoTracking().SingleOrDefaultAsync(x => x.TenDangNhap == Actor(), ct)
            ?? throw new ApiException(401, "Authentication required.");
        db.NhatKyThaoTacs.Add(new NhatKyThaoTac
        {
            MaNhatKy = await ids.NhatKyAsync(ct),
            MaNhanVien = actor.MaNhanVien,
            LoaiThaoTac = action,
            DoiTuongTacDong = entity,
            NoiDung = message,
            ThoiGian = DateTime.UtcNow
        });
    }

    private static UserResponse Map(TaiKhoan account) =>
        new(account.TenDangNhap, account.TenDangNhap, account.NhanVien.HoTen, account.VaiTro.TenVaiTro,
            account.MaNhanVien, account.TrangThai == AccountStatus.Active, AsOffset(account.ThoiGianDangNhap),
            account.TrangThai == AccountStatus.Locked);

    private static DateTimeOffset? AsOffset(DateTime value) =>
        value == AccountDefaults.NeverLoggedIn
            ? null
            : new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
