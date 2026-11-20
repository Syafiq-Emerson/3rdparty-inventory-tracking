using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace IndustrialInventory.Api.Services.Security;

public sealed class AntiforgeryAuthorizationFilter(IAntiforgery antiforgery) : IAsyncAuthorizationFilter
{
    private const string TokenHeaderName = "X-CSRF-TOKEN";

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var method = context.HttpContext.Request.Method;
        if (HttpMethods.IsGet(method) ||
            HttpMethods.IsHead(method) ||
            HttpMethods.IsOptions(method) ||
            HttpMethods.IsTrace(method))
        {
            return;
        }

        var tokenHeader = context.HttpContext.Request.Headers[TokenHeaderName];
        if (tokenHeader.Count != 1 ||
            string.IsNullOrWhiteSpace(tokenHeader[0]) ||
            !await antiforgery.IsRequestValidAsync(context.HttpContext))
        {
            context.Result = new ObjectResult(new
            {
                error = "The anti-forgery token is missing or invalid.",
                errorCode = "CSRF_VALIDATION_FAILED"
            })
            {
                StatusCode = StatusCodes.Status400BadRequest
            };
        }
    }
}
