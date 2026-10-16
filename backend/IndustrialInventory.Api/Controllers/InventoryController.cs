using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using IndustrialInventory.Api.Data;
using IndustrialInventory.Api.DTOs;
using IndustrialInventory.Api.Models;

namespace IndustrialInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InventoryController : ControllerBase
{
    private readonly InventoryDbContext _context;
    private readonly ILogger<InventoryController> _logger;

    public InventoryController(InventoryDbContext context, ILogger<InventoryController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // GET: /api/inventory
    [HttpGet]
    public async Task<ActionResult<IEnumerable<InventoryItemResponse>>> GetInventory(
        [FromQuery] string? locationId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var query = _context.InventoryItems
            .Include(i => i.Location)
            .Include(i => i.RegisteredByPersonnel)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(locationId))
        {
            query = query.Where(i => i.LocationId == locationId);
        }

        var items = await query
            .OrderByDescending(i => i.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => MapToResponse(i))
            .ToListAsync();

        return Ok(items);
    }

    // GET: /api/inventory/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<InventoryItemResponse>> GetById(string id)
    {
        var item = await _context.InventoryItems
            .Include(i => i.Location)
            .Include(i => i.RegisteredByPersonnel)
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == id);

        if (item == null)
        {
            return NotFound(new { message = $"Inventory item with ID '{id}' was not found." });
        }

