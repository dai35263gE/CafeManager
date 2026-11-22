using CafeManagement.Api.Contracts;
using CafeManagement.Domain.Enums;

namespace CafeManagement.Api.Services;

public interface IBienDongKhoService
{
    Task<InventoryMovementResponse> LapBienDongAsync(
        string id, InventoryTransactionType type, InventoryMovementRequest request, CancellationToken ct);
    Task<PageResponse<InventoryTransactionResponse>> TimKiemBienDongAsync(
        string id, ListRequest request, CancellationToken ct);
    Task<InventoryTransactionResponse> LayBienDongAsync(string id, string transactionId, CancellationToken ct);
}
