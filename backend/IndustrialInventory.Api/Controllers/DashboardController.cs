using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using IndustrialInventory.Api.Data;
using IndustrialInventory.Api.DTOs;

namespace IndustrialInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly InventoryDbContext _context;

    public DashboardController(InventoryDbContext context)
    {
        _context = context;
    }

    // GET: /api/dashboard/summary
    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryResponse>> GetSummary()
    {
        var totalUnits = await _context.InventoryItems.CountAsync();
        var availableUnits = await _context.InventoryItems.CountAsync(i => !i.IsIssued);
        var issuedForPackingUnits = await _context.InventoryItems.CountAsync(i => i.IsIssued);
        var activeLocationsCount = await _context.StoreLocations.CountAsync(l => l.IsActive);
        var activePersonnelCount = await _context.Personnel.CountAsync(p => p.IsActive);

        var locations = await _context.StoreLocations
            .AsNoTracking()
            .Select(l => new LocationInventoryCountDto
            {
                LocationId = l.Id,
                LocationCode = l.LocationCode,
                LocationName = l.LocationName,
                IsActive = l.IsActive,
                ItemCount = l.InventoryItems.Count(i => !i.IsIssued)
            })
            .OrderByDescending(l => l.ItemCount)
            .ToListAsync();

        var recentItems = await _context.InventoryItems
            .Include(i => i.Location)
            .Include(i => i.RegisteredByPersonnel)
            .AsNoTracking()
            .OrderByDescending(i => i.CreatedAt)
            .Take(6)
            .Select(i => new InventoryItemResponse
            {
                Id = i.Id,
                SerialNumber = i.SerialNumber,
                TagNumber = i.TagNumber,
                LocationId = i.LocationId,
                LocationCode = i.Location.LocationCode,
                LocationName = i.Location.LocationName,
                RegisteredByPersonnelId = i.RegisteredByPersonnelId,
                RegisteredByPersonnelCode = i.RegisteredByPersonnel.PersonnelCode,
                RegisteredByPersonnelName = i.RegisteredByPersonnel.Name,
                IsIssued = i.IsIssued,
                CreatedAt = i.CreatedAt,
                UpdatedAt = i.UpdatedAt
            })
            .ToListAsync();

        return Ok(new DashboardSummaryResponse
        {
            TotalRegisteredUnits = totalUnits,
            AvailableUnits = availableUnits,
            IssuedForPackingUnits = issuedForPackingUnits,
            ActiveStoreLocationsCount = activeLocationsCount,
            ActivePersonnelCount = activePersonnelCount,
            InventoryByStoreLocation = locations,
            RecentlyRegisteredInventory = recentItems
        });
    }
}
