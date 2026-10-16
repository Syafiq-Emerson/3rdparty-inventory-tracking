using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using IndustrialInventory.Api.Data;
using IndustrialInventory.Api.DTOs;
using IndustrialInventory.Api.Models;

namespace IndustrialInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LocationsController : ControllerBase
{
    private readonly InventoryDbContext _context;

    public LocationsController(InventoryDbContext context)
    {
        _context = context;
    }

    // GET: /api/locations
    [HttpGet]
    public async Task<ActionResult<IEnumerable<StoreLocationResponse>>> GetLocations([FromQuery] bool? activeOnly = null)
    {
        var query = _context.StoreLocations.AsNoTracking();

        if (activeOnly.HasValue && activeOnly.Value)
        {
            query = query.Where(l => l.IsActive);
        }

        var locations = await query
            .OrderBy(l => l.LocationCode)
            .Select(l => new StoreLocationResponse
            {
                Id = l.Id,
                LocationCode = l.LocationCode,
                LocationName = l.LocationName,
                IsActive = l.IsActive,
                ItemCount = l.InventoryItems.Count,
                CreatedAt = l.CreatedAt,
                UpdatedAt = l.UpdatedAt
            })
            .ToListAsync();

        return Ok(locations);
    }

    // POST: /api/locations
    [HttpPost]
    public async Task<ActionResult<StoreLocationResponse>> CreateLocation([FromBody] CreateStoreLocationRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var codeClean = request.LocationCode.Trim();
        var exists = await _context.StoreLocations.AnyAsync(l => l.LocationCode == codeClean);
        if (exists)
        {
            return Conflict(new { message = $"Location code '{codeClean}' already exists." });
        }

        var newLocation = new StoreLocation
        {
            Id = Guid.NewGuid().ToString(),
            LocationCode = codeClean,
            LocationName = request.LocationName.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.StoreLocations.Add(newLocation);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetLocations), new { id = newLocation.Id }, new StoreLocationResponse
        {
            Id = newLocation.Id,
            LocationCode = newLocation.LocationCode,
            LocationName = newLocation.LocationName,
            IsActive = newLocation.IsActive,
            ItemCount = 0,
            CreatedAt = newLocation.CreatedAt,
            UpdatedAt = newLocation.UpdatedAt
        });
    }

    // PUT: /api/locations/{id}
    [HttpPut("{id}")]
    public async Task<ActionResult<StoreLocationResponse>> UpdateLocation(string id, [FromBody] UpdateStoreLocationRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var location = await _context.StoreLocations.Include(l => l.InventoryItems).FirstOrDefaultAsync(l => l.Id == id);
        if (location == null) return NotFound(new { message = $"Location with ID '{id}' was not found." });

        var codeClean = request.LocationCode.Trim();
        var codeInUse = await _context.StoreLocations.AnyAsync(l => l.LocationCode == codeClean && l.Id != id);
        if (codeInUse)
        {
            return Conflict(new { message = $"Location code '{codeClean}' is already in use by another location." });
        }

        location.LocationCode = codeClean;
        location.LocationName = request.LocationName.Trim();
        location.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new StoreLocationResponse
        {
            Id = location.Id,
            LocationCode = location.LocationCode,
            LocationName = location.LocationName,
            IsActive = location.IsActive,
            ItemCount = location.InventoryItems.Count,
            CreatedAt = location.CreatedAt,
            UpdatedAt = location.UpdatedAt
        });
    }

    // PATCH: /api/locations/{id}/status
    [HttpPatch("{id}/status")]
    public async Task<ActionResult<StoreLocationResponse>> UpdateStatus(string id, [FromBody] UpdateStatusRequest request)
    {
        var location = await _context.StoreLocations.Include(l => l.InventoryItems).FirstOrDefaultAsync(l => l.Id == id);
        if (location == null) return NotFound(new { message = $"Location with ID '{id}' was not found." });

        location.IsActive = request.IsActive;
        location.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new StoreLocationResponse
        {
            Id = location.Id,
            LocationCode = location.LocationCode,
            LocationName = location.LocationName,
            IsActive = location.IsActive,
            ItemCount = location.InventoryItems.Count,
            CreatedAt = location.CreatedAt,
            UpdatedAt = location.UpdatedAt
        });
    }
}
