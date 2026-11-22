using CafeManagement.Api.Contracts;
using CafeManagement.Api.Services;
using CafeManagement.Domain.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CafeManagement.Api.Middleware;

public sealed class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            logger.LogDebug("Request was aborted: {Method} {Path}; trace {TraceId}",
                context.Request.Method, context.Request.Path, context.TraceIdentifier);
        }
        catch (Exception exception) when (!context.Response.HasStarted)
        {
            var (status, message) = exception switch
            {
                ApiException api => (api.StatusCode, api.Message),
                DomainException => (StatusCodes.Status400BadRequest, exception.Message),
                DbUpdateConcurrencyException => (StatusCodes.Status409Conflict,
                    "The record changed. Reload it before trying again."),
                DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } } =>
                    (StatusCodes.Status409Conflict, "A conflicting record already exists."),
                DbUpdateException { InnerException: SqlException { Number: 547 } } =>
                    (StatusCodes.Status400BadRequest, "The operation violates a data integrity rule."),
                SqlException { Number: 1205 } =>
                    (StatusCodes.Status409Conflict, "A concurrent transaction conflicted. Reload and retry."),
                DbUpdateException { InnerException: SqlException { Number: 1205 } } =>
                    (StatusCodes.Status409Conflict, "A concurrent transaction conflicted. Reload and retry."),
                InvalidOperationException { InnerException: SqlException { Number: 1205 } } =>
                    (StatusCodes.Status409Conflict, "A concurrent transaction conflicted. Reload and retry."),
                InvalidOperationException { InnerException: DbUpdateException { InnerException: SqlException { Number: 1205 } } } =>
                    (StatusCodes.Status409Conflict, "A concurrent transaction conflicted. Reload and retry."),
                _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.")
            };
            logger.LogError(exception, "Request failed: {Method} {Path}; trace {TraceId}",
                context.Request.Method, context.Request.Path, context.TraceIdentifier);
            context.Response.Clear();
            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(new ApiResponse<object>(false, message, null,
                exception is ApiException { Errors: not null } apiError ? apiError.Errors :
                    new Dictionary<string, string[]> { ["traceId"] = [context.TraceIdentifier] }), context.RequestAborted);
        }
    }
}
