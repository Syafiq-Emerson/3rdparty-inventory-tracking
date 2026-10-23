using System.Text.Json;
using IndustrialInventory.Api.Data;
using IndustrialInventory.Api.Models.Audit;

namespace IndustrialInventory.Api.Services.Shared;

/// <summary>
/// Adds audit rows to the current DbContext. The caller saves them, so the audit row commits
/// in the same transaction as the change it records.
/// </summary>
public sealed class AuditWriter(InventoryDbContext db, AuditContext context, TimeProvider timeProvider)
{
    private const int RemarksMaxLength = 500;

    /// <summary>Property names that must never be written into an audit row.</summary>
    private static readonly HashSet<string> RedactedProperties =
        new(StringComparer.OrdinalIgnoreCase) { "PasswordHash", "Password", "Token", "Secret" };

    public void Add(
        string actionType,
        string entityName,
        string? entityId,
        object? previousValue = null,
        object? newValue = null,
        string? referenceNo = null,
        string? remarks = null,
        string? userNameOverride = null)
    {
        db.AuditEvents.Add(new AuditEvent
        {
            OccurredAtUtc = timeProvider.GetUtcNow().UtcDateTime,
            ActionType = actionType,
            EntityName = entityName,
            EntityId = entityId,
            UserName = userNameOverride ?? context.UserName,
            UserAgent = context.UserAgent,
            IpAddress = context.IpAddress,
            CorrelationId = context.CorrelationId,
            PreviousValue = Serialize(previousValue),
            NewValue = Serialize(newValue),
            ReferenceNo = referenceNo,
            Remarks = remarks is null or "" ? null
                : remarks.Length <= RemarksMaxLength ? remarks
                : remarks[..RemarksMaxLength]
        });
    }

    private static string? Serialize(object? value)
    {
        if (value is null) return null;

        var properties = value.GetType().GetProperties()
            .Where(p => !RedactedProperties.Contains(p.Name))
            .ToDictionary(p => p.Name, p => p.GetValue(value));

        return properties.Count == 0 ? null : JsonSerializer.Serialize(properties);
    }
}
