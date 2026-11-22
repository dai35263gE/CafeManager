using CafeManagement.Api.Contracts;

namespace CafeManagement.Api.Services;

public interface INhatKyThaoTacService
{
    Task<PageResponse<AuditLogResponse>> TimKiemNhatKyAsync(
        ListRequest request, DateTimeOffset? from, DateTimeOffset? to, string? action, CancellationToken ct);
}
