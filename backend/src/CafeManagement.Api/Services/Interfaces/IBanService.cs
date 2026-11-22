using CafeManagement.Api.Contracts;
using CafeManagement.Domain.Enums;

namespace CafeManagement.Api.Services;

public interface IBanService
{
    Task<PageResponse<TableResponse>> XemSoDoBanAsync(ListRequest request, TableStatus? status, string? area, CancellationToken ct);
    Task<TableResponse> LayBanAsync(string id, CancellationToken ct);
    Task<TableResponse> ThemBanAsync(SaveTableRequest request, CancellationToken ct);
    Task<TableResponse> SuaThongTinAsync(string id, SaveTableRequest request, CancellationToken ct);
    Task<TableResponse> XepBanAsync(string id, SeatTableRequest request, CancellationToken ct);
    Task<TableResponse> CapNhatTrangThaiBanAsync(string id, TableStatusRequest request, CancellationToken ct);
    Task NgungSuDungAsync(string id, string rowVersion, CancellationToken ct);
}
