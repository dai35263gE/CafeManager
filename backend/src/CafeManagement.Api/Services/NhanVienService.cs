using System.Data;
using CafeManagement.Api.Contracts;
using CafeManagement.Domain.Common;
using CafeManagement.Domain.Entities;
using CafeManagement.Domain.Enums;
using CafeManagement.Infrastructure;
using CafeManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CafeManagement.Api.Services;

public sealed class NhanVienService(
    CafeDbContext db,
    IdGenerator ids,
    IHttpContextAccessor accessor) : INhanVienService
{
    public async Task<PageResponse<EmployeeResponse>> TimKiemNhanVienAsync(
        ListRequest request, EmployeeStatus? status, string? position, CancellationToken ct)
    {
        var query = db.NhanViens.AsNoTracking().Include(x => x.TaiKhoan).AsQueryable();
        if (status.HasValue)
            query = query.Where(x => x.TrangThai == status);
        if (!string.IsNullOrWhiteSpace(position))
        {
            var positionSearch = position.Trim();
            query = query.Where(x => x.ViTri.Contains(positionSearch));
        }
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(x => x.MaNhanVien.Contains(search) || x.HoTen.Contains(search));
        }

        var total = await query.CountAsync(ct);
        var offset = ((long)request.Page - 1) * request.PageSize;
        var employees = offset > int.MaxValue ? [] : await query.OrderBy(x => x.MaNhanVien)
            .Skip((int)offset).Take(request.PageSize).ToListAsync(ct);
        return new PageResponse<EmployeeResponse>(employees.Select(Map).ToList(), total, request.Page, request.PageSize);
    }

    public async Task<EmployeeResponse> LayNhanVienAsync(string id, CancellationToken ct) =>
        Map(await FindAsync(id, ct));

    public async Task<EmployeeResponse> ThemNhanVienAsync(CreateEmployeeRequest request, CancellationToken ct)
    {
        var id = await ids.NhanVienAsync(ct);
        return await SaveAsync(null, id, request.FullName, request.Position, request.Phone, request.Email,
            EmployeeStatus.Working, ct);
    }

    public Task<EmployeeResponse> CapNhatHoSoAsync(
        string id, SaveEmployeeRequest request, CancellationToken ct)
    {
        if (!string.Equals(request.Code.Trim(), id, StringComparison.Ordinal))
            throw new ApiException(400, "An employee code cannot be changed.");
        return SaveAsync(id, id, request.FullName, request.Position, request.Phone, request.Email, request.Status, ct);
    }

    private async Task<EmployeeResponse> SaveAsync(
        string? id, string code, string fullName, string position, string? phone, string? email,
        EmployeeStatus status, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        code = code.Trim();
        if (code.Length is 0 or > IdGenerator.MaxLength)
            throw new ApiException(400, $"Employee code must contain at most {IdGenerator.MaxLength} characters.");

        var employee = id is null
            ? new NhanVien { MaNhanVien = code, TrangThai = EmployeeStatus.Working }
            : await FindAsync(id, ct);
        if (id is null)
        {
            if (await db.NhanViens.AnyAsync(x => x.MaNhanVien == code, ct))
                throw ServiceHelpers.Conflict("The employee code is already in use.");
            db.NhanViens.Add(employee);
        }

        var contact = BuildContact(phone, email);
        await EnsureContactIsUniqueAsync(code, phone, email, ct);
        if (status == EmployeeStatus.Resigned && employee.TrangThai != EmployeeStatus.Resigned)
            await DisableAccountAsync(employee, ct);
        employee.HoTen = fullName.Trim();
        employee.ViTri = position.Trim();
        employee.ThongTinLienHe = contact;
        employee.TrangThai = status;

        ServiceHelpers.Audit(db, ServiceHelpers.Actor(accessor), "EmployeeSaved", "NhanVien",
            employee.MaNhanVien, $"Employee status {status}.");
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Map(employee);
    }

    public async Task NgungNhanVienAsync(string id, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var employee = await FindAsync(id, ct);
        if (employee.TrangThai != EmployeeStatus.Resigned)
        {
            await DisableAccountAsync(employee, ct);
            employee.TrangThai = EmployeeStatus.Resigned;
            ServiceHelpers.Audit(db, ServiceHelpers.Actor(accessor), "EmployeeResigned", "NhanVien",
                id, "Employee marked as resigned.");
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
    }

    private async Task DisableAccountAsync(NhanVien employee, CancellationToken ct)
    {
        var account = await db.TaiKhoans.Include(x => x.VaiTro)
            .SingleOrDefaultAsync(x => x.MaNhanVien == employee.MaNhanVien, ct);
        if (account is null)
            return;
        if (account.VaiTro.TenVaiTro == Roles.Admin)
        {
            var anotherActiveAdmin = await db.TaiKhoans.AnyAsync(x =>
                x.TenDangNhap != account.TenDangNhap &&
                x.TrangThai == AccountStatus.Active &&
                x.NhanVien.TrangThai == EmployeeStatus.Working &&
                x.VaiTro.TenVaiTro == Roles.Admin, ct);
            if (!anotherActiveAdmin)
                throw ServiceHelpers.Conflict("At least one active administrator must remain.");
        }
        if (account.MaNhanVien == ServiceHelpers.Actor(accessor))
            throw ServiceHelpers.Conflict("You cannot resign your own employee account.");
        account.TrangThai = AccountStatus.Disabled;
    }

    private async Task<NhanVien> FindAsync(string id, CancellationToken ct) =>
        await db.NhanViens.Include(x => x.TaiKhoan)
            .SingleOrDefaultAsync(x => x.MaNhanVien == id, ct) ?? throw ServiceHelpers.NotFound("Employee");

    private static EmployeeResponse Map(NhanVien employee)
    {
        var (phone, email) = SplitContact(employee.ThongTinLienHe);
        var account = employee.TaiKhoan;
        return new(employee.MaNhanVien, employee.MaNhanVien, employee.HoTen, employee.ViTri,
            phone, email, employee.TrangThai, account?.TenDangNhap,
            account is null ? null : account.TrangThai == AccountStatus.Active,
            account is null ? null : account.TrangThai == AccountStatus.Locked);
    }

    private async Task EnsureContactIsUniqueAsync(
        string employeeCode, string? phone, string? email, CancellationToken ct)
    {
        var normalizedPhone = phone?.Trim();
        if (!string.IsNullOrEmpty(normalizedPhone))
        {
            var phoneLabel = $"Phone: {normalizedPhone}";
            if (await db.NhanViens.AnyAsync(x => x.MaNhanVien != employeeCode &&
                    (x.ThongTinLienHe == phoneLabel || x.ThongTinLienHe.StartsWith(phoneLabel + "; ")), ct))
                throw ServiceHelpers.Conflict("Nhân viên đã tồn tại: thông tin liên hệ đã được sử dụng.");
        }

        var normalizedEmail = email?.Trim();
        if (!string.IsNullOrEmpty(normalizedEmail))
        {
            var emailLabel = $"Email: {normalizedEmail}";
            if (await db.NhanViens.AnyAsync(x => x.MaNhanVien != employeeCode &&
                    (x.ThongTinLienHe == emailLabel || x.ThongTinLienHe == normalizedEmail ||
                     x.ThongTinLienHe.EndsWith("; " + emailLabel)), ct))
                throw ServiceHelpers.Conflict("Nhân viên đã tồn tại: thông tin liên hệ đã được sử dụng.");
        }
    }

    private static string BuildContact(string? phone, string? email)
    {
        var parts = new List<string>(2);
        if (!string.IsNullOrWhiteSpace(phone))
            parts.Add($"Phone: {phone.Trim()}");
        if (!string.IsNullOrWhiteSpace(email))
            parts.Add($"Email: {email.Trim()}");
        var value = string.Join("; ", parts);
        if (value.Length > 150)
            throw new ApiException(400, "Combined contact information cannot exceed 150 characters.");
        return value;
    }

    private static (string? Phone, string? Email) SplitContact(string value)
    {
        if (value.StartsWith("Phone: ", StringComparison.Ordinal) || value.StartsWith("Email: ", StringComparison.Ordinal))
        {
            string? phone = null;
            string? email = null;
            foreach (var part in value.Split("; ", StringSplitOptions.RemoveEmptyEntries))
            {
                if (part.StartsWith("Phone: ", StringComparison.Ordinal))
                    phone = part[7..];
                else if (part.StartsWith("Email: ", StringComparison.Ordinal))
                    email = part[7..];
            }
            return (phone, email);
        }
        return (null, value.Contains('@') ? value : null);
    }
}
