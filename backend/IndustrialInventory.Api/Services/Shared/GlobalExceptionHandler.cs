using Microsoft.AspNetCore.Diagnostics;

namespace IndustrialInventory.Api.Services.Shared;

/// <summary>Turns an unhandled exception into the standard 500 { error, traceId } body.</summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var traceId = httpContext.TraceIdentifier;
        logger.LogError(exception, "Unhandled exception. TraceId={TraceId}", traceId);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(
            new
            {
                error = "Something went wrong on the server. Try again; if it keeps happening, give IT the reference code.",
                traceId
            },
            cancellationToken);

        return true;
    }
}
