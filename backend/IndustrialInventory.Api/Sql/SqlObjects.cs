using IndustrialInventory.Api.Data;

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
}
