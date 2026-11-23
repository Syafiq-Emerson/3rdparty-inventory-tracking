using IndustrialInventory.Api.Services.Security;
using Microsoft.AspNetCore.Http;

namespace IndustrialInventory.Api.Tests.Security;

public class ExactOriginMiddlewareTests
{
    private static readonly ExactOriginAllowlist Allowlist =
        new(["http://localhost:3000", "http://localhost:5000", "https://inventory.example.test"]);

    [Theory]
    [InlineData("http://localhost:3000")]
    [InlineData("http://localhost:5000")]
    [InlineData("https://inventory.example.test")]
    public async Task Exact_configured_origin_is_allowed(string origin)
    {
        var called = false;
        var context = CreateContext("/api/inventory", "POST", origin);
        var middleware = new ExactOriginMiddleware(_ =>
        {
            called = true;
            return Task.CompletedTask;
        }, Allowlist);

        await middleware.InvokeAsync(context);

        Assert.True(called);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Theory]
    [InlineData("http://localhost.evil:3000")]
    [InlineData("http://localhost:3001")]
    [InlineData("http://localhost:3000/path")]
    [InlineData("null")]
    public async Task Mismatched_or_non_origin_values_are_rejected(string origin)
    {
        var called = false;
        var context = CreateContext("/api/inventory", "POST", origin);
        var middleware = new ExactOriginMiddleware(_ =>
        {
            called = true;
            return Task.CompletedTask;
        }, Allowlist);

        await middleware.InvokeAsync(context);

        Assert.False(called);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.Contains("ORIGIN_NOT_ALLOWED", await ReadBody(context));
    }

    [Fact]
    public async Task Missing_origin_is_rejected()
    {
        var context = CreateContext("/api/auth/login", "POST", null);
        var middleware = new ExactOriginMiddleware(_ => Task.CompletedTask, Allowlist);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task Multiple_origins_are_rejected()
    {
        var context = CreateContext("/api/auth/login", "POST", null);
        context.Request.Headers.Append("Origin", "http://localhost:3000");
        context.Request.Headers.Append("Origin", "http://localhost:5000");
        var middleware = new ExactOriginMiddleware(_ => Task.CompletedTask, Allowlist);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task Safe_reads_and_non_api_requests_do_not_require_origin()
    {
        var middleware = new ExactOriginMiddleware(_ => Task.CompletedTask, Allowlist);
        var read = CreateContext("/api/inventory", "GET", null);
        var nonApiWrite = CreateContext("/health", "POST", null);

        await middleware.InvokeAsync(read);
        await middleware.InvokeAsync(nonApiWrite);

        Assert.Equal(StatusCodes.Status200OK, read.Response.StatusCode);
        Assert.Equal(StatusCodes.Status200OK, nonApiWrite.Response.StatusCode);
    }

    [Theory]
    [InlineData("http", "inventory-server:8080", "http://inventory-server:8080")]
    [InlineData("http", "INVENTORY-SERVER", "http://inventory-server")]
    [InlineData("https", "inventory.example.test", "https://inventory.example.test:443")]
    public async Task Same_origin_is_allowed_without_configuration(string scheme, string host, string origin)
    {
        var called = false;
        var context = CreateContext("/api/inventory", "POST", origin, scheme, host);
        var middleware = new ExactOriginMiddleware(_ =>
        {
            called = true;
            return Task.CompletedTask;
        }, new ExactOriginAllowlist([]));

        await middleware.InvokeAsync(context);

        Assert.True(called);
    }

    [Theory]
    [InlineData("http://evil.example.test")]
    [InlineData("http://inventory-server:8081")]
    [InlineData("https://inventory-server:8080")]
    public async Task Other_origin_is_rejected_without_configuration(string origin)
    {
        var context = CreateContext("/api/inventory", "POST", origin, "http", "inventory-server:8080");
        var middleware = new ExactOriginMiddleware(_ => Task.CompletedTask, new ExactOriginAllowlist([]));

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public void Invalid_allowlist_entries_are_rejected_at_startup()
    {
        Assert.Throws<InvalidOperationException>(() => new ExactOriginAllowlist(["https://example.test/path"]));
        Assert.Throws<InvalidOperationException>(() => new ExactOriginAllowlist(["*"]));
    }

    private static DefaultHttpContext CreateContext(
        string path, string method, string? origin, string scheme = "http", string host = "api.internal:5000")
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Method = method;
        context.Request.Scheme = scheme;
        context.Request.Host = new HostString(host);
        context.Response.Body = new MemoryStream();
        if (origin is not null) context.Request.Headers["Origin"] = origin;
        return context;
    }

    private static async Task<string> ReadBody(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        return await reader.ReadToEndAsync();
    }
}
