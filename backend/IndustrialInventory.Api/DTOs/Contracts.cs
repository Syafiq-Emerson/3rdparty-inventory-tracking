using System.ComponentModel.DataAnnotations;

namespace IndustrialInventory.Api.DTOs;

// --- Inventory DTOs ---
public record RegisterInventoryRequest
{
    [Required(ErrorMessage = "Serial Number is required.")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Serial Number must be between 1 and 100 characters.")]
    public string SerialNumber { get; init; } = string.Empty;

    [Required(ErrorMessage = "Tag Number is required.")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Tag Number must be between 1 and 100 characters.")]
    public string TagNumber { get; init; } = string.Empty;

    [Required(ErrorMessage = "Store Location ID is required.")]
    public string StoreLocationId { get; init; } = string.Empty;

    [Required(ErrorMessage = "Scan By Personnel ID is required.")]
    public string ScanByPersonnelId { get; init; } = string.Empty;
}

public record InventoryItemResponse
{
    public string Id { get; init; } = string.Empty;
    public string SerialNumber { get; init; } = string.Empty;
    public string TagNumber { get; init; } = string.Empty;
    public string LocationId { get; init; } = string.Empty;
    public string LocationCode { get; init; } = string.Empty;
    public string LocationName { get; init; } = string.Empty;
    public string RegisteredByPersonnelId { get; init; } = string.Empty;
    public string RegisteredByPersonnelCode { get; init; } = string.Empty;
    public string RegisteredByPersonnelName { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public record DuplicateConflictResponse
{
    public string ErrorCode { get; init; } = "DUPLICATE_IDENTIFIER";
    public string Message { get; init; } = string.Empty;
    public string ConflictingField { get; init; } = string.Empty; // "SerialNumber" or "TagNumber"
    public string ConflictingValue { get; init; } = string.Empty;
    public InventoryItemResponse ExistingItem { get; init; } = null!;
}

public record InventoryMovementResponse
{
    public string Id { get; init; } = string.Empty;
    public string InventoryItemId { get; init; } = string.Empty;
    public string? FromLocationId { get; init; }
    public string? FromLocationName { get; init; }
    public string ToLocationId { get; init; } = string.Empty;
    public string ToLocationName { get; init; } = string.Empty;
    public string PerformedByPersonnelId { get; init; } = string.Empty;
    public string PerformedByPersonnelName { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public DateTime MovedAt { get; init; }
}

// --- Dashboard DTOs ---
public record DashboardSummaryResponse
{
    public int TotalRegisteredUnits { get; init; }
    public int ActiveStoreLocationsCount { get; init; }
    public int ActivePersonnelCount { get; init; }
    public List<LocationInventoryCountDto> InventoryByStoreLocation { get; init; } = new();
    public List<InventoryItemResponse> RecentlyRegisteredInventory { get; init; } = new();
}

public record LocationInventoryCountDto
{
    public string LocationId { get; init; } = string.Empty;
    public string LocationCode { get; init; } = string.Empty;
    public string LocationName { get; init; } = string.Empty;
    public int ItemCount { get; init; }
    public bool IsActive { get; init; }
}

// --- Store Location DTOs ---
public record CreateStoreLocationRequest
{
    [Required]
    [StringLength(50)]
    public string LocationCode { get; init; } = string.Empty;

    [Required]
    [StringLength(150)]
    public string LocationName { get; init; } = string.Empty;
}

public record UpdateStoreLocationRequest
{
    [Required]
    [StringLength(50)]
    public string LocationCode { get; init; } = string.Empty;

    [Required]
    [StringLength(150)]
    public string LocationName { get; init; } = string.Empty;
}

public record UpdateStatusRequest
{
    public bool IsActive { get; init; }
}

public record StoreLocationResponse
{
    public string Id { get; init; } = string.Empty;
    public string LocationCode { get; init; } = string.Empty;
    public string LocationName { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public int ItemCount { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

// --- Personnel DTOs ---
public record CreatePersonnelRequest
{
    [Required]
    [StringLength(50)]
    public string PersonnelCode { get; init; } = string.Empty;

    [Required]
    [StringLength(150)]
    public string Name { get; init; } = string.Empty;
}

public record UpdatePersonnelRequest
{
    [Required]
    [StringLength(50)]
    public string PersonnelCode { get; init; } = string.Empty;

    [Required]
    [StringLength(150)]
    public string Name { get; init; } = string.Empty;
}

public record PersonnelResponse
{
    public string Id { get; init; } = string.Empty;
    public string PersonnelCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public int RegisteredItemsCount { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
