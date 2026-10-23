using System.Security.Claims;
using IndustrialInventory.Api.Data;
using IndustrialInventory.Api.DTOs;
using IndustrialInventory.Api.Models;
using IndustrialInventory.Api.Models.Audit;
using IndustrialInventory.Api.Services.Shared;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IndustrialInventory.Api.Services.Auth;

public sealed class AuthService(
    InventoryDbContext db,
    AuditWriter audit,
    TimeProvider timeProvider,
    IHttpContextAccessor accessor)
{
    public const int MinimumPasswordLength = 12;

    private static readonly PasswordHasher<AppUser> Hasher = new();

    /// <summary>Deliberately vague so a caller cannot tell which part was wrong.</summary>
    private const string SignInFailedMessage = "The user name or password is not correct.";

    public async Task<ServiceResult<SignedInUserResponse>> SignInAsync(
        LoginRequest request,
        CancellationToken ct)
    {
        var userName = request.UserName.Trim();

        var user = await db.AppUsers.FirstOrDefaultAsync(u => u.UserName == userName, ct);

        if (user is null || !user.IsActive)
        {
            audit.Add(AuditAction.LoginFailed, AuditEntity.AppUser, user?.Id,
                remarks: "Unknown or inactive account.", userNameOverride: Truncate(userName));
            await db.SaveChangesAsync(ct);
            return ServiceResult<SignedInUserResponse>.Fail(SignInFailedMessage, StatusCodes.Status401Unauthorized);
        }

        if (string.IsNullOrEmpty(user.PasswordHash))
        {
            return ServiceResult<SignedInUserResponse>.Fail(
                "This account has no password yet. Ask IT to run set-admin-password on the server, then sign in again.",
                StatusCodes.Status409Conflict);
        }

        var verification = Hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            audit.Add(AuditAction.LoginFailed, AuditEntity.AppUser, user.Id,
                remarks: "Wrong password.", userNameOverride: user.UserName);
            await db.SaveChangesAsync(ct);
            return ServiceResult<SignedInUserResponse>.Fail(SignInFailedMessage, StatusCodes.Status401Unauthorized);
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = Hasher.HashPassword(user, request.Password);
            user.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        }

        audit.Add(AuditAction.Login, AuditEntity.AppUser, user.Id, userNameOverride: user.UserName);
        await db.SaveChangesAsync(ct);

        await IssueCookieAsync(user);

        return ServiceResult<SignedInUserResponse>.Ok(ToResponse(user));
    }

    public async Task SignOutAsync(CancellationToken ct)
    {
        var httpContext = accessor.HttpContext;
        if (httpContext is null) return;

        var userName = httpContext.User.FindFirstValue(ClaimTypes.Name);
        if (!string.IsNullOrWhiteSpace(userName))
        {
            audit.Add(AuditAction.Logout, AuditEntity.AppUser,
                httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier), userNameOverride: userName);
            await db.SaveChangesAsync(ct);
        }

        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    public async Task<ServiceResult<SignedInUserResponse>> GetCurrentUserAsync(CancellationToken ct)
    {
        var userId = accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return ServiceResult<SignedInUserResponse>.Fail("Not signed in.", StatusCodes.Status401Unauthorized);
        }

        var user = await db.AppUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null || !user.IsActive)
        {
            return ServiceResult<SignedInUserResponse>.Fail("Not signed in.", StatusCodes.Status401Unauthorized);
        }

        return ServiceResult<SignedInUserResponse>.Ok(ToResponse(user));
    }

    public async Task<ServiceResult<SignedInUserResponse>> ChangePasswordAsync(
        ChangePasswordRequest request,
        CancellationToken ct)
    {
        var userId = accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return ServiceResult<SignedInUserResponse>.Fail("Not signed in.", StatusCodes.Status401Unauthorized);
        }

        var user = await db.AppUsers.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null || !user.IsActive)
        {
            return ServiceResult<SignedInUserResponse>.Fail("Not signed in.", StatusCodes.Status401Unauthorized);
        }

        if (Hasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword)
            == PasswordVerificationResult.Failed)
        {
            return ServiceResult<SignedInUserResponse>.Fail(
                "The current password is not correct.", StatusCodes.Status401Unauthorized);
        }

        var invalid = DescribePasswordProblem(request.NewPassword);
        if (invalid is not null) return ServiceResult<SignedInUserResponse>.Fail(invalid);

        user.PasswordHash = Hasher.HashPassword(user, request.NewPassword);
        user.MustChangePassword = false;
        user.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;

        // The password itself is never written to the audit row.
        audit.Add(AuditAction.PasswordChanged, AuditEntity.AppUser, user.Id, userNameOverride: user.UserName);
        await db.SaveChangesAsync(ct);

        await IssueCookieAsync(user);

        return ServiceResult<SignedInUserResponse>.Ok(ToResponse(user));
    }

    /// <summary>Returns a message when the password is not acceptable, otherwise null.</summary>
    public static string? DescribePasswordProblem(string? password) =>
        string.IsNullOrWhiteSpace(password)
            ? "Enter a new password."
            : password.Length < MinimumPasswordLength
                ? $"The new password must be at least {MinimumPasswordLength} characters."
                : null;

    public static string HashPasswordFor(AppUser user, string password) => Hasher.HashPassword(user, password);

    private async Task IssueCookieAsync(AppUser user)
    {
        var httpContext = accessor.HttpContext;
        if (httpContext is null) return;

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.UserName),
                new Claim(ClaimTypes.Role, user.Role)
            ],
            CookieAuthenticationDefaults.AuthenticationScheme);

        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true });
    }

    private static SignedInUserResponse ToResponse(AppUser user) =>
        new(user.Id, user.UserName, user.Role, user.MustChangePassword);

    private static string Truncate(string value) => value.Length <= 50 ? value : value[..50];
}
