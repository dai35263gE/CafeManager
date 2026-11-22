using CafeManagement.Api.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeManagement.Api.Controllers;

[Route("api/health")]
public sealed class HealthController : ApiController
{
    /// <summary>Check API liveness without accessing the database.</summary>
    [HttpGet, AllowAnonymous]
    public ActionResult<ApiResponse<HealthResponse>> Get(CancellationToken ct) =>
        Success(new HealthResponse(3), "API is running.");
}
