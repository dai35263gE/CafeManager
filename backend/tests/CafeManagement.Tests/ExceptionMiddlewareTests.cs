using CafeManagement.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CafeManagement.Tests;

public sealed class ExceptionMiddlewareTests
{
    [Fact]
    public async Task ClientAbortedRequestDoesNotBecomeAnErrorResponse()
    {
        var context = new DefaultHttpContext
        {
            RequestAborted = new CancellationToken(canceled: true)
        };
        var middleware = new ExceptionMiddleware(
            _ => Task.FromException(new TaskCanceledException()),
            NullLogger<ExceptionMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }
}
