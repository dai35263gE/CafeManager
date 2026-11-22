using CafeManagement.Api.Contracts;

namespace CafeManagement.Api.Services;

public interface ITaiKhoanService
{
    Task<LoginResponse> DangNhapAsync(LoginRequest request, CancellationToken ct);
    Task<UserResponse> XacThucTaiKhoanAsync(CancellationToken ct);
    Task DoiMatKhauAsync(ChangePasswordRequest request, CancellationToken ct);
    Task<PageResponse<UserResponse>> TimKiemTaiKhoanAsync(ListRequest request, CancellationToken ct);
    Task<UserResponse> LayTaiKhoanAsync(string id, CancellationToken ct);
    Task<UserResponse> ThemTaiKhoanAsync(CreateUserRequest request, CancellationToken ct);
    Task<UserResponse> ThemTaiKhoanNhanVienAsync(string employeeId, CreateEmployeeAccountRequest request, CancellationToken ct);
    Task<UserResponse> CapNhatTaiKhoanAsync(string id, UpdateUserRequest request, CancellationToken ct);
    Task<UserResponse> KhoaTaiKhoanAsync(string id, LockUserRequest request, CancellationToken ct);
}
