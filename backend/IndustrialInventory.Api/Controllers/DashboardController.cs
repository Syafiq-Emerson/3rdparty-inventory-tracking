using IndustrialInventory.Api.Data;
using IndustrialInventory.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IndustrialInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class DashboardController(InventoryDbContext db) : ControllerBase
{
    private const int RecentItemCount = 6;

    internal sealed record ItemCounts(int Total, int Issued);

    // GET: /api/dashboard/summary
    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryResponse>> GetSummary(CancellationToken ct)
    {
        var counts = await ItemCountsQuery(db).FirstOrDefaultAsync(ct) ?? new ItemCounts(0, 0);
        var activeLocationsCount = await db.StoreLocations.CountAsync(l => l.IsActive, ct);
        var activePersonnelCount = await db.Personnel.CountAsync(p => p.IsActive, ct);
        var locations = await LocationCountsQuery(db).ToListAsync(ct);
        var recentItems = await RecentItemsQuery(db).ToListAsync(ct);

        return Ok(new DashboardSummaryResponse
        {
            TotalRegisteredUnits = counts.Total,
            AvailableUnits = counts.Total - counts.Issued,
            IssuedForPackingUnits = counts.Issued,
            ActiveStoreLocationsCount = activeLocationsCount,
            ActivePersonnelCount = activePersonnelCount,
            InventoryByStoreLocation = locations,
            RecentlyRegisteredInventory = recentItems
        });
    }

    // One round trip for both totals.
    internal static IQueryable<ItemCounts> ItemCountsQuery(InventoryDbContext db) =>
        db.InventoryItems
            .GroupBy(_ => 1)
            .Select(g => new ItemCounts(g.Count(), g.Count(i => i.IsIssued)));

    internal static IQueryable<LocationInventoryCountDto> LocationCountsQuery(InventoryDbContext db) =>
        db.StoreLocations
            .AsNoTracking()
            .Select(l => new LocationInventoryCountDto
            {
                LocationId = l.Id,
                LocationCode = l.LocationCode,
                LocationName = l.LocationName,
                IsActive = l.IsActive,
                ItemCount = l.InventoryItems.Count(i => !i.IsIssued)
            })
            .OrderByDescending(l => l.ItemCount);

    internal static IQueryable<InventoryItemResponse> RecentItemsQuery(InventoryDbContext db) =>
        db.InventoryItems
            .AsNoTracking()
            .OrderByDescending(i => i.CreatedAtUtc)
            .Take(RecentItemCount)
            .Select(InventoryController.ItemProjection);
}
