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

    // GET: /api/inventory/identify?serialNumber={serialNumber}&tagNumber={tagNumber}
    [HttpGet("identify")]
    public async Task<ActionResult<InventoryItemIdentificationResponse>> Identify(
        [FromQuery] string? serialNumber,
        [FromQuery] string? tagNumber)
    {
        if (string.IsNullOrWhiteSpace(serialNumber) || string.IsNullOrWhiteSpace(tagNumber))
        {
            return BadRequest(new { message = "Both serial number and tag number are required." });
        }

        var serialClean = serialNumber.Trim();
        var tagClean = tagNumber.Trim();
        if (serialClean.Length > 100 || tagClean.Length > 100)
        {
            return BadRequest(new { message = "Serial number and tag number must not exceed 100 characters." });
        }

        var item = await _context.InventoryItems
            .Include(i => i.Location)
            .AsNoTracking()
            .FirstOrDefaultAsync(i =>
                i.SerialNumber == serialClean &&
                i.TagNumber == tagClean);

        if (item == null)
        {
            return NotFound(new { message = "No inventory item matches both identifiers." });
        }

        return Ok(MapToIdentificationResponse(item));
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
        var itemId = Guid.NewGuid().ToString();
        var movementId = Guid.NewGuid().ToString();
        var executionStrategy = _context.Database.CreateExecutionStrategy();
        try
        {
            var created = await executionStrategy.ExecuteAsync(async () =>
            {
                _context.ChangeTracker.Clear();

                var existingAttempt = await _context.InventoryItems
                    .Include(i => i.Location)
                    .Include(i => i.RegisteredByPersonnel)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(i => i.Id == itemId);

                if (existingAttempt != null)
                {
                    var movementExists = await _context.InventoryMovements
                        .AsNoTracking()
                        .AnyAsync(m => m.Id == movementId &&
                                       m.InventoryItemId == itemId &&
                                       m.FromLocationId == null &&
                                       m.ToLocationId == location.Id &&
                                       m.PerformedByPersonnelId == personnel.Id &&
                                       m.EventType == "INITIAL_REGISTRATION");

                    if (!movementExists ||
                        existingAttempt.SerialNumber != serialClean ||
                        existingAttempt.TagNumber != tagClean ||
                        existingAttempt.LocationId != location.Id ||
                        existingAttempt.RegisteredByPersonnelId != personnel.Id)
                    {
                        throw new InvalidOperationException(
                            "The prior registration attempt did not persist the expected item and initial movement.");
                    }

                    return MapToResponse(existingAttempt);
                }

                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var newItem = new InventoryItem
                    {
                        Id = itemId,
                        SerialNumber = serialClean,
                        TagNumber = tagClean,
                        LocationId = location.Id,
                        RegisteredByPersonnelId = personnel.Id,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    var initialMovement = new InventoryMovement
                    {
                        Id = movementId,
                        InventoryItemId = newItem.Id,
                        FromLocationId = null,
                        ToLocationId = location.Id,
                        PerformedByPersonnelId = personnel.Id,
                        EventType = "INITIAL_REGISTRATION",
                        MovedAt = DateTime.UtcNow
                    };

                    _context.InventoryItems.Add(newItem);
                    _context.InventoryMovements.Add(initialMovement);

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return new InventoryItemResponse
                    {
                        Id = newItem.Id,
                        SerialNumber = newItem.SerialNumber,
                        TagNumber = newItem.TagNumber,
                        LocationId = newItem.LocationId,
                        LocationCode = location.LocationCode,
                        LocationName = location.LocationName,
                        RegisteredByPersonnelId = newItem.RegisteredByPersonnelId,
                        RegisteredByPersonnelCode = personnel.PersonnelCode,
                        RegisteredByPersonnelName = personnel.Name,
                        CreatedAt = newItem.CreatedAt,
                        UpdatedAt = newItem.UpdatedAt
                    };
                }
                catch
                {
                    try
                    {
                        await transaction.RollbackAsync();
                    }
                    catch (Exception rollbackException)
                    {
                        _logger.LogError(rollbackException, "Failed to roll back inventory registration transaction.");
                    }
                    _context.ChangeTracker.Clear();
                    throw;
                }
            });

            _logger.LogInformation("Item successfully registered: Serial={Serial}, Tag={Tag}, Loc={Loc}, By={By}",
                created.SerialNumber, created.TagNumber, location.LocationCode, personnel.PersonnelCode);

            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (DbUpdateException dbEx)
        {
            _logger.LogError(dbEx, "Database concurrency or constraint error registering item.");
            return Conflict(new { message = "Registration failed due to a concurrent database constraint violation. Please verify identifier uniqueness." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error registering item.");
            return StatusCode(500, new { message = "An internal server error occurred while persisting the inventory item." });
        }
    }

    // POST: /api/inventory/issue
    [HttpPost("issue")]
    public async Task<ActionResult<IssueInventoryResponse>> IssueForPacking(
        [FromBody] IssueInventoryRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var serialClean = request.SerialNumber.Trim();
        var tagClean = request.TagNumber.Trim();
        var personnelId = request.PerformedByPersonnelId.Trim();
        if (string.IsNullOrWhiteSpace(serialClean) ||
            string.IsNullOrWhiteSpace(tagClean) ||
            string.IsNullOrWhiteSpace(personnelId))
        {
            return BadRequest(new { message = "Serial number, tag number, and responsible personnel are required." });
        }

        var movementId = Guid.NewGuid().ToString();
        var issuedAt = DateTime.UtcNow;
        var executionStrategy = _context.Database.CreateExecutionStrategy();

        try
        {
            var result = await executionStrategy.ExecuteAsync(async () =>
            {
                _context.ChangeTracker.Clear();
                await using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    var item = await _context.InventoryItems
                        .Include(i => i.Location)
                        .AsNoTracking()
                        .FirstOrDefaultAsync(i =>
                            i.SerialNumber == serialClean &&
                            i.TagNumber == tagClean);

                    if (item == null)
                    {
                        await transaction.CommitAsync();
                        return new IssueAttemptResult(
                            null,
                            StatusCodes.Status404NotFound,
                            "No inventory item matches both identifiers.");
                    }

                    var personnel = await _context.Personnel
                        .AsNoTracking()
                        .FirstOrDefaultAsync(p => p.Id == personnelId);

                    if (personnel == null || !personnel.IsActive)
                    {
                        await transaction.CommitAsync();
                        return new IssueAttemptResult(
                            null,
                            StatusCodes.Status400BadRequest,
                            "Responsible personnel does not exist or is inactive.");
                    }

                    if (item.IsIssued)
                    {
                        var previousAttempt = await _context.InventoryMovements
                            .AsNoTracking()
                            .FirstOrDefaultAsync(m => m.Id == movementId);

                        if (previousAttempt != null &&
                            previousAttempt.InventoryItemId == item.Id &&
                            previousAttempt.FromLocationId == item.LocationId &&
                            previousAttempt.ToLocationId == null &&
                            previousAttempt.PerformedByPersonnelId == personnel.Id &&
                            previousAttempt.EventType == "ISSUE_FOR_PACKING")
                        {
                            await transaction.CommitAsync();
                            return new IssueAttemptResult(
                                CreateIssueResponse(item, personnel, movementId, previousAttempt.MovedAt),
                                null,
                                null);
                        }

                        await transaction.CommitAsync();
                        return new IssueAttemptResult(
                            null,
                            StatusCodes.Status409Conflict,
                            "This inventory item has already been issued.");
                    }

                    var updatedRows = await _context.InventoryItems
                        .Where(i =>
                            i.Id == item.Id &&
                            i.SerialNumber == serialClean &&
                            i.TagNumber == tagClean &&
                            !i.IsIssued)
                        .ExecuteUpdateAsync(update => update
                            .SetProperty(i => i.IsIssued, true)
                            .SetProperty(i => i.UpdatedAt, issuedAt));

                    if (updatedRows != 1)
                    {
                        await transaction.CommitAsync();
                        return new IssueAttemptResult(
                            null,
                            StatusCodes.Status409Conflict,
                            "This inventory item has already been issued.");
                    }

                    var movement = new InventoryMovement
                    {
                        Id = movementId,
                        InventoryItemId = item.Id,
                        FromLocationId = item.LocationId,
                        ToLocationId = null,
                        PerformedByPersonnelId = personnel.Id,
                        EventType = "ISSUE_FOR_PACKING",
                        MovedAt = issuedAt
                    };

                    _context.InventoryMovements.Add(movement);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    item.IsIssued = true;
                    item.UpdatedAt = issuedAt;
                    return new IssueAttemptResult(
                        CreateIssueResponse(item, personnel, movement.Id, movement.MovedAt),
                        null,
                        null);
                }
                catch
                {
                    try
                    {
                        await transaction.RollbackAsync();
                    }
                    catch (Exception rollbackException)
                    {
                        _logger.LogError(rollbackException, "Failed to roll back inventory issue transaction.");
                    }

                    _context.ChangeTracker.Clear();
                    throw;
                }
            });

            if (result.ErrorStatusCode.HasValue)
            {
                var error = new { message = result.ErrorMessage };
                return result.ErrorStatusCode.Value switch
                {
                    StatusCodes.Status400BadRequest => BadRequest(error),
                    StatusCodes.Status404NotFound => NotFound(error),
                    StatusCodes.Status409Conflict => Conflict(error),
                    _ => StatusCode(StatusCodes.Status500InternalServerError,
                        new { message = "An internal server error occurred while issuing the inventory item." })
                };
            }

            if (result.Response == null)
            {
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "An internal server error occurred while issuing the inventory item." });
            }

            return CreatedAtAction(
                nameof(GetItemHistory),
                new { id = result.Response.Item.Id },
                result.Response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error issuing item for packing.");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "An internal server error occurred while issuing the inventory item." });
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
                ToLocationName = m.ToLocation != null ? m.ToLocation.LocationName : null,
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
            IsIssued = item.IsIssued,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt
        };
    }

    private static InventoryItemIdentificationResponse MapToIdentificationResponse(InventoryItem item)
    {
        return new InventoryItemIdentificationResponse
        {
            Id = item.Id,
            SerialNumber = item.SerialNumber,
            TagNumber = item.TagNumber,
            LocationId = item.LocationId,
            LocationCode = item.Location.LocationCode,
            LocationName = item.Location.LocationName,
            IsIssued = item.IsIssued
        };
    }

    private static IssueInventoryResponse CreateIssueResponse(
        InventoryItem item,
        Personnel personnel,
        string movementId,
        DateTime movedAt)
    {
        return new IssueInventoryResponse
        {
            Item = MapToIdentificationResponse(item),
            Movement = new InventoryMovementResponse
            {
                Id = movementId,
                InventoryItemId = item.Id,
                FromLocationId = item.LocationId,
                FromLocationName = item.Location.LocationName,
                ToLocationId = null,
                ToLocationName = null,
                PerformedByPersonnelId = personnel.Id,
                PerformedByPersonnelName = personnel.Name,
                EventType = "ISSUE_FOR_PACKING",
                MovedAt = movedAt
            }
        };
    }

    private sealed record IssueAttemptResult(
        IssueInventoryResponse? Response,
        int? ErrorStatusCode,
        string? ErrorMessage);
}
