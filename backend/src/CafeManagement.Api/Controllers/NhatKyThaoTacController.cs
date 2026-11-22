using CafeManagement.Api.Contracts;
using CafeManagement.Api.Services;
using CafeManagement.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeManagement.Api.Controllers;

[Route("api/nhat-ky"), Authorize(Roles = Roles.Admin + "," + Roles.Manager)]
public sealed class NhatKyThaoTacController(INhatKyThaoTacService service) : ApiController
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PageResponse<AuditLogResponse>>>> TimKiem(
        [FromQuery] ListRequest request, DateTimeOffset? from, DateTimeOffset? to,
        string? action, CancellationToken ct) =>
        Success(await service.TimKiemNhatKyAsync(request, from, to, action, ct));
}
