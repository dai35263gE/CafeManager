using CafeManagement.Api.Contracts;
using CafeManagement.Domain.Enums;

namespace CafeManagement.Api.Services;

public interface INhanVienService
{
    Task<PageResponse<EmployeeResponse>> TimKiemNhanVienAsync(
        ListRequest request, EmployeeStatus? status, string? position, CancellationToken ct);
    Task<EmployeeResponse> LayNhanVienAsync(string id, CancellationToken ct);
    Task<EmployeeResponse> ThemNhanVienAsync(CreateEmployeeRequest request, CancellationToken ct);
    Task<EmployeeResponse> CapNhatHoSoAsync(string id, SaveEmployeeRequest request, CancellationToken ct);
    Task NgungNhanVienAsync(string id, CancellationToken ct);
}
