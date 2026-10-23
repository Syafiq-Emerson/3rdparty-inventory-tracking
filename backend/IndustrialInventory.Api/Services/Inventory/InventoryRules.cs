using IndustrialInventory.Api.DTOs;

namespace IndustrialInventory.Api.Services.Inventory;

/// <summary>Input rules for the inventory endpoints, kept free of HTTP and EF so they can be tested.</summary>
internal static class InventoryRules
{
    public const int IdentifierMaxLength = 100;
    public const int IdempotencyKeyMaxLength = 64;
    public const int MaxPageSize = 200;
    public const string DuplicateErrorCode = "DUPLICATE_IDENTIFIER";

    public static (int Page, int PageSize) ClampPaging(int page, int pageSize) =>
        (Math.Max(1, page), Math.Clamp(pageSize, 1, MaxPageSize));

    /// <summary>Blank becomes null; longer keys are cut to the column length.</summary>
    public static string? NormalizeIdempotencyKey(string? raw)
    {
        var key = raw?.Trim();
        if (string.IsNullOrEmpty(key)) return null;
        return key.Length <= IdempotencyKeyMaxLength ? key : key[..IdempotencyKeyMaxLength];
    }

    /// <summary>
    /// "%text%" for LIKE with \ as the escape character. [ is a wildcard in SQL Server, so it is
    /// escaped along with % and _.
    /// </summary>
    public static string ContainsPattern(string text)
    {
        var escaped = text.Trim()
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_")
            .Replace("[", "\\[");
        return $"%{escaped}%";
    }

    /// <summary>The 409 body for a serial or tag that already exists. Serial wins when both clash.</summary>
    public static DuplicateConflictResponse DuplicateConflict(InventoryItemResponse existing, string serial, string tag)
    {
        var bySerial = existing.SerialNumber == serial;
        return new DuplicateConflictResponse
        {
            ErrorCode = DuplicateErrorCode,
            Message = bySerial
                ? $"Serial Number '{serial}' is already registered in the system."
                : $"Tag Number '{tag}' is already registered in the system.",
            ConflictingField = bySerial ? "SerialNumber" : "TagNumber",
            ConflictingValue = bySerial ? serial : tag,
            ExistingItem = existing
        };
    }
}
