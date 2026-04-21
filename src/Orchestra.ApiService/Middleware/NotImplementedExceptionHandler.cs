using Microsoft.AspNetCore.Diagnostics;

namespace Orchestra.ApiService.Middleware;

/// <summary>
/// Maps any unhandled <see cref="NotImplementedException"/> in the request pipeline to
/// HTTP 501 Not Implemented with a standard JSON error body.
/// All other exception types are left for the default handler to process.
/// </summary>
public sealed class NotImplementedExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not NotImplementedException)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status501NotImplemented;
        await httpContext.Response.WriteAsJsonAsync(
            new { error = "This feature is not yet implemented." },
            cancellationToken);

        return true;
    }
}
