using CafeManagement.Api.Contracts;
using CafeManagement.Domain.Enums;

namespace CafeManagement.Api.Services;

public sealed class BienDongKhoService(NguyenLieuService operations) : IBienDongKhoService
{
    public Task<InventoryMovementResponse> LapBienDongAsync(
        string id, InventoryTransactionType type, InventoryMovementRequest request, CancellationToken ct) =>
        operations.LapBienDongAsync(id, type, request, ct);

    public Task<PageResponse<InventoryTransactionResponse>> TimKiemBienDongAsync(
        string id, ListRequest request, CancellationToken ct) =>
        operations.TimKiemBienDongAsync(id, request, ct);

    public Task<InventoryTransactionResponse> LayBienDongAsync(
        string id, string transactionId, CancellationToken ct) =>
        operations.LayBienDongAsync(id, transactionId, ct);
}
