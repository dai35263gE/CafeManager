using CafeManagement.Api.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace CafeManagement.Api.Controllers;

[ApiController]
[Produces("application/json")]
public abstract class ApiController : ControllerBase
{
    protected ActionResult<ApiResponse<T>> Success<T>(T data, string message = "Operation completed.") =>
        Ok(new ApiResponse<T>(true, message, data));
}
