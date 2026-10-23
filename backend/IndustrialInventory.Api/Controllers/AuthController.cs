using IndustrialInventory.Api.Controllers.Shared;
using IndustrialInventory.Api.DTOs;
using IndustrialInventory.Api.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace IndustrialInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class AuthController(AuthService auth) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<SignedInUserResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken ct) =>
        (await auth.SignInAsync(request, ct)).ToActionResult();

    [HttpPost("logout")]
    public async Task<ActionResult> Logout(CancellationToken ct)
    {
        await auth.SignOutAsync(ct);
        return NoContent();
    }

    [HttpGet("me")]
    [AllowAnonymous]
    public async Task<ActionResult<SignedInUserResponse>> Me(CancellationToken ct) =>
        (await auth.GetCurrentUserAsync(ct)).ToActionResult();

    [HttpPost("change-password")]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<SignedInUserResponse>> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken ct) =>
        (await auth.ChangePasswordAsync(request, ct)).ToActionResult();
}
