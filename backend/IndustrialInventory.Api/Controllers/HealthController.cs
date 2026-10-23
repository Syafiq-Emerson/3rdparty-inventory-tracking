using IndustrialInventory.Api.Data;
using IndustrialInventory.Api.DTOs;
using IndustrialInventory.Api.Services.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IndustrialInventory.Api.Controllers;

[ApiController]
[AllowAnonymous]
public sealed class HealthController(InventoryDbContext db, ILogger<HealthController> logger) : ControllerBase
{
    private static readonly TimeSpan ReadinessTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Liveness. Touches nothing, so the client can tell "server up" from "database down".</summary>
    [HttpGet("api/health")]
    public ActionResult Health() => Ok(new { status = "ok" });

    [HttpGet("api/health/ready")]
    public async Task<ActionResult> Ready(CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(ReadinessTimeout);

        try
        {
            await db.Database.ExecuteSqlRawAsync("SELECT 1", timeout.Token);
            return Ok(new { status = "ready" });
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Readiness check could not reach the database.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                error = "The database is not reachable. The app cannot serve data until it is back."
            });
        }
    }

    [HttpGet("api/system/version")]
    public ActionResult<VersionDto> Version() => new VersionDto(AppVersion.Current, AppVersion.BuildDate);
}
