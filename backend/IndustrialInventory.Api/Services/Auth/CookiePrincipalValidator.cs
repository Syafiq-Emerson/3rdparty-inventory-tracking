using System.Security.Claims;
using IndustrialInventory.Api.Data;
using IndustrialInventory.Api.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace IndustrialInventory.Api.Services.Auth;

public interface IAppUserAccountLookup
{
    Task<AppUser?> FindByIdAsync(string id, CancellationToken cancellationToken);
}

public sealed class EfAppUserAccountLookup(InventoryDbContext db) : IAppUserAccountLookup
{
    public Task<AppUser?> FindByIdAsync(string id, CancellationToken cancellationToken) =>
        db.AppUsers.AsNoTracking().SingleOrDefaultAsync(user => user.Id == id, cancellationToken);
}

public sealed class CookiePrincipalValidator(IAppUserAccountLookup accountLookup)
{
    public async Task<bool> IsValidAsync(ClaimsPrincipal? principal, CancellationToken cancellationToken)
    {
        var userId = principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var roleClaims = principal?.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray();
        if (string.IsNullOrWhiteSpace(userId) || roleClaims is not { Length: 1 })
        {
            return false;
        }

        var user = await accountLookup.FindByIdAsync(userId, cancellationToken);
        return user is { IsActive: true } &&
            UserRole.All.Contains(user.Role, StringComparer.Ordinal) &&
            string.Equals(roleClaims[0], user.Role, StringComparison.Ordinal);
    }
}

public sealed class AppCookieAuthenticationEvents(CookiePrincipalValidator validator) : CookieAuthenticationEvents
{
    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return context.Response.WriteAsJsonAsync(new { error = "Sign in to continue." });
    }

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return context.Response.WriteAsJsonAsync(new { error = "Your account does not have access to this." });
    }

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        if (await validator.IsValidAsync(context.Principal, context.HttpContext.RequestAborted))
        {
            return;
        }

        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