        return Ok(MapToResponse(item));
    }

    // GET: /api/inventory/search?q={query}
    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<InventoryItemResponse>>> Search(
        [FromQuery] string q,
        [FromQuery] string? locationId = null)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return await GetInventory(locationId);
        }

        var searchTerm = q.Trim();

        var query = _context.InventoryItems
            .Include(i => i.Location)
            .Include(i => i.RegisteredByPersonnel)
            .AsNoTracking()
            .Where(i => EF.Functions.Like(i.SerialNumber, $"%{searchTerm}%") ||
                        EF.Functions.Like(i.TagNumber, $"%{searchTerm}%"));

        if (!string.IsNullOrWhiteSpace(locationId))
        {
            query = query.Where(i => i.LocationId == locationId);
        }

        var results = await query
            .OrderByDescending(i => i.CreatedAt)
            .Take(50)
            .Select(i => MapToResponse(i))
            .ToListAsync();

        return Ok(results);
    }

    // POST: /api/inventory
    [HttpPost]
    public async Task<ActionResult<InventoryItemResponse>> RegisterItem([FromBody] RegisterInventoryRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var serialClean = request.SerialNumber.Trim();
        var tagClean = request.TagNumber.Trim();

        // 1. Check for Duplicate Serial Number
        var existingSerial = await _context.InventoryItems
            .Include(i => i.Location)
            .Include(i => i.RegisteredByPersonnel)
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.SerialNumber == serialClean);

        if (existingSerial != null)
        {
            return Conflict(new DuplicateConflictResponse
            {
                ErrorCode = "DUPLICATE_IDENTIFIER",
                Message = $"Serial Number '{serialClean}' is already registered in the system.",
                ConflictingField = "SerialNumber",
                ConflictingValue = serialClean,
                ExistingItem = MapToResponse(existingSerial)
            });
        }

        // 2. Check for Duplicate Tag Number
        var existingTag = await _context.InventoryItems
            .Include(i => i.Location)
            .Include(i => i.RegisteredByPersonnel)
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.TagNumber == tagClean);

        if (existingTag != null)
        {
            return Conflict(new DuplicateConflictResponse
            {
                ErrorCode = "DUPLICATE_IDENTIFIER",
                Message = $"Tag Number '{tagClean}' is already registered in the system.",
                ConflictingField = "TagNumber",
                ConflictingValue = tagClean,
                ExistingItem = MapToResponse(existingTag)
            });
        }

        // 3. Verify Referenced Location
        var location = await _context.StoreLocations.FindAsync(request.StoreLocationId);
        if (location == null)
        {
            return BadRequest(new { message = $"Store location ID '{request.StoreLocationId}' does not exist." });
        }
        if (!location.IsActive)
        {
            return BadRequest(new { message = $"Store location '{location.LocationName}' ({location.LocationCode}) is inactive." });
        }

        // 4. Verify Referenced Personnel
        var personnel = await _context.Personnel.FindAsync(request.ScanByPersonnelId);
        if (personnel == null)
        {
            return BadRequest(new { message = $"Personnel ID '{request.ScanByPersonnelId}' does not exist." });
        }
        if (!personnel.IsActive)
        {
            return BadRequest(new { message = $"Personnel '{personnel.Name}' is inactive." });
        }

        // 5. Execute Atomic Transaction: Create InventoryItem + Initial Movement
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var newItem = new InventoryItem
            {
                Id = Guid.NewGuid().ToString(),
                SerialNumber = serialClean,
                TagNumber = tagClean,
                LocationId = location.Id,
                RegisteredByPersonnelId = personnel.Id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.InventoryItems.Add(newItem);

            var initialMovement = new InventoryMovement
            {
                Id = Guid.NewGuid().ToString(),
                InventoryItemId = newItem.Id,
                FromLocationId = null,
                ToLocationId = location.Id,
                PerformedByPersonnelId = personnel.Id,
                EventType = "INITIAL_REGISTRATION",
                MovedAt = DateTime.UtcNow
            };

            _context.InventoryMovements.Add(initialMovement);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Item successfully registered: Serial={Serial}, Tag={Tag}, Loc={Loc}, By={By}",
                newItem.SerialNumber, newItem.TagNumber, location.LocationCode, personnel.PersonnelCode);

            // Fetch populated for clean response
            var created = await _context.InventoryItems
                .Include(i => i.Location)
                .Include(i => i.RegisteredByPersonnel)
                .FirstAsync(i => i.Id == newItem.Id);

            return CreatedAtAction(nameof(GetById), new { id = created.Id }, MapToResponse(created));
        }
        catch (DbUpdateException dbEx)
        {
            await transaction.RollbackAsync();
            _logger.LogError(dbEx, "Database concurrency or constraint error registering item.");
            return Conflict(new { message = "Registration failed due to a concurrent database constraint violation. Please verify identifier uniqueness." });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Unexpected error registering item.");
            return StatusCode(500, new { message = "An internal server error occurred while persisting the inventory item." });
        }
    }

    // GET: /api/inventory/{id}/history
    [HttpGet("{id}/history")]
    public async Task<ActionResult<IEnumerable<InventoryMovementResponse>>> GetItemHistory(string id)
    {
        var itemExists = await _context.InventoryItems.AnyAsync(i => i.Id == id);
        if (!itemExists)
        {
            return NotFound(new { message = $"Inventory item '{id}' does not exist." });
        }

        var history = await _context.InventoryMovements
            .Include(m => m.FromLocation)
            .Include(m => m.ToLocation)
            .Include(m => m.PerformedByPersonnel)
            .Where(m => m.InventoryItemId == id)
            .OrderByDescending(m => m.MovedAt)
            .Select(m => new InventoryMovementResponse
            {
                Id = m.Id,
                InventoryItemId = m.InventoryItemId,
                FromLocationId = m.FromLocationId,
                FromLocationName = m.FromLocation != null ? m.FromLocation.LocationName : null,
                ToLocationId = m.ToLocationId,
                ToLocationName = m.ToLocation.LocationName,
                PerformedByPersonnelId = m.PerformedByPersonnelId,
                PerformedByPersonnelName = m.PerformedByPersonnel.Name,
                EventType = m.EventType,
                MovedAt = m.MovedAt
            })
            .ToListAsync();

        return Ok(history);
    }

    private static InventoryItemResponse MapToResponse(InventoryItem item)
    {
        return new InventoryItemResponse
        {
            Id = item.Id,
            SerialNumber = item.SerialNumber,
            TagNumber = item.TagNumber,
            LocationId = item.LocationId,
            LocationCode = item.Location?.LocationCode ?? string.Empty,
            LocationName = item.Location?.LocationName ?? string.Empty,
            RegisteredByPersonnelId = item.RegisteredByPersonnelId,
            RegisteredByPersonnelCode = item.RegisteredByPersonnel?.PersonnelCode ?? string.Empty,
            RegisteredByPersonnelName = item.RegisteredByPersonnel?.Name ?? string.Empty,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt
        };
    }
}
