using IndustrialInventory.Api.Data;
using IndustrialInventory.Api.DTOs;
using IndustrialInventory.Api.Models;
using IndustrialInventory.Api.Models.Audit;
using IndustrialInventory.Api.Services.Auth;
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

        var enriched = await WithCurrentAgingAsync(db, rows, timeProvider.GetUtcNow().UtcDateTime, ct);
        return Ok(new PagedResponse<InventoryItemResponse>(enriched, hasMore));
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

        if (item is null) return NotFound(new { error = $"Inventory item with ID '{id}' was not found." });

        var enriched = await WithCurrentAgingAsync(db, [item], timeProvider.GetUtcNow().UtcDateTime, ct);
        return Ok(enriched[0]);
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
            .Select(ItemProjection)
            .FirstOrDefaultAsync(ct);

        if (item is null) return NotFound(new { error = "No inventory item matches both identifiers." });

        var current = (await WithCurrentAgingAsync(
            db, [item], timeProvider.GetUtcNow().UtcDateTime, ct))[0];
        return Ok(new InventoryItemIdentificationResponse
        {
            Id = current.Id,
            SerialNumber = current.SerialNumber,
            TagNumber = current.TagNumber,
            LocationId = current.LocationId,
            LocationCode = current.LocationCode,
            LocationName = current.LocationName,
            IsIssued = current.IsIssued,
            CurrentStorageCycleId = current.CurrentStorageCycleId,
            CurrentAgingDays = current.CurrentAgingDays
        });
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
        var enriched = await WithCurrentAgingAsync(db, results, timeProvider.GetUtcNow().UtcDateTime, ct);

        return Ok(new PagedResponse<InventoryItemResponse>(enriched, false));
    }

    // POST: /api/inventory
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.InventoryLifecycle)]
    public async Task<ActionResult<InventoryItemResponse>> RegisterItem(
        [FromBody] RegisterInventoryRequest request,
        CancellationToken ct)
    {
        var serialClean = request.SerialNumber.Trim();
        var tagClean = request.TagNumber.Trim();
        var locationId = request.StoreLocationId.Trim();
        var personnelId = request.ScanByPersonnelId.Trim();

        // A replayed key must return the original row, so this runs before the duplicate check:
        // the row the first attempt created would otherwise look like a conflict.
        if (!TryReadIdempotencyKey(out var idempotencyKey))
        {
            return BadRequest(new { error = "Idempotency-Key must not exceed 64 characters." });
        }
        if (idempotencyKey is not null)
        {
            var replay = await ReadRegistrationReplayAsync(
                idempotencyKey, serialClean, tagClean, locationId, personnelId, ct);
            if (replay.Found)
            {
                return replay.Matches
                    ? Ok(replay.Response)
                    : Conflict(new { error = "That idempotency key was already used for a different operation." });
            }
        }

        var duplicate = await FindDuplicateAsync(serialClean, tagClean, ct);
        if (duplicate is not null) return Conflict(duplicate);

        var location = await db.StoreLocations.AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == locationId, ct);
        if (location is null)
        {
            return BadRequest(new { error = $"Store location ID '{locationId}' does not exist." });
        }
        if (!location.IsActive)
        {
            return BadRequest(new { error = $"Store location '{location.LocationName}' ({location.LocationCode}) is inactive." });
        }

        var personnel = await db.Personnel.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == personnelId, ct);
        if (personnel is null)
        {
            return BadRequest(new { error = $"Personnel ID '{personnelId}' does not exist." });
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
        var cycle = NewStorageCycle(newItem.Id, location, personnel, now);
        newItem.StorageCycles.Add(cycle);
        var movement = new InventoryMovement
        {
            Id = Guid.NewGuid().ToString(),
            InventoryItemId = newItem.Id,
            StorageCycleId = cycle.Id,
            StorageCycle = cycle,
            FromLocationId = null,
            ToLocationId = location.Id,
            PerformedByPersonnelId = personnel.Id,
            EventType = MovementEventType.InitialRegistration,
            MovedAtUtc = now,
            IdempotencyKey = idempotencyKey
        };

        try
        {
            var strategy = db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(ct);

                db.InventoryItems.Add(newItem);
                db.StorageCycles.Add(cycle);
                db.InventoryMovements.Add(movement);

                audit.Add(AuditAction.Create, AuditEntity.InventoryItem, newItem.Id,
                    newValue: new
                    {
                        newItem.SerialNumber,
                        newItem.TagNumber,
                        newItem.LocationId,
                        newItem.RegisteredByPersonnelId,
                        StorageCycleId = cycle.Id
                    },
                    referenceNo: newItem.SerialNumber,
                    occurredAtUtc: now);

                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            });
        }
        catch (DbUpdateException ex)
        {
            if (idempotencyKey is not null) idempotency.Release(idempotencyKey);
            logger.LogError(ex, "Constraint violation registering item {Serial}.", serialClean);

            db.ChangeTracker.Clear();
            if (idempotencyKey is not null)
            {
                var replay = await ReadRegistrationReplayAsync(
                    idempotencyKey, serialClean, tagClean, locationId, personnelId, ct);
                if (replay.Found)
                {
                    return replay.Matches
                        ? Ok(replay.Response)
                        : Conflict(new { error = "That idempotency key was already used for a different operation." });
                }
            }

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
            CurrentStorageCycleId = cycle.Id,
            CurrentAgingDays = 0,
            CreatedAt = StorageAging.AsUtc(newItem.CreatedAtUtc),
            UpdatedAt = StorageAging.AsUtc(newItem.UpdatedAtUtc)
        };

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    // POST: /api/inventory/issue
    [HttpPost("issue")]
    [Authorize(Policy = AuthorizationPolicies.InventoryLifecycle)]
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

        if (!TryReadIdempotencyKey(out var idempotencyKey))
        {
            return BadRequest(new { error = "Idempotency-Key must not exceed 64 characters." });
        }
        if (idempotencyKey is not null)
        {
            var replay = await ReadIssueReplayAsync(idempotencyKey, serialClean, tagClean, personnelId, ct);
            if (replay.Found)
            {
                return replay.Matches
                    ? Ok(replay.Response)
                    : Conflict(new { error = "That idempotency key was already used for a different operation." });
            }

            if (!idempotency.TryBegin(idempotencyKey))
            {
                return Conflict(new { error = "That request is already being processed. Wait a moment and check the item history." });
            }
        }

        var issueCommitted = false;
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

            var activeCycle = await db.StorageCycles
                .AsNoTracking()
                .Include(c => c.Location)
                .FirstOrDefaultAsync(c => c.InventoryItemId == item.Id && c.IssuedAtUtc == null, ct);
            if (activeCycle is null)
            {
                logger.LogCritical("Available inventory item {ItemId} has no active storage cycle.", item.Id);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { error = "The inventory item's storage history is inconsistent." });
            }
            if (activeCycle.LocationId != item.LocationId)
            {
                logger.LogCritical("Inventory item {ItemId} and its active storage cycle disagree on location.", item.Id);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { error = "The inventory item's storage history is inconsistent." });
            }

            var issuedAt = timeProvider.GetUtcNow().UtcDateTime;
            if (issuedAt < activeCycle.StoredAtUtc)
            {
                logger.LogCritical("Storage cycle {CycleId} starts after the backend clock.", activeCycle.Id);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { error = "The inventory item's storage history is inconsistent." });
            }

            var movement = new InventoryMovement
            {
                Id = Guid.NewGuid().ToString(),
                InventoryItemId = item.Id,
                StorageCycleId = activeCycle.Id,
                FromLocationId = activeCycle.LocationId,
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

                var closed = await db.StorageCycles
                    .Where(c => c.Id == activeCycle.Id && c.InventoryItemId == item.Id && c.IssuedAtUtc == null)
                    .ExecuteUpdateAsync(set => set
                        .SetProperty(c => c.IssuedAtUtc, issuedAt)
                        .SetProperty(c => c.IssuedByPersonnelId, personnel.Id)
                        .SetProperty(c => c.IssuedByPersonnelCodeSnapshot, personnel.PersonnelCode)
                        .SetProperty(c => c.IssuedByPersonnelNameSnapshot, personnel.Name), ct);

                if (closed != 1)
                {
                    await tx.RollbackAsync(ct);
                    return;
                }

                db.InventoryMovements.Add(movement);
                audit.Add(AuditAction.Post, AuditEntity.InventoryItem, item.Id,
                    previousValue: new { IsIssued = false, StorageCycleId = activeCycle.Id },
                    newValue: new { IsIssued = true, StorageCycleId = activeCycle.Id, ResponsiblePersonnelId = personnel.Id },
                    referenceNo: item.SerialNumber,
                    remarks: $"Issued for packing by {personnel.Name}.",
                    occurredAtUtc: issuedAt);

                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                issued = true;
            });
            issueCommitted = issued;

            if (!issued)
            {
                if (idempotencyKey is not null)
                {
                    var replay = await ReadIssueReplayAsync(idempotencyKey, serialClean, tagClean, personnelId, ct);
                    if (replay.Found)
                    {
                        return replay.Matches
                            ? Ok(replay.Response)
                            : Conflict(new { error = "That idempotency key was already used for a different operation." });
                    }
                }

                return Conflict(new { error = "This inventory item has already been issued." });
            }

            item.IsIssued = true;
            item.UpdatedAtUtc = issuedAt;
            activeCycle.IssuedAtUtc = issuedAt;
            activeCycle.IssuedByPersonnelId = personnel.Id;
            activeCycle.IssuedByPersonnelCodeSnapshot = personnel.PersonnelCode;
            activeCycle.IssuedByPersonnelNameSnapshot = personnel.Name;

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
                    IsIssued = true,
                    CurrentStorageCycleId = null,
                    CurrentAgingDays = null
                },
                StorageCycle = ToStorageCycleResponse(activeCycle, issuedAt),
                Movement = new InventoryMovementResponse
                {
                    Id = movement.Id,
                    InventoryItemId = item.Id,
                    StorageCycleId = activeCycle.Id,
                    FromLocationId = activeCycle.LocationId,
                    FromLocationName = activeCycle.Location.LocationName,
                    ToLocationId = null,
                    ToLocationName = null,
                    PerformedByPersonnelId = personnel.Id,
                    PerformedByPersonnelName = personnel.Name,
                    EventType = MovementEventType.IssueForPacking,
                    MovedAt = StorageAging.AsUtc(issuedAt)
                }
            };

            return CreatedAtAction(nameof(GetItemHistory), new { id = item.Id }, response);
        }
        catch (DbUpdateException ex)
        {
            if (idempotencyKey is not null) idempotency.Release(idempotencyKey);
            logger.LogError(ex, "Constraint violation issuing item {Serial}.", serialClean);
            db.ChangeTracker.Clear();
            if (idempotencyKey is not null)
            {
                var replay = await ReadIssueReplayAsync(idempotencyKey, serialClean, tagClean, personnelId, ct);
                if (replay.Found)
                {
                    return replay.Matches
                        ? Ok(replay.Response)
                        : Conflict(new { error = "That idempotency key was already used for a different operation." });
                }
            }
            return Conflict(new { error = ConcurrentWriteMessage });
        }
        catch
        {
            if (idempotencyKey is not null) idempotency.Release(idempotencyKey);
            throw;
        }
        finally
        {
            if (idempotencyKey is not null && !issueCommitted) idempotency.Release(idempotencyKey);
        }
    }

    // POST: /api/inventory/return-to-storage
    [HttpPost("return-to-storage")]
    [Authorize(Policy = AuthorizationPolicies.InventoryLifecycle)]
    public async Task<ActionResult<ReturnToStorageResponse>> ReturnToStorage(
        [FromBody] ReturnToStorageRequest request,
        CancellationToken ct)
    {
        var serialClean = request.SerialNumber.Trim();
        var tagClean = request.TagNumber.Trim();
        var locationId = request.StoreLocationId.Trim();
        var personnelId = request.PerformedByPersonnelId.Trim();
        if (string.IsNullOrWhiteSpace(serialClean) ||
            string.IsNullOrWhiteSpace(tagClean) ||
            string.IsNullOrWhiteSpace(locationId) ||
            string.IsNullOrWhiteSpace(personnelId))
        {
            return BadRequest(new { error = "Serial number, tag number, store location, and responsible personnel are required." });
        }

        if (!TryReadIdempotencyKey(out var idempotencyKey) || idempotencyKey is null)
        {
            return BadRequest(new { error = "A valid Idempotency-Key of at most 64 characters is required." });
        }

        var replay = await ReadReturnReplayAsync(
            idempotencyKey, serialClean, tagClean, locationId, personnelId, ct);
        if (replay.Found)
        {
            return replay.Matches
                ? Ok(replay.Response)
                : Conflict(new { error = "That idempotency key was already used for a different operation." });
        }

        if (!idempotency.TryBegin(idempotencyKey))
        {
            return Conflict(new { error = "That request is already being processed. Wait a moment and retry the same key." });
        }

        var committed = false;
        try
        {
            var item = await db.InventoryItems
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.SerialNumber == serialClean && i.TagNumber == tagClean, ct);
            if (item is null)
            {
                return NotFound(new { error = "No inventory item matches both identifiers." });
            }
            if (!item.IsIssued)
            {
                return Conflict(new { error = "This inventory item is already in storage." });
            }

            var existingCycles = await db.StorageCycles
                .AsNoTracking()
                .Where(cycle => cycle.InventoryItemId == item.Id)
                .Select(cycle => new { cycle.Id, cycle.IssuedAtUtc })
                .ToListAsync(ct);
            if (existingCycles.Count == 0 || existingCycles.Any(cycle => cycle.IssuedAtUtc is null))
            {
                logger.LogCritical("Issued inventory item {ItemId} has inconsistent storage-cycle history.", item.Id);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { error = "The inventory item's storage history is inconsistent." });
            }

            var location = await db.StoreLocations.AsNoTracking()
                .FirstOrDefaultAsync(candidate => candidate.Id == locationId, ct);
            if (location is null || !location.IsActive)
            {
                return BadRequest(new { error = "Store location does not exist or is inactive." });
            }

            var personnel = await db.Personnel.AsNoTracking()
                .FirstOrDefaultAsync(candidate => candidate.Id == personnelId, ct);
            if (personnel is null || !personnel.IsActive)
            {
                return BadRequest(new { error = "Responsible personnel does not exist or is inactive." });
            }

            var returnedAt = timeProvider.GetUtcNow().UtcDateTime;
            var cycle = NewStorageCycle(item.Id, location, personnel, returnedAt);
            var movement = new InventoryMovement
            {
                Id = Guid.NewGuid().ToString(),
                InventoryItemId = item.Id,
                StorageCycleId = cycle.Id,
                StorageCycle = cycle,
                FromLocationId = null,
                ToLocationId = location.Id,
                PerformedByPersonnelId = personnel.Id,
                EventType = MovementEventType.ReturnedToStorage,
                MovedAtUtc = returnedAt,
                IdempotencyKey = idempotencyKey
            };

            var returned = false;
            var strategy = db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                var updated = await db.InventoryItems
                    .Where(candidate => candidate.Id == item.Id && candidate.IsIssued)
                    .ExecuteUpdateAsync(set => set
                        .SetProperty(candidate => candidate.IsIssued, false)
                        .SetProperty(candidate => candidate.LocationId, location.Id)
                        .SetProperty(candidate => candidate.UpdatedAtUtc, returnedAt), ct);

                if (updated != 1)
                {
                    await tx.RollbackAsync(ct);
                    return;
                }

                db.StorageCycles.Add(cycle);
                db.InventoryMovements.Add(movement);
                audit.Add(AuditAction.Post, AuditEntity.InventoryItem, item.Id,
                    previousValue: new { IsIssued = true, LocationId = item.LocationId },
                    newValue: new
                    {
                        IsIssued = false,
                        LocationId = location.Id,
                        StorageCycleId = cycle.Id,
                        ResponsiblePersonnelId = personnel.Id
                    },
                    referenceNo: item.SerialNumber,
                    remarks: $"Returned to storage by {personnel.Name}.",
                    occurredAtUtc: returnedAt);

                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                returned = true;
            });

            if (!returned)
            {
                var racedReplay = await ReadReturnReplayAsync(
                    idempotencyKey, serialClean, tagClean, locationId, personnelId, ct);
                if (racedReplay.Found)
                {
                    return racedReplay.Matches
                        ? Ok(racedReplay.Response)
                        : Conflict(new { error = "That idempotency key was already used for a different operation." });
                }

                return Conflict(new { error = "This inventory item is no longer issued." });
            }

            committed = true;
            var response = new ReturnToStorageResponse
            {
                Item = new InventoryItemIdentificationResponse
                {
                    Id = item.Id,
                    SerialNumber = item.SerialNumber,
                    TagNumber = item.TagNumber,
                    LocationId = location.Id,
                    LocationCode = location.LocationCode,
                    LocationName = location.LocationName,
                    IsIssued = false,
                    CurrentStorageCycleId = cycle.Id,
                    CurrentAgingDays = 0
                },
                StorageCycle = ToStorageCycleResponse(cycle, returnedAt),
                Movement = new InventoryMovementResponse
                {
                    Id = movement.Id,
                    InventoryItemId = item.Id,
                    StorageCycleId = cycle.Id,
                    FromLocationId = null,
                    FromLocationName = null,
                    ToLocationId = location.Id,
                    ToLocationName = location.LocationName,
                    PerformedByPersonnelId = personnel.Id,
                    PerformedByPersonnelName = personnel.Name,
                    EventType = MovementEventType.ReturnedToStorage,
                    MovedAt = StorageAging.AsUtc(returnedAt)
                }
            };

            return CreatedAtAction(nameof(GetItemHistory), new { id = item.Id }, response);
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Constraint violation returning item {Serial} to storage.", serialClean);
            db.ChangeTracker.Clear();
            var racedReplay = await ReadReturnReplayAsync(
                idempotencyKey, serialClean, tagClean, locationId, personnelId, ct);
            if (racedReplay.Found)
            {
                return racedReplay.Matches
                    ? Ok(racedReplay.Response)
                    : Conflict(new { error = "That idempotency key was already used for a different operation." });
            }
            return Conflict(new { error = ConcurrentWriteMessage });
        }
        finally
        {
            if (!committed) idempotency.Release(idempotencyKey);
        }
    }

    // GET: /api/inventory/{id}/history
    [HttpGet("{id}/history")]
    public async Task<ActionResult<InventoryHistoryResponse>> GetItemHistory(
        string id,
        CancellationToken ct)
    {
        var item = await db.InventoryItems
            .AsNoTracking()
            .Where(i => i.Id == id)
            .Select(ItemProjection)
            .FirstOrDefaultAsync(ct);
        if (item is null) return NotFound(new { error = $"Inventory item '{id}' does not exist." });

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var cycles = await db.StorageCycles
            .AsNoTracking()
            .Where(c => c.InventoryItemId == id)
            .OrderBy(c => c.StoredAtUtc)
            .ThenBy(c => c.Id)
            .ToListAsync(ct);
        var current = WithAging([item], cycles, nowUtc)[0];
        var cycleResponses = cycles.Select(c => ToStorageCycleResponse(c, nowUtc)).ToList();
        var movements = await HistoryQuery(db, id).ToListAsync(ct);

        return Ok(new InventoryHistoryResponse
        {
            Item = new InventoryHistoryItemResponse
            {
                Id = current.Id,
                SerialNumber = current.SerialNumber,
                TagNumber = current.TagNumber,
                IsIssued = current.IsIssued,
                LocationId = current.LocationId,
                LocationCode = current.LocationCode,
                LocationName = current.LocationName,
                CurrentStorageCycleId = current.CurrentStorageCycleId,
                CurrentAgingDays = current.CurrentAgingDays
            },
            StorageCycles = cycleResponses,
            Movements = movements,
            TotalStorageDays = current.TotalStorageDays
        });
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
            .OrderBy(m => m.MovedAtUtc)
            .ThenBy(m => m.Id)
            .Select(m => new InventoryMovementResponse
            {
                Id = m.Id,
                InventoryItemId = m.InventoryItemId,
                StorageCycleId = m.StorageCycleId,
                FromLocationId = m.FromLocationId,
                FromLocationName = m.FromLocation != null ? m.FromLocation.LocationName : null,
                ToLocationId = m.ToLocationId,
                ToLocationName = m.ToLocation != null ? m.ToLocation.LocationName : null,
                PerformedByPersonnelId = m.PerformedByPersonnelId,
                PerformedByPersonnelName = m.PerformedByPersonnel.Name,
                EventType = m.EventType,
                MovedAt = StorageAging.AsUtc(m.MovedAtUtc)
            });

    private bool TryReadIdempotencyKey(out string? idempotencyKey)
    {
        var values = Request.Headers[IdempotencyHeader];
        if (values.Count > 1)
        {
            idempotencyKey = null;
            return false;
        }

        return InventoryRules.TryNormalizeIdempotencyKey(values.ToString(), out idempotencyKey);
    }

    private async Task<(bool Found, bool Matches, InventoryItemResponse? Response)> ReadRegistrationReplayAsync(
        string key,
        string serial,
        string tag,
        string locationId,
        string personnelId,
        CancellationToken ct)
    {
        var movement = await db.InventoryMovements
            .AsNoTracking()
            .Where(candidate => candidate.IdempotencyKey == key)
            .Select(candidate => new
            {
                candidate.EventType,
                candidate.ToLocationId,
                candidate.PerformedByPersonnelId,
                Item = candidate.InventoryItem,
                Cycle = candidate.StorageCycle
            })
            .FirstOrDefaultAsync(ct);
        if (movement is null) return (false, false, null);
        if (movement.Cycle is null) throw new InvalidOperationException("An idempotent lifecycle movement has no storage cycle.");

        var cycle = movement.Cycle;
        var response = new InventoryItemResponse
        {
            Id = movement.Item.Id,
            SerialNumber = movement.Item.SerialNumber,
            TagNumber = movement.Item.TagNumber,
            LocationId = cycle.LocationId,
            LocationCode = cycle.LocationCodeSnapshot,
            LocationName = cycle.LocationNameSnapshot,
            RegisteredByPersonnelId = cycle.StoredByPersonnelId,
            RegisteredByPersonnelCode = cycle.StoredByPersonnelCodeSnapshot,
            RegisteredByPersonnelName = cycle.StoredByPersonnelNameSnapshot,
            IsIssued = false,
            CurrentStorageCycleId = cycle.Id,
            CurrentAgingDays = 0,
            CreatedAt = StorageAging.AsUtc(cycle.StoredAtUtc),
            UpdatedAt = StorageAging.AsUtc(cycle.StoredAtUtc)
        };
        var matches = movement.EventType == MovementEventType.InitialRegistration &&
            movement.Item.SerialNumber == serial &&
            movement.Item.TagNumber == tag &&
            movement.ToLocationId == locationId &&
            cycle.LocationId == locationId &&
            movement.PerformedByPersonnelId == personnelId &&
            cycle.StoredByPersonnelId == personnelId;

        return (true, matches, response);
    }

    private async Task<(bool Found, bool Matches, IssueInventoryResponse? Response)> ReadIssueReplayAsync(
        string key,
        string serial,
        string tag,
        string personnelId,
        CancellationToken ct)
    {
        var movement = await db.InventoryMovements
            .AsNoTracking()
            .Where(candidate => candidate.IdempotencyKey == key)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.EventType,
                candidate.InventoryItemId,
                candidate.StorageCycleId,
                candidate.FromLocationId,
                candidate.ToLocationId,
                candidate.PerformedByPersonnelId,
                candidate.MovedAtUtc,
                FromLocationName = candidate.FromLocation == null ? null : candidate.FromLocation.LocationName,
                PerformedByPersonnelName = candidate.PerformedByPersonnel.Name,
                Item = candidate.InventoryItem,
                Cycle = candidate.StorageCycle
            })
            .FirstOrDefaultAsync(ct);
        if (movement is null) return (false, false, null);
        if (movement.Cycle is null) throw new InvalidOperationException("An idempotent lifecycle movement has no storage cycle.");

        var cycle = movement.Cycle;
        var response = new IssueInventoryResponse
        {
            Item = new InventoryItemIdentificationResponse
            {
                Id = movement.Item.Id,
                SerialNumber = movement.Item.SerialNumber,
                TagNumber = movement.Item.TagNumber,
                LocationId = cycle.LocationId,
                LocationCode = cycle.LocationCodeSnapshot,
                LocationName = cycle.LocationNameSnapshot,
                IsIssued = true,
                CurrentStorageCycleId = null,
                CurrentAgingDays = null
            },
            StorageCycle = ToStorageCycleResponse(cycle, cycle.IssuedAtUtc ?? movement.MovedAtUtc),
            Movement = new InventoryMovementResponse
            {
                Id = movement.Id,
                InventoryItemId = movement.InventoryItemId,
                StorageCycleId = movement.StorageCycleId,
                FromLocationId = movement.FromLocationId,
                FromLocationName = movement.FromLocationName,
                ToLocationId = movement.ToLocationId,
                ToLocationName = null,
                PerformedByPersonnelId = movement.PerformedByPersonnelId,
                PerformedByPersonnelName = movement.PerformedByPersonnelName,
                EventType = movement.EventType,
                MovedAt = StorageAging.AsUtc(movement.MovedAtUtc)
            }
        };
        var matches = movement.EventType == MovementEventType.IssueForPacking &&
            movement.Item.SerialNumber == serial &&
            movement.Item.TagNumber == tag &&
            movement.PerformedByPersonnelId == personnelId;

        return (true, matches, response);
    }

    private async Task<(bool Found, bool Matches, ReturnToStorageResponse? Response)> ReadReturnReplayAsync(
        string key,
        string serial,
        string tag,
        string locationId,
        string personnelId,
        CancellationToken ct)
    {
        var movement = await db.InventoryMovements
            .AsNoTracking()
            .Where(candidate => candidate.IdempotencyKey == key)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.EventType,
                candidate.InventoryItemId,
                candidate.StorageCycleId,
                candidate.ToLocationId,
                candidate.PerformedByPersonnelId,
                candidate.MovedAtUtc,
                Item = candidate.InventoryItem,
                Cycle = candidate.StorageCycle
            })
            .FirstOrDefaultAsync(ct);
        if (movement is null) return (false, false, null);
        if (movement.Cycle is null) throw new InvalidOperationException("An idempotent lifecycle movement has no storage cycle.");

        var persistedCycle = movement.Cycle;
        var originalActiveCycle = new StorageCycle
        {
            Id = persistedCycle.Id,
            InventoryItemId = persistedCycle.InventoryItemId,
            LocationId = persistedCycle.LocationId,
            StoredAtUtc = persistedCycle.StoredAtUtc,
            StoredByPersonnelId = persistedCycle.StoredByPersonnelId,
            LocationCodeSnapshot = persistedCycle.LocationCodeSnapshot,
            LocationNameSnapshot = persistedCycle.LocationNameSnapshot,
            StoredByPersonnelCodeSnapshot = persistedCycle.StoredByPersonnelCodeSnapshot,
            StoredByPersonnelNameSnapshot = persistedCycle.StoredByPersonnelNameSnapshot
        };
        var response = new ReturnToStorageResponse
        {
            Item = new InventoryItemIdentificationResponse
            {
                Id = movement.Item.Id,
                SerialNumber = movement.Item.SerialNumber,
                TagNumber = movement.Item.TagNumber,
                LocationId = persistedCycle.LocationId,
                LocationCode = persistedCycle.LocationCodeSnapshot,
                LocationName = persistedCycle.LocationNameSnapshot,
                IsIssued = false,
                CurrentStorageCycleId = persistedCycle.Id,
                CurrentAgingDays = 0
            },
            StorageCycle = ToStorageCycleResponse(originalActiveCycle, movement.MovedAtUtc),
            Movement = new InventoryMovementResponse
            {
                Id = movement.Id,
                InventoryItemId = movement.InventoryItemId,
                StorageCycleId = movement.StorageCycleId,
                FromLocationId = null,
                FromLocationName = null,
                ToLocationId = movement.ToLocationId,
                ToLocationName = persistedCycle.LocationNameSnapshot,
                PerformedByPersonnelId = movement.PerformedByPersonnelId,
                PerformedByPersonnelName = persistedCycle.StoredByPersonnelNameSnapshot,
                EventType = movement.EventType,
                MovedAt = StorageAging.AsUtc(movement.MovedAtUtc)
            }
        };
        var matches = movement.EventType == MovementEventType.ReturnedToStorage &&
            movement.Item.SerialNumber == serial &&
            movement.Item.TagNumber == tag &&
            movement.ToLocationId == locationId &&
            persistedCycle.LocationId == locationId &&
            movement.PerformedByPersonnelId == personnelId &&
            persistedCycle.StoredByPersonnelId == personnelId;

        return (true, matches, response);
    }

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

        if (existing is null) return null;

        var current = await WithCurrentAgingAsync(
            db, [existing], timeProvider.GetUtcNow().UtcDateTime, ct);
        return InventoryRules.DuplicateConflict(current[0], serial, tag);
    }

    internal static async Task<List<InventoryItemResponse>> WithCurrentAgingAsync(
        InventoryDbContext db,
        IReadOnlyList<InventoryItemResponse> items,
        DateTime nowUtc,
        CancellationToken ct)
    {
        if (items.Count == 0) return [];

        var itemIds = items.Select(item => item.Id).ToArray();
        var cycles = await db.StorageCycles
            .AsNoTracking()
            .Where(cycle => itemIds.Contains(cycle.InventoryItemId))
            .ToListAsync(ct);
        return WithAging(items, cycles, nowUtc);
    }

    internal static List<InventoryItemResponse> WithAging(
        IReadOnlyList<InventoryItemResponse> items,
        IReadOnlyCollection<StorageCycle> cycles,
        DateTime nowUtc)
    {
        var cyclesByItem = cycles.ToLookup(cycle => cycle.InventoryItemId, StringComparer.Ordinal);
        var result = new List<InventoryItemResponse>(items.Count);

        foreach (var item in items)
        {
            var summary = StorageAging.Summarize(item.IsIssued, cyclesByItem[item.Id].ToArray(), nowUtc);
            result.Add(item with
            {
                CurrentStorageCycleId = summary.CurrentStorageCycleId,
                CurrentAgingDays = summary.CurrentAgingDays,
                TotalStorageDays = summary.TotalStorageDays,
                CreatedAt = StorageAging.AsUtc(item.CreatedAt),
                UpdatedAt = StorageAging.AsUtc(item.UpdatedAt)
            });
        }

        return result;
    }

    private static StorageCycle NewStorageCycle(
        string itemId,
        StoreLocation location,
        Personnel responsiblePersonnel,
        DateTime storedAtUtc) =>
        new()
        {
            Id = Guid.NewGuid().ToString(),
            InventoryItemId = itemId,
            LocationId = location.Id,
            StoredAtUtc = StorageAging.AsUtc(storedAtUtc),
            StoredByPersonnelId = responsiblePersonnel.Id,
            LocationCodeSnapshot = location.LocationCode,
            LocationNameSnapshot = location.LocationName,
            StoredByPersonnelCodeSnapshot = responsiblePersonnel.PersonnelCode,
            StoredByPersonnelNameSnapshot = responsiblePersonnel.Name
        };

    private static StorageCycleResponse ToStorageCycleResponse(StorageCycle cycle, DateTime nowUtc)
    {
        var endUtc = cycle.IssuedAtUtc ?? nowUtc;
        return new StorageCycleResponse
        {
            Id = cycle.Id,
            InventoryItemId = cycle.InventoryItemId,
            LocationId = cycle.LocationId,
            LocationCodeSnapshot = cycle.LocationCodeSnapshot,
            LocationNameSnapshot = cycle.LocationNameSnapshot,
            StoredAtUtc = StorageAging.AsUtc(cycle.StoredAtUtc),
            IssuedAtUtc = cycle.IssuedAtUtc is DateTime issuedAt
                ? StorageAging.AsUtc(issuedAt)
                : null,
            StoredByPersonnelId = cycle.StoredByPersonnelId,
            StoredByPersonnelCodeSnapshot = cycle.StoredByPersonnelCodeSnapshot,
            StoredByPersonnelNameSnapshot = cycle.StoredByPersonnelNameSnapshot,
            IssuedByPersonnelId = cycle.IssuedByPersonnelId,
            IssuedByPersonnelCodeSnapshot = cycle.IssuedByPersonnelCodeSnapshot,
            IssuedByPersonnelNameSnapshot = cycle.IssuedByPersonnelNameSnapshot,
            DurationDays = StorageAging.WholeDays(cycle.StoredAtUtc, endUtc),
            IsActive = cycle.IssuedAtUtc is null
        };
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
}
