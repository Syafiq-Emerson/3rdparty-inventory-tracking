using IndustrialInventory.Api.Controllers;
using IndustrialInventory.Api.Services.Security;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

namespace IndustrialInventory.Api.Tests.Security;

public class AntiforgeryAuthorizationFilterTests
{
    [Fact]
    public async Task Valid_header_and_cookie_are_accepted()
    {
        var antiforgery = new TestAntiforgery();
        var context = CreateContext("POST");
        context.HttpContext.Request.Headers["X-CSRF-TOKEN"] = "request-token";
        context.HttpContext.Request.Headers["Cookie"] = "IndustrialInventory.Antiforgery=cookie-token";

        await new AntiforgeryAuthorizationFilter(antiforgery).OnAuthorizationAsync(context);

        Assert.Null(context.Result);
        Assert.Equal(1, antiforgery.ValidationCalls);
    }

    [Fact]
    public async Task Missing_header_is_rejected_before_antiforgery_validation()
    {
        var antiforgery = new TestAntiforgery();
        var context = CreateContext("POST");

        await new AntiforgeryAuthorizationFilter(antiforgery).OnAuthorizationAsync(context);

        AssertCsrfRejected(context);
        Assert.Equal(0, antiforgery.ValidationCalls);
    }

    [Fact]
    public async Task Invalid_request_token_is_rejected()
    {
        var antiforgery = new TestAntiforgery();
        var context = CreateContext("POST");
        context.HttpContext.Request.Headers["X-CSRF-TOKEN"] = "invalid-token";
        context.HttpContext.Request.Headers["Cookie"] = "IndustrialInventory.Antiforgery=cookie-token";

        await new AntiforgeryAuthorizationFilter(antiforgery).OnAuthorizationAsync(context);

        AssertCsrfRejected(context);
        Assert.Equal(1, antiforgery.ValidationCalls);
    }

    [Fact]
    public async Task Safe_read_action_does_not_require_a_token()
    {
        var antiforgery = new TestAntiforgery();
        var context = CreateContext("GET");

        await new AntiforgeryAuthorizationFilter(antiforgery).OnAuthorizationAsync(context);

        Assert.Null(context.Result);
        Assert.Equal(0, antiforgery.ValidationCalls);
    }

    [Fact]
    public void Token_endpoint_sets_no_store_and_returns_request_token()
    {
        var antiforgery = new TestAntiforgery();
        var controller = new AuthController(null!, antiforgery)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = Assert.IsType<OkObjectResult>(controller.GetCsrfToken());

        Assert.Equal("no-store", controller.Response.Headers.CacheControl);
        Assert.Equal("no-cache", controller.Response.Headers.Pragma);
        Assert.Equal("request-token", result.Value!.GetType().GetProperty("requestToken")!.GetValue(result.Value));
        Assert.Equal(1, antiforgery.TokenIssueCalls);
    }

    private static AuthorizationFilterContext CreateContext(string method)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = method;
        httpContext.Response.Body = new MemoryStream();
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new AuthorizationFilterContext(actionContext, []);
    }

    private static void AssertCsrfRejected(AuthorizationFilterContext context)
    {
        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal(
            "CSRF_VALIDATION_FAILED",
            result.Value!.GetType().GetProperty("errorCode")!.GetValue(result.Value));
    }

    private sealed class TestAntiforgery : IAntiforgery
    {
        public int ValidationCalls { get; private set; }
        public int TokenIssueCalls { get; private set; }

        public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext)
        {
            TokenIssueCalls++;
            return new AntiforgeryTokenSet("request-token", "cookie-token", "__RequestVerificationToken", "X-CSRF-TOKEN");
        }

        public AntiforgeryTokenSet GetTokens(HttpContext httpContext) =>
            new("request-token", "cookie-token", "__RequestVerificationToken", "X-CSRF-TOKEN");

        public Task<bool> IsRequestValidAsync(HttpContext httpContext)
        {
            ValidationCalls++;
            return Task.FromResult(
                httpContext.Request.Headers["X-CSRF-TOKEN"] == "request-token" &&
                httpContext.Request.Cookies.TryGetValue("IndustrialInventory.Antiforgery", out var cookieToken) &&
                cookieToken == "cookie-token");
        }

        public async Task ValidateRequestAsync(HttpContext httpContext)
        {
            if (!await IsRequestValidAsync(httpContext))
            {
                throw new AntiforgeryValidationException("Invalid token.");
            }
        }

        public bool IsRequestValid(HttpContext httpContext) =>
            httpContext.Request.Headers["X-CSRF-TOKEN"] == "request-token";

        public void SetCookieTokenAndHeader(HttpContext httpContext) { }
    }
}
