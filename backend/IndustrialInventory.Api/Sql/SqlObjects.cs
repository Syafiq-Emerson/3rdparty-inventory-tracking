using IndustrialInventory.Api.Data;
using IndustrialInventory.Api.Models;

namespace IndustrialInventory.Api.Sql;

/// <summary>
/// CREATE OR ALTER text for database objects that migrations apply. Changing an object means
/// editing the constant here and adding a migration that re-applies it.
/// </summary>
public static class SqlObjects
{
    public const string Schema = InventoryDbContext.Schema;

    /// <summary>
    /// Wraps a batch in EXEC(N'...') with quotes doubled, so the statement survives
    /// "dotnet ef migrations script --idempotent" where CREATE must be first in its batch.
    /// </summary>
    public static string AsBatch(string sql) =>
        $"EXEC(N'{sql.Replace("'", "''")}')";

    public const string CreateInventoryMovementImmutableTrigger = $"""
        CREATE OR ALTER TRIGGER [{Schema}].[TR_InventoryMovement_Immutable]
        ON [{Schema}].[InventoryMovement]
        INSTEAD OF UPDATE, DELETE
        AS
        BEGIN
            SET NOCOUNT ON;
            IF EXISTS (SELECT 1 FROM deleted)
                THROW 51002, N'Movement records cannot be changed or deleted. Post a reversing entry instead.', 1;
        END
        """;

    public const string DropInventoryMovementImmutableTrigger =
        $"DROP TRIGGER IF EXISTS [{Schema}].[TR_InventoryMovement_Immutable]";

    public const string DropInventoryMovementLifecycleTrigger =
        $"DROP TRIGGER IF EXISTS [{Schema}].[TR_InventoryMovement_ValidateLifecycle]";

    public const string DropStorageCycleImmutableTrigger =
        $"DROP TRIGGER IF EXISTS [{Schema}].[TR_StorageCycle_Immutable]";

    public const string CreateAuditEventImmutableTrigger = $"""
        CREATE OR ALTER TRIGGER [{Schema}].[TR_AuditEvent_Immutable]
        ON [{Schema}].[AuditEvent]
        INSTEAD OF UPDATE, DELETE
        AS
        BEGIN
            SET NOCOUNT ON;
            IF EXISTS (SELECT 1 FROM deleted)
                THROW 51000, N'Audit records cannot be changed or deleted.', 1;
        END
        """;

    public const string DropAuditEventImmutableTrigger =
        $"DROP TRIGGER IF EXISTS [{Schema}].[TR_AuditEvent_Immutable]";

    public const string BackfillLegacyStorageCycles = $"""
        DECLARE @MovementCount bigint = (SELECT COUNT_BIG(*) FROM [{Schema}].[InventoryMovement]);
        DECLARE @AuditCount bigint = (SELECT COUNT_BIG(*) FROM [{Schema}].[AuditEvent]);

        IF EXISTS (
            SELECT 1
            FROM [{Schema}].[InventoryMovement]
            WHERE [EventType] NOT IN (N'{MovementEventType.InitialRegistration}', N'{MovementEventType.IssueForPacking}')
        )
            THROW 51005, N'Unknown legacy movement event prevents storage-cycle backfill.', 1;

        IF EXISTS (
            SELECT i.Id
            FROM [{Schema}].[InventoryItem] i
            LEFT JOIN [{Schema}].[InventoryMovement] m
                ON m.InventoryItemId = i.Id
               AND m.EventType = N'{MovementEventType.InitialRegistration}'
            GROUP BY i.Id, i.CreatedAtUtc, i.LocationId, i.RegisteredByPersonnelId
            HAVING COUNT_BIG(m.Id) <> 1
                OR MIN(m.MovedAtUtc) <> i.CreatedAtUtc
                OR MAX(CASE WHEN m.FromLocationId IS NULL THEN 0 ELSE 1 END) <> 0
                OR MAX(CASE WHEN m.ToLocationId = i.LocationId THEN 0 ELSE 1 END) <> 0
                OR MAX(CASE WHEN m.PerformedByPersonnelId = i.RegisteredByPersonnelId THEN 0 ELSE 1 END) <> 0
        )
            THROW 51005, N'Legacy registration history is missing, duplicated, or inconsistent; no timestamps or actors were inferred.', 1;

        IF EXISTS (
            SELECT i.Id
            FROM [{Schema}].[InventoryItem] i
            LEFT JOIN [{Schema}].[InventoryMovement] m
                ON m.InventoryItemId = i.Id
               AND m.EventType = N'{MovementEventType.IssueForPacking}'
            GROUP BY i.Id, i.IsIssued, i.CreatedAtUtc, i.LocationId
            HAVING (i.IsIssued = 1 AND COUNT_BIG(m.Id) <> 1)
                OR (i.IsIssued = 0 AND COUNT_BIG(m.Id) <> 0)
                OR (COUNT_BIG(m.Id) = 1 AND (
                    MIN(m.MovedAtUtc) < i.CreatedAtUtc
                    OR MAX(CASE WHEN m.FromLocationId = i.LocationId THEN 0 ELSE 1 END) <> 0
                    OR MAX(CASE WHEN m.ToLocationId IS NULL THEN 0 ELSE 1 END) <> 0
                ))
        )
            THROW 51005, N'Legacy issue history conflicts with item state or has invalid chronology; no timestamps or actors were inferred.', 1;

        DECLARE @CycleMap TABLE (
            InventoryItemId nvarchar(36) NOT NULL PRIMARY KEY,
            StorageCycleId nvarchar(36) NOT NULL
        );

        INSERT INTO @CycleMap (InventoryItemId, StorageCycleId)
        SELECT Id, LOWER(CONVERT(nvarchar(36), NEWID()))
        FROM [{Schema}].[InventoryItem];

        INSERT INTO [{Schema}].[StorageCycle] (
            Id, InventoryItemId, LocationId, StoredAtUtc, IssuedAtUtc,
            StoredByPersonnelId, IssuedByPersonnelId,
            LocationCodeSnapshot, LocationNameSnapshot,
            StoredByPersonnelCodeSnapshot, StoredByPersonnelNameSnapshot,
            IssuedByPersonnelCodeSnapshot, IssuedByPersonnelNameSnapshot
        )
        SELECT
            c.StorageCycleId,
            i.Id,
            i.LocationId,
            i.CreatedAtUtc,
            CASE WHEN i.IsIssued = 1 THEN issue.MovedAtUtc END,
            i.RegisteredByPersonnelId,
            CASE WHEN i.IsIssued = 1 THEN issue.PerformedByPersonnelId END,
            location.LocationCode,
            location.LocationName,
            storedBy.PersonnelCode,
            storedBy.Name,
            CASE WHEN i.IsIssued = 1 THEN issuedBy.PersonnelCode END,
            CASE WHEN i.IsIssued = 1 THEN issuedBy.Name END
        FROM [{Schema}].[InventoryItem] i
        JOIN @CycleMap c ON c.InventoryItemId = i.Id
        JOIN [{Schema}].[StoreLocation] location ON location.Id = i.LocationId
        JOIN [{Schema}].[Personnel] storedBy ON storedBy.Id = i.RegisteredByPersonnelId
        LEFT JOIN [{Schema}].[InventoryMovement] issue
            ON issue.InventoryItemId = i.Id
           AND issue.EventType = N'{MovementEventType.IssueForPacking}'
        LEFT JOIN [{Schema}].[Personnel] issuedBy ON issuedBy.Id = issue.PerformedByPersonnelId;

        DROP TRIGGER IF EXISTS [{Schema}].[TR_InventoryMovement_Immutable];

        UPDATE movement
        SET StorageCycleId = cycle.StorageCycleId
        FROM [{Schema}].[InventoryMovement] movement
        JOIN @CycleMap cycle ON cycle.InventoryItemId = movement.InventoryItemId
        WHERE movement.EventType IN (N'{MovementEventType.InitialRegistration}', N'{MovementEventType.IssueForPacking}');

        IF (SELECT COUNT_BIG(*) FROM [{Schema}].[StorageCycle])
                <> (SELECT COUNT_BIG(*) FROM [{Schema}].[InventoryItem])
            THROW 51005, N'Storage-cycle backfill count does not match inventory item count.', 1;

        IF EXISTS (
            SELECT i.Id
            FROM [{Schema}].[InventoryItem] i
            LEFT JOIN [{Schema}].[StorageCycle] c ON c.InventoryItemId = i.Id
            GROUP BY i.Id, i.IsIssued
            HAVING COUNT(c.Id) <> 1
                OR (i.IsIssued = 0 AND SUM(CASE WHEN c.Id IS NOT NULL AND c.IssuedAtUtc IS NULL THEN 1 ELSE 0 END) <> 1)
                OR (i.IsIssued = 1 AND SUM(CASE WHEN c.Id IS NOT NULL AND c.IssuedAtUtc IS NULL THEN 1 ELSE 0 END) <> 0)
        )
            THROW 51005, N'Storage-cycle backfill does not match the existing issued state.', 1;

        IF EXISTS (
            SELECT c.Id
            FROM [{Schema}].[StorageCycle] c
            LEFT JOIN [{Schema}].[InventoryMovement] m ON m.StorageCycleId = c.Id
            GROUP BY c.Id, c.IssuedAtUtc
            HAVING SUM(CASE WHEN m.EventType IN (N'{MovementEventType.InitialRegistration}', N'{MovementEventType.ReturnedToStorage}') THEN 1 ELSE 0 END) <> 1
                OR SUM(CASE WHEN m.EventType = N'{MovementEventType.IssueForPacking}' THEN 1 ELSE 0 END)
                    <> CASE WHEN c.IssuedAtUtc IS NULL THEN 0 ELSE 1 END
        )
            THROW 51005, N'Storage-cycle backfill movement pairs are inconsistent.', 1;

        IF (SELECT COUNT_BIG(*) FROM [{Schema}].[InventoryMovement]) <> @MovementCount
           OR (SELECT COUNT_BIG(*) FROM [{Schema}].[AuditEvent]) <> @AuditCount
            THROW 51005, N'Backfill changed the existing movement or audit row count.', 1;
        """;

    public const string ValidateStorageCycleRollback = $"""
        IF EXISTS (
            SELECT 1
            FROM [{Schema}].[InventoryMovement]
            WHERE EventType = N'{MovementEventType.ReturnedToStorage}'
        )
            THROW 51006, N'Storage-cycle migration cannot be rolled back after a return-to-storage movement.', 1;

        IF (SELECT COUNT_BIG(*) FROM [{Schema}].[StorageCycle])
                <> (SELECT COUNT_BIG(*) FROM [{Schema}].[InventoryItem])
            THROW 51006, N'Storage-cycle migration cannot be rolled back after additional cycles exist.', 1;
        """;

    public const string CreateStorageCycleImmutableTrigger = $"""
        CREATE OR ALTER TRIGGER [{Schema}].[TR_StorageCycle_Immutable]
        ON [{Schema}].[StorageCycle]
        AFTER UPDATE, DELETE
        AS
        BEGIN
            SET NOCOUNT ON;

            IF EXISTS (
                SELECT 1
                FROM deleted d
                LEFT JOIN inserted i ON i.Id = d.Id
                WHERE i.Id IS NULL
            )
                THROW 51003, N'Storage cycles cannot be deleted.', 1;

            IF EXISTS (
                SELECT 1
                FROM inserted i
                LEFT JOIN deleted d ON d.Id = i.Id
                WHERE d.Id IS NULL
            )
                THROW 51003, N'Storage cycle identity cannot be changed.', 1;

            IF EXISTS (
                SELECT 1
                FROM deleted d
                JOIN inserted i ON i.Id = d.Id
                WHERE d.IssuedAtUtc IS NOT NULL
                   OR i.IssuedAtUtc IS NULL
                   OR i.IssuedAtUtc < d.StoredAtUtc
                   OR i.IssuedByPersonnelId IS NULL
                   OR i.InventoryItemId <> d.InventoryItemId
                   OR i.LocationId <> d.LocationId
                   OR i.StoredAtUtc <> d.StoredAtUtc
                   OR i.StoredByPersonnelId <> d.StoredByPersonnelId
                   OR i.LocationCodeSnapshot <> d.LocationCodeSnapshot
                   OR i.LocationNameSnapshot <> d.LocationNameSnapshot
                   OR i.StoredByPersonnelCodeSnapshot <> d.StoredByPersonnelCodeSnapshot
                   OR i.StoredByPersonnelNameSnapshot <> d.StoredByPersonnelNameSnapshot
                   OR i.IssuedByPersonnelCodeSnapshot IS NULL
                   OR i.IssuedByPersonnelNameSnapshot IS NULL
            )
                THROW 51003, N'Only an active storage cycle may be closed, and its stored identity is immutable.', 1;
        END
        """;

    public const string CreateInventoryMovementLifecycleTrigger = $"""
        CREATE OR ALTER TRIGGER [{Schema}].[TR_InventoryMovement_ValidateLifecycle]
        ON [{Schema}].[InventoryMovement]
        AFTER INSERT
        AS
        BEGIN
            SET NOCOUNT ON;

            IF EXISTS (
                SELECT 1
                FROM inserted m
                JOIN [{Schema}].[StorageCycle] c
                    ON c.Id = m.StorageCycleId
                   AND c.InventoryItemId = m.InventoryItemId
                WHERE m.EventType IN (N'{MovementEventType.InitialRegistration}', N'{MovementEventType.ReturnedToStorage}')
                  AND (
                      c.IssuedAtUtc IS NOT NULL
                      OR c.StoredAtUtc <> m.MovedAtUtc
                      OR c.StoredByPersonnelId <> m.PerformedByPersonnelId
                      OR m.ToLocationId IS NULL
                      OR c.LocationId <> m.ToLocationId
                      OR m.FromLocationId IS NOT NULL
                  )
            )
                THROW 51004, N'A storage-opening movement must match its active cycle boundary.', 1;

            IF EXISTS (
                SELECT 1
                FROM inserted m
                JOIN [{Schema}].[StorageCycle] c
                    ON c.Id = m.StorageCycleId
                   AND c.InventoryItemId = m.InventoryItemId
                WHERE m.EventType = N'{MovementEventType.InitialRegistration}'
                  AND EXISTS (
                      SELECT 1
                      FROM [{Schema}].[StorageCycle] prior
                      WHERE prior.InventoryItemId = c.InventoryItemId
                        AND prior.Id <> c.Id
                  )
            )
                THROW 51004, N'Initial registration may open only an item''s first storage cycle.', 1;

            IF EXISTS (
                SELECT 1
                FROM inserted m
                JOIN [{Schema}].[StorageCycle] c
                    ON c.Id = m.StorageCycleId
                   AND c.InventoryItemId = m.InventoryItemId
                WHERE m.EventType = N'{MovementEventType.ReturnedToStorage}'
                  AND NOT EXISTS (
                      SELECT 1
                      FROM [{Schema}].[StorageCycle] prior
                      WHERE prior.InventoryItemId = c.InventoryItemId
                        AND prior.Id <> c.Id
                        AND prior.IssuedAtUtc IS NOT NULL
                        AND prior.IssuedAtUtc <= c.StoredAtUtc
                  )
            )
                THROW 51004, N'A return movement requires an earlier completed storage cycle.', 1;

            IF EXISTS (
                SELECT 1
                FROM inserted m
                JOIN [{Schema}].[StorageCycle] c
                    ON c.Id = m.StorageCycleId
                   AND c.InventoryItemId = m.InventoryItemId
                WHERE m.EventType = N'{MovementEventType.IssueForPacking}'
                  AND (
                      c.IssuedAtUtc IS NULL
                      OR c.IssuedAtUtc <> m.MovedAtUtc
                      OR c.IssuedByPersonnelId <> m.PerformedByPersonnelId
                      OR m.FromLocationId IS NULL
                      OR c.LocationId <> m.FromLocationId
                      OR m.ToLocationId IS NOT NULL
                  )
            )
                THROW 51004, N'An issue movement must match its closed storage cycle boundary.', 1;

            IF EXISTS (
                SELECT m.StorageCycleId
                FROM [{Schema}].[InventoryMovement] m
                WHERE m.StorageCycleId IN (SELECT StorageCycleId FROM inserted WHERE EventType IN (
                    N'{MovementEventType.InitialRegistration}', N'{MovementEventType.ReturnedToStorage}'))
                  AND m.EventType IN (N'{MovementEventType.InitialRegistration}', N'{MovementEventType.ReturnedToStorage}')
                GROUP BY m.StorageCycleId
                HAVING COUNT_BIG(*) <> 1
            )
                THROW 51004, N'A storage cycle may have exactly one opening movement.', 1;
        END
        """;
}
