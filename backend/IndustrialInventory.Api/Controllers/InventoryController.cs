using IndustrialInventory.Api.Data;
using IndustrialInventory.Api.DTOs;
using IndustrialInventory.Api.Models;
using IndustrialInventory.Api.Models.Audit;
using IndustrialInventory.Api.Services.Inventory;
using IndustrialInventory.Api.Services.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IndustrialInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class InventoryController(
    InventoryDbContext db,
    AuditWriter audit,
    IdempotencyGate idempotency,
    TimeProvider timeProvider,
    ILogger<InventoryController> logger) : ControllerBase
{
    private const int SearchResultLimit = 50;
    private const string IdempotencyHeader = "Idempotency-Key";

    private const string ConcurrentWriteMessage =
        "Someone else saved a change at the same moment, so this was not recorded. Please try again.";

    // GET: /api/inventory
    [HttpGet]
    public async Task<ActionResult<PagedResponse<InventoryItemResponse>>> GetInventory(
        CancellationToken ct,
        [FromQuery] string? locationId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        (page, pageSize) = InventoryRules.ClampPaging(page, pageSize);

        var rows = await PageQuery(db, locationId, page, pageSize).ToListAsync(ct);

        var hasMore = rows.Count > pageSize;
        if (hasMore) rows.RemoveAt(rows.Count - 1);

        return Ok(new PagedResponse<InventoryItemResponse>(rows, hasMore));
    }

    // GET: /api/inventory/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<InventoryItemResponse>> GetById(string id, CancellationToken ct)
    {
        var item = await db.InventoryItems
            .AsNoTracking()
            .Where(i => i.Id == id)
            .Select(ItemProjection)
            .FirstOrDefaultAsync(ct);

        return item is null
            ? NotFound(new { error = $"Inventory item with ID '{id}' was not found." })
            : Ok(item);
    }

    // GET: /api/inventory/identify?serialNumber={serialNumber}&tagNumber={tagNumber}
    [HttpGet("identify")]
    public async Task<ActionResult<InventoryItemIdentificationResponse>> Identify(
        CancellationToken ct,
        [FromQuery] string? serialNumber = null,
        [FromQuery] string? tagNumber = null)
    {
        if (string.IsNullOrWhiteSpace(serialNumber) || string.IsNullOrWhiteSpace(tagNumber))
        {
            return BadRequest(new { error = "Both serial number and tag number are required." });
        }

        var serialClean = serialNumber.Trim();
        var tagClean = tagNumber.Trim();
        if (serialClean.Length > InventoryRules.IdentifierMaxLength || tagClean.Length > InventoryRules.IdentifierMaxLength)
        {
            return BadRequest(new
            {
                error = $"Serial number and tag number must not exceed {InventoryRules.IdentifierMaxLength} characters."
            });
        }

        var item = await db.InventoryItems
            .AsNoTracking()
            .Where(i => i.SerialNumber == serialClean && i.TagNumber == tagClean)
            .Select(IdentificationProjection)
            .FirstOrDefaultAsync(ct);

        return item is null
            ? NotFound(new { error = "No inventory item matches both identifiers." })
            : Ok(item);
    }

    // GET: /api/inventory/search?q={query}
    [HttpGet("search")]
    public async Task<ActionResult<PagedResponse<InventoryItemResponse>>> Search(
        CancellationToken ct,
        [FromQuery] string? q = null,
        [FromQuery] string? locationId = null)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return await GetInventory(ct, locationId);
        }

        var results = await SearchQuery(db, InventoryRules.ContainsPattern(q), locationId).ToListAsync(ct);

        return Ok(new PagedResponse<InventoryItemResponse>(results, false));
    }

    // POST: /api/inventory
    [HttpPost]
    public async Task<ActionResult<InventoryItemResponse>> RegisterItem(
        [FromBody] RegisterInventoryRequest request,
        CancellationToken ct)
    {
        var serialClean = request.SerialNumber.Trim();
        var tagClean = request.TagNumber.Trim();

        // A replayed key must return the original row, so this runs before the duplicate check:
        // the row the first attempt created would otherwise look like a conflict.
        var idempotencyKey = ReadIdempotencyKey();
        if (idempotencyKey is not null)
        {
            var replay = await FindByIdempotencyKeyAsync(idempotencyKey, ct);
            if (replay is not null) return Ok(replay);
        }

        var duplicate = await FindDuplicateAsync(serialClean, tagClean, ct);
        if (duplicate is not null) return Conflict(duplicate);

        var location = await db.StoreLocations.AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == request.StoreLocationId, ct);
        if (location is null)
        {
            return BadRequest(new { error = $"Store location ID '{request.StoreLocationId}' does not exist." });
        }
        if (!location.IsActive)
        {
            return BadRequest(new { error = $"Store location '{location.LocationName}' ({location.LocationCode}) is inactive." });
        }

        var personnel = await db.Personnel.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.ScanByPersonnelId, ct);
        if (personnel is null)
        {
            return BadRequest(new { error = $"Personnel ID '{request.ScanByPersonnelId}' does not exist." });
        }
        if (!personnel.IsActive)
        {
            return BadRequest(new { error = $"Personnel '{personnel.Name}' is inactive." });
        }

        if (idempotencyKey is not null && !idempotency.TryBegin(idempotencyKey))
        {
            return Conflict(new { error = "That request is already being processed. Wait a moment and check the inventory list." });
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var newItem = new InventoryItem
        {
            Id = Guid.NewGuid().ToString(),
            SerialNumber = serialClean,
            TagNumber = tagClean,
            LocationId = location.Id,
            RegisteredByPersonnelId = personnel.Id,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        try
        {
            var strategy = db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(ct);

                db.InventoryItems.Add(newItem);
                db.InventoryMovements.Add(new InventoryMovement
                {
                    Id = Guid.NewGuid().ToString(),
                    InventoryItemId = newItem.Id,
                    FromLocationId = null,
                    ToLocationId = location.Id,
                    PerformedByPersonnelId = personnel.Id,
                    EventType = MovementEventType.InitialRegistration,
                    MovedAtUtc = now,
                    IdempotencyKey = idempotencyKey
                });

                audit.Add(AuditAction.Create, AuditEntity.InventoryItem, newItem.Id,
                    newValue: new { newItem.SerialNumber, newItem.TagNumber, newItem.LocationId, newItem.RegisteredByPersonnelId },
                    referenceNo: newItem.SerialNumber);

                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            });
        }
        catch (DbUpdateException ex)
        {
            if (idempotencyKey is not null) idempotency.Release(idempotencyKey);
            logger.LogError(ex, "Constraint violation registering item {Serial}.", serialClean);

            // A racing request may have taken the serial or tag between the check and the insert.
            var raced = await FindDuplicateAsync(serialClean, tagClean, ct);
            return raced is not null ? Conflict(raced) : Conflict(new { error = ConcurrentWriteMessage });
        }
        catch
        {
            if (idempotencyKey is not null) idempotency.Release(idempotencyKey);
            throw;
        }

        logger.LogInformation("Item registered: Serial={Serial}, Tag={Tag}, Loc={Loc}, By={By}",
            newItem.SerialNumber, newItem.TagNumber, location.LocationCode, personnel.PersonnelCode);

        var created = new InventoryItemResponse
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
            IsIssued = false,
            CreatedAt = newItem.CreatedAtUtc,
            UpdatedAt = newItem.UpdatedAtUtc
        };

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    // POST: /api/inventory/issue
    [HttpPost("issue")]
    public async Task<ActionResult<IssueInventoryResponse>> IssueForPacking(
        [FromBody] IssueInventoryRequest request,
        CancellationToken ct)
    {
        var serialClean = request.SerialNumber.Trim();
        var tagClean = request.TagNumber.Trim();
        var personnelId = request.PerformedByPersonnelId.Trim();

        if (string.IsNullOrWhiteSpace(serialClean) ||
            string.IsNullOrWhiteSpace(tagClean) ||
            string.IsNullOrWhiteSpace(personnelId))
        {
            return BadRequest(new { error = "Serial number, tag number, and responsible personnel are required." });
        }

        var idempotencyKey = ReadIdempotencyKey();
        if (idempotencyKey is not null)
        {
            // Replayed key: hand back the movement the first attempt recorded.
            var replay = await ReadIssueReplayAsync(idempotencyKey, ct);
            if (replay is not null) return Ok(replay);

            if (!idempotency.TryBegin(idempotencyKey))
            {
                return Conflict(new { error = "That request is already being processed. Wait a moment and check the item history." });
            }
        }

        try
        {
            var item = await db.InventoryItems
                .AsNoTracking()
                .Include(i => i.Location)
                .FirstOrDefaultAsync(i => i.SerialNumber == serialClean && i.TagNumber == tagClean, ct);

            if (item is null)
            {
                return NotFound(new { error = "No inventory item matches both identifiers." });
            }

            var personnel = await db.Personnel.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == personnelId, ct);
            if (personnel is null || !personnel.IsActive)
            {
                return BadRequest(new { error = "Responsible personnel does not exist or is inactive." });
            }

            if (item.IsIssued)
            {
                return Conflict(new { error = "This inventory item has already been issued." });
            }

            var issuedAt = timeProvider.GetUtcNow().UtcDateTime;
            var movement = new InventoryMovement
            {
                Id = Guid.NewGuid().ToString(),
                InventoryItemId = item.Id,
                FromLocationId = item.LocationId,
                ToLocationId = null,
                PerformedByPersonnelId = personnel.Id,
                EventType = MovementEventType.IssueForPacking,
                MovedAtUtc = issuedAt,
                IdempotencyKey = idempotencyKey
            };

            var issued = false;
            var strategy = db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(ct);

                // The IsIssued guard inside the update is what makes a double-issue impossible.
                var updated = await db.InventoryItems
                    .Where(i => i.Id == item.Id && !i.IsIssued)
                    .ExecuteUpdateAsync(set => set
                        .SetProperty(i => i.IsIssued, true)
                        .SetProperty(i => i.UpdatedAtUtc, issuedAt), ct);

                if (updated != 1)
                {
                    await tx.RollbackAsync(ct);
                    return;
                }

                db.InventoryMovements.Add(movement);
                audit.Add(AuditAction.Post, AuditEntity.InventoryItem, item.Id,
                    previousValue: new { IsIssued = false },
                    newValue: new { IsIssued = true },
                    referenceNo: item.SerialNumber,
                    remarks: $"Issued for packing by {personnel.Name}.");

                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                issued = true;
            });

            if (!issued)
            {
                return Conflict(new { error = "This inventory item has already been issued." });
            }

            item.IsIssued = true;
            item.UpdatedAtUtc = issuedAt;

            var response = new IssueInventoryResponse
            {
                Item = new InventoryItemIdentificationResponse
                {
                    Id = item.Id,
                    SerialNumber = item.SerialNumber,
                    TagNumber = item.TagNumber,
                    LocationId = item.LocationId,
                    LocationCode = item.Location.LocationCode,
                    LocationName = item.Location.LocationName,
                    IsIssued = true
                },
                Movement = new InventoryMovementResponse
                {
                    Id = movement.Id,
                    InventoryItemId = item.Id,
                    FromLocationId = item.LocationId,
                    FromLocationName = item.Location.LocationName,
                    ToLocationId = null,
                    ToLocationName = null,
                    PerformedByPersonnelId = personnel.Id,
                    PerformedByPersonnelName = personnel.Name,
                    EventType = MovementEventType.IssueForPacking,
                    MovedAt = issuedAt
                }
            };

            return CreatedAtAction(nameof(GetItemHistory), new { id = item.Id }, response);
        }
        catch (DbUpdateException ex)
        {
            if (idempotencyKey is not null) idempotency.Release(idempotencyKey);
            logger.LogError(ex, "Constraint violation issuing item {Serial}.", serialClean);
            return Conflict(new { error = ConcurrentWriteMessage });
        }
        catch
        {
            if (idempotencyKey is not null) idempotency.Release(idempotencyKey);
            throw;
        }
    }

    // GET: /api/inventory/{id}/history
    [HttpGet("{id}/history")]
    public async Task<ActionResult<IEnumerable<InventoryMovementResponse>>> GetItemHistory(
        string id,
        CancellationToken ct)
    {
        var itemExists = await db.InventoryItems.AnyAsync(i => i.Id == id, ct);
        if (!itemExists)
        {
            return NotFound(new { error = $"Inventory item '{id}' does not exist." });
        }

        var history = await HistoryQuery(db, id).ToListAsync(ct);

        return Ok(history);
    }

    internal static IQueryable<InventoryItemResponse> PageQuery(
        InventoryDbContext db, string? locationId, int page, int pageSize)
    {
        var query = db.InventoryItems.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(locationId)) query = query.Where(i => i.LocationId == locationId);

        // One extra row tells the client whether another page exists.
        return query
            .OrderByDescending(i => i.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize + 1)
            .Select(ItemProjection);
    }

    internal static IQueryable<InventoryItemResponse> SearchQuery(
        InventoryDbContext db, string containsPattern, string? locationId)
    {
        var query = db.InventoryItems
            .AsNoTracking()
            .Where(i => EF.Functions.Like(i.SerialNumber, containsPattern, "\\")
                        || EF.Functions.Like(i.TagNumber, containsPattern, "\\"));
        if (!string.IsNullOrWhiteSpace(locationId)) query = query.Where(i => i.LocationId == locationId);

        return query
            .OrderByDescending(i => i.CreatedAtUtc)
            .Take(SearchResultLimit)
            .Select(ItemProjection);
    }

    internal static IQueryable<InventoryMovementResponse> HistoryQuery(InventoryDbContext db, string itemId) =>
        db.InventoryMovements
            .AsNoTracking()
            .Where(m => m.InventoryItemId == itemId)
            .OrderByDescending(m => m.MovedAtUtc)
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
                MovedAt = m.MovedAtUtc
            });

    private string? ReadIdempotencyKey() =>
        InventoryRules.NormalizeIdempotencyKey(Request.Headers[IdempotencyHeader].ToString());

    private async Task<IssueInventoryResponse?> ReadIssueReplayAsync(string key, CancellationToken ct)
    {
        var movement = await db.InventoryMovements
            .AsNoTracking()
            .Where(m => m.IdempotencyKey == key && m.EventType == MovementEventType.IssueForPacking)
            .Select(m => new
            {
                Movement = new InventoryMovementResponse
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
                    MovedAt = m.MovedAtUtc
                },
                Item = new InventoryItemIdentificationResponse
                {
                    Id = m.InventoryItem.Id,
                    SerialNumber = m.InventoryItem.SerialNumber,
                    TagNumber = m.InventoryItem.TagNumber,
                    LocationId = m.InventoryItem.LocationId,
                    LocationCode = m.InventoryItem.Location.LocationCode,
                    LocationName = m.InventoryItem.Location.LocationName,
                    IsIssued = m.InventoryItem.IsIssued
                }
            })
            .FirstOrDefaultAsync(ct);

        return movement is null
            ? null
            : new IssueInventoryResponse { Item = movement.Item, Movement = movement.Movement };
    }

    private async Task<InventoryItemResponse?> FindByIdempotencyKeyAsync(string key, CancellationToken ct) =>
        await db.InventoryMovements
            .AsNoTracking()
            .Where(m => m.IdempotencyKey == key)
            .Select(m => m.InventoryItem)
            .Select(ItemProjection)
            .FirstOrDefaultAsync(ct);

    private async Task<DuplicateConflictResponse?> FindDuplicateAsync(
        string serial,
        string tag,
        CancellationToken ct)
    {
        var existing = await db.InventoryItems
            .AsNoTracking()
            .Where(i => i.SerialNumber == serial || i.TagNumber == tag)
            .Select(ItemProjection)
            .FirstOrDefaultAsync(ct);

        return existing is null ? null : InventoryRules.DuplicateConflict(existing, serial, tag);
    }

    internal static readonly System.Linq.Expressions.Expression<Func<InventoryItem, InventoryItemResponse>> ItemProjection =
        i => new InventoryItemResponse
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
            CreatedAt = i.CreatedAtUtc,
            UpdatedAt = i.UpdatedAtUtc
        };

    internal static readonly System.Linq.Expressions.Expression<Func<InventoryItem, InventoryItemIdentificationResponse>> IdentificationProjection =
        i => new InventoryItemIdentificationResponse
        {
            Id = i.Id,
            SerialNumber = i.SerialNumber,
            TagNumber = i.TagNumber,
            LocationId = i.LocationId,
            LocationCode = i.Location.LocationCode,
            LocationName = i.Location.LocationName,
            IsIssued = i.IsIssued
        };
}
