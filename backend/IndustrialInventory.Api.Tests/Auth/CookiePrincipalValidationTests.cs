using System.Security.Claims;
using IndustrialInventory.Api.Models;
using IndustrialInventory.Api.Services.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace IndustrialInventory.Api.Tests.Auth;

public class CookiePrincipalValidationTests
{
    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Operator)]
    public async Task Active_account_with_matching_role_keeps_cookie(string role)
    {
        var user = ActiveUser(role);
        var signOut = new RecordingAuthenticationService();
        var context = CreateContext(Principal(role), signOut);
        var events = new AppCookieAuthenticationEvents(new CookiePrincipalValidator(new MemoryLookup(user)));

        await events.ValidatePrincipal(context);

        Assert.Null(signOut.SignedOutScheme);
    }

    [Fact]
    public async Task Deactivated_account_rejects_and_signs_out_cookie()
    {
        var user = ActiveUser(UserRole.Admin);
        user.IsActive = false;
        var signOut = new RecordingAuthenticationService();
        var context = CreateContext(Principal(UserRole.Admin), signOut);
        var events = new AppCookieAuthenticationEvents(new CookiePrincipalValidator(new MemoryLookup(user)));

        await events.ValidatePrincipal(context);

        Assert.Equal(CookieAuthenticationDefaults.AuthenticationScheme, signOut.SignedOutScheme);
    }

    [Fact]
    public async Task Role_change_rejects_and_signs_out_stale_cookie()
    {
        var signOut = new RecordingAuthenticationService();
        var context = CreateContext(Principal(UserRole.Admin), signOut);
        var events = new AppCookieAuthenticationEvents(
            new CookiePrincipalValidator(new MemoryLookup(ActiveUser(UserRole.Operator))));

        await events.ValidatePrincipal(context);

        Assert.Equal(CookieAuthenticationDefaults.AuthenticationScheme, signOut.SignedOutScheme);
    }

    [Fact]
    public async Task Missing_or_unsupported_account_rejects_cookie()
    {
        var missingSignOut = new RecordingAuthenticationService();
        var missingContext = CreateContext(Principal(UserRole.Admin), missingSignOut);
        var missingEvents = new AppCookieAuthenticationEvents(
            new CookiePrincipalValidator(new MemoryLookup((AppUser?)null)));

        await missingEvents.ValidatePrincipal(missingContext);

        Assert.Equal(CookieAuthenticationDefaults.AuthenticationScheme, missingSignOut.SignedOutScheme);

        var unsupportedSignOut = new RecordingAuthenticationService();
        var unsupportedContext = CreateContext(Principal("Other"), unsupportedSignOut);
        var unsupportedEvents = new AppCookieAuthenticationEvents(
            new CookiePrincipalValidator(new MemoryLookup(ActiveUser("Other"))));

        await unsupportedEvents.ValidatePrincipal(unsupportedContext);

        Assert.Equal(CookieAuthenticationDefaults.AuthenticationScheme, unsupportedSignOut.SignedOutScheme);
    }

    [Fact]
    public async Task Account_lookup_failure_propagates_and_does_not_accept_cookie()
    {
        var signOut = new RecordingAuthenticationService();
        var context = CreateContext(Principal(UserRole.Admin), signOut);
        var events = new AppCookieAuthenticationEvents(
            new CookiePrincipalValidator(new MemoryLookup(new InvalidOperationException("lookup failed"))));

        await Assert.ThrowsAsync<InvalidOperationException>(() => events.ValidatePrincipal(context));

        Assert.Null(signOut.SignedOutScheme);
    }

    [Fact]
    public async Task Missing_or_multiple_role_claims_reject_cookie()
    {
        var user = ActiveUser(UserRole.Admin);
        foreach (var principal in new[]
        {
            Principal(),
            Principal(UserRole.Admin, UserRole.Operator),
            new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Role, UserRole.Admin)],
                CookieAuthenticationDefaults.AuthenticationScheme))
        })
        {
            var signOut = new RecordingAuthenticationService();
            var context = CreateContext(principal, signOut);
            var events = new AppCookieAuthenticationEvents(new CookiePrincipalValidator(new MemoryLookup(user)));

            await events.ValidatePrincipal(context);

            Assert.Equal(CookieAuthenticationDefaults.AuthenticationScheme, signOut.SignedOutScheme);
        }
    }

    private static AppUser ActiveUser(string role) =>
        new() { Id = "user-1", UserName = "tester", Role = role, IsActive = true };

    private static ClaimsPrincipal Principal(params string[] roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "user-1"),
            new(ClaimTypes.Name, "tester")
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }

    private static CookieValidatePrincipalContext CreateContext(
        ClaimsPrincipal principal,
        RecordingAuthenticationService authenticationService)
    {
        var services = new ServiceCollection()
            .AddSingleton<IAuthenticationService>(authenticationService)
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = services };
        var scheme = new AuthenticationScheme(
            CookieAuthenticationDefaults.AuthenticationScheme,
            CookieAuthenticationDefaults.AuthenticationScheme,
            typeof(CookieAuthenticationHandler));
        var ticket = new AuthenticationTicket(
            principal,
            new AuthenticationProperties(),
            CookieAuthenticationDefaults.AuthenticationScheme);
        return new CookieValidatePrincipalContext(
            httpContext,
            scheme,
            new CookieAuthenticationOptions(),
            ticket);
    }

    private sealed class MemoryLookup : IAppUserAccountLookup
    {
        private readonly AppUser? user;
        private readonly Exception? error;

        public MemoryLookup(AppUser? user) => this.user = user;

        public MemoryLookup(Exception error) => this.error = error;

        public Task<AppUser?> FindByIdAsync(string id, CancellationToken cancellationToken) =>
            error is null
                ? Task.FromResult(user is not null && user.Id == id ? user : null)
                : Task.FromException<AppUser?>(error);
    }

    private sealed class RecordingAuthenticationService : IAuthenticationService
    {
        public string? SignedOutScheme { get; private set; }

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
            Task.FromResult(AuthenticateResult.NoResult());

        public Task ChallengeAsync(
            HttpContext context,
            string? scheme,
            AuthenticationProperties? properties) => Task.CompletedTask;

        public Task ForbidAsync(
            HttpContext context,
            string? scheme,
            AuthenticationProperties? properties) => Task.CompletedTask;

        public Task SignInAsync(
            HttpContext context,
            string? scheme,
            ClaimsPrincipal principal,
            AuthenticationProperties? properties) => Task.CompletedTask;

        public Task SignOutAsync(
            HttpContext context,
            string? scheme,
            AuthenticationProperties? properties)
        {
            SignedOutScheme = scheme;
            return Task.CompletedTask;
        }
    }
}
