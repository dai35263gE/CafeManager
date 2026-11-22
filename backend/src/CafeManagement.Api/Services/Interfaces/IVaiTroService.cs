using CafeManagement.Api.Contracts;

namespace CafeManagement.Api.Services;

public interface IVaiTroService
{
    Task<DanhSachQuyenResponse> LayDanhSachQuyenAsync(string id, CancellationToken ct);
}
