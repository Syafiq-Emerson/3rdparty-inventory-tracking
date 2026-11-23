using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace IndustrialInventory.Api.Services.Security;

/// <summary>
/// Unsafe API requests must come from the app's own origin (the scheme and host the request was sent to),
/// or from one of the extra origins in Security:AllowedOrigins (development: the Vite dev server).
/// </summary>
public sealed class ExactOriginAllowlist
{
    private readonly HashSet<Origin> origins;

    public ExactOriginAllowlist(IEnumerable<string> configuredOrigins)
    {
        ArgumentNullException.ThrowIfNull(configuredOrigins);

        origins = [];
        foreach (var value in configuredOrigins)
        {
            if (!TryParseOrigin(value, out var origin))
            {
                throw new InvalidOperationException(
                    "Security:AllowedOrigins must contain only exact HTTP or HTTPS origins.");
            }

            origins.Add(origin);
        }
    }

    public bool Allows(StringValues originHeaders, HttpRequest request)
    {
        if (originHeaders.Count != 1 ||
            string.IsNullOrWhiteSpace(originHeaders[0]) ||
            !TryParseOrigin(originHeaders[0]!, out var origin))
        {
            return false;
        }

        return origins.Contains(origin) ||
            (TryGetRequestOrigin(request, out var own) && own == origin);
    }

    private static bool TryGetRequestOrigin(HttpRequest request, out Origin origin)
    {
        origin = default;
        return request.Host.HasValue &&
            TryParseOrigin($"{request.Scheme}://{request.Host.Value}", out origin);
    }

    private static bool TryParseOrigin(string? value, out Origin origin)
    {
        origin = default;
        if (string.IsNullOrWhiteSpace(value) ||
            !Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            string.IsNullOrWhiteSpace(uri.Host) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            uri.AbsolutePath != "/" ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            return false;
        }

        origin = new Origin(uri.Scheme.ToLowerInvariant(), uri.IdnHost.ToLowerInvariant(), uri.Port);
        return true;
    }

    private readonly record struct Origin(string Scheme, string Host, int Port);
}

public sealed class ExactOriginMiddleware(RequestDelegate next, ExactOriginAllowlist allowlist)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!IsUnsafe(context.Request.Method) || !IsApiRequest(context.Request.Path))
        {
            await next(context);
            return;
        }

        if (!allowlist.Allows(context.Request.Headers["Origin"], context.Request))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "The request origin is not allowed.",
                errorCode = "ORIGIN_NOT_ALLOWED"
            }, context.RequestAborted);
            return;
        }

        await next(context);
    }

    private static bool IsUnsafe(string method) =>
        !HttpMethods.IsGet(method) &&
        !HttpMethods.IsHead(method) &&
        !HttpMethods.IsOptions(method) &&
        !HttpMethods.IsTrace(method);

    private static bool IsApiRequest(PathString path)
    {
        var value = path.Value;
        return value is not null &&
            (string.Equals(value, "/api", StringComparison.OrdinalIgnoreCase) ||
             value.StartsWith("/api/", StringComparison.OrdinalIgnoreCase));
    }
}
