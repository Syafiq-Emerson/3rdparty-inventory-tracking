using IndustrialInventory.Api.Data;
using IndustrialInventory.Api.DTOs;
using IndustrialInventory.Api.Models;
using IndustrialInventory.Api.Models.Audit;
using IndustrialInventory.Api.Services.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IndustrialInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class LocationsController(
    InventoryDbContext db,
    AuditWriter audit,
    TimeProvider timeProvider) : ControllerBase
{
    // GET: /api/locations
    [HttpGet]
    public async Task<ActionResult<IEnumerable<StoreLocationResponse>>> GetLocations(
        CancellationToken ct,
        [FromQuery] bool? activeOnly = null)
    {
        var query = db.StoreLocations.AsNoTracking();
        if (activeOnly is true) query = query.Where(l => l.IsActive);

        var locations = await query
            .OrderBy(l => l.LocationCode)
            .Select(Projection)
            .ToListAsync(ct);

        return Ok(locations);
    }

    // POST: /api/locations
    [HttpPost]
    public async Task<ActionResult<StoreLocationResponse>> CreateLocation(
        [FromBody] CreateStoreLocationRequest request,
        CancellationToken ct)
    {
        var codeClean = request.LocationCode.Trim();
        if (await db.StoreLocations.AnyAsync(l => l.LocationCode == codeClean, ct))
        {
            return Conflict(new { error = $"Location code '{codeClean}' already exists." });
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var newLocation = new StoreLocation
        {
            Id = Guid.NewGuid().ToString(),
            LocationCode = codeClean,
            LocationName = request.LocationName.Trim(),
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        db.StoreLocations.Add(newLocation);
        audit.Add(AuditAction.Create, AuditEntity.StoreLocation, newLocation.Id,
            newValue: new { newLocation.LocationCode, newLocation.LocationName },
            referenceNo: newLocation.LocationCode);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetLocations), new { id = newLocation.Id }, new StoreLocationResponse
        {
            Id = newLocation.Id,
            LocationCode = newLocation.LocationCode,
            LocationName = newLocation.LocationName,
            IsActive = newLocation.IsActive,
            ItemCount = 0,
            CreatedAt = newLocation.CreatedAtUtc,
            UpdatedAt = newLocation.UpdatedAtUtc
        });
    }

    // PUT: /api/locations/{id}
    [HttpPut("{id}")]
    public async Task<ActionResult<StoreLocationResponse>> UpdateLocation(
        string id,
        [FromBody] UpdateStoreLocationRequest request,
        CancellationToken ct)
    {
        var location = await db.StoreLocations.FirstOrDefaultAsync(l => l.Id == id, ct);
        if (location is null) return NotFound(new { error = $"Location with ID '{id}' was not found." });

        var codeClean = request.LocationCode.Trim();
        if (await db.StoreLocations.AnyAsync(l => l.LocationCode == codeClean && l.Id != id, ct))
        {
            return Conflict(new { error = $"Location code '{codeClean}' is already in use by another location." });
        }

        var before = new { location.LocationCode, location.LocationName };

        location.LocationCode = codeClean;
        location.LocationName = request.LocationName.Trim();
        location.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;

        audit.Add(AuditAction.Update, AuditEntity.StoreLocation, location.Id,
            previousValue: before,
            newValue: new { location.LocationCode, location.LocationName },
            referenceNo: location.LocationCode);
        await db.SaveChangesAsync(ct);

        return Ok(await ReadBackAsync(id, ct));
    }

    // PATCH: /api/locations/{id}/status
    [HttpPatch("{id}/status")]
    public async Task<ActionResult<StoreLocationResponse>> UpdateStatus(
        string id,
        [FromBody] UpdateStatusRequest request,
        CancellationToken ct)
    {
        var location = await db.StoreLocations.FirstOrDefaultAsync(l => l.Id == id, ct);
        if (location is null) return NotFound(new { error = $"Location with ID '{id}' was not found." });

        location.IsActive = request.IsActive;
        location.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;

        audit.Add(request.IsActive ? AuditAction.Restore : AuditAction.Deactivate,
            AuditEntity.StoreLocation, location.Id,
            previousValue: new { IsActive = !request.IsActive },
            newValue: new { request.IsActive },
            referenceNo: location.LocationCode);
        await db.SaveChangesAsync(ct);

        return Ok(await ReadBackAsync(id, ct));
    }

    private async Task<StoreLocationResponse> ReadBackAsync(string id, CancellationToken ct) =>
        await db.StoreLocations.AsNoTracking().Where(l => l.Id == id).Select(Projection).FirstAsync(ct);

    internal static readonly System.Linq.Expressions.Expression<Func<StoreLocation, StoreLocationResponse>> Projection =
        l => new StoreLocationResponse
        {
            Id = l.Id,
            LocationCode = l.LocationCode,
            LocationName = l.LocationName,
            IsActive = l.IsActive,
            ItemCount = l.InventoryItems.Count,
            CreatedAt = l.CreatedAtUtc,
            UpdatedAt = l.UpdatedAtUtc
        };
}
