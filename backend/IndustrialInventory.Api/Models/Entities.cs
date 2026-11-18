using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IndustrialInventory.Api.Models;

/// <summary>Allowed values for <see cref="InventoryMovement.EventType"/>; enforced by CK_InventoryMovement_EventType.</summary>
public static class MovementEventType
{
    public const string InitialRegistration = "INITIAL_REGISTRATION";
    public const string IssueForPacking = "ISSUE_FOR_PACKING";
    public const string ReturnedToStorage = "RETURNED_TO_STORAGE";

    public static readonly string[] All = [InitialRegistration, IssueForPacking, ReturnedToStorage];
}

/// <summary>Allowed values for <see cref="AppUser.Role"/>; enforced by CK_AppUser_Role.</summary>
public static class UserRole
{
    public const string Admin = "Admin";
    public const string Operator = "Operator";

    public static readonly string[] All = [Admin, Operator];
}

[Table("StoreLocation")]
public class StoreLocation
{
    [Key]
    [Column("Id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [Column("LocationCode")]
    [MaxLength(50)]
    public string LocationCode { get; set; } = string.Empty;

    [Required]
    [Column("LocationName")]
    [MaxLength(150)]
    public string LocationName { get; set; } = string.Empty;

    [Column("IsActive")]
    public bool IsActive { get; set; } = true;

    [Column("CreatedAtUtc")]
    public DateTime CreatedAtUtc { get; set; }

    [Column("UpdatedAtUtc")]
    public DateTime UpdatedAtUtc { get; set; }

    public virtual ICollection<InventoryItem> InventoryItems { get; set; } = new List<InventoryItem>();
    public virtual ICollection<InventoryMovement> MovementsTo { get; set; } = new List<InventoryMovement>();
    public virtual ICollection<InventoryMovement> MovementsFrom { get; set; } = new List<InventoryMovement>();
}

[Table("Personnel")]
public class Personnel
{
    [Key]
    [Column("Id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [Column("PersonnelCode")]
    [MaxLength(50)]
    public string PersonnelCode { get; set; } = string.Empty;

    [Required]
    [Column("Name")]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [Column("IsActive")]
    public bool IsActive { get; set; } = true;

    [Column("CreatedAtUtc")]
    public DateTime CreatedAtUtc { get; set; }

    [Column("UpdatedAtUtc")]
    public DateTime UpdatedAtUtc { get; set; }

    public virtual ICollection<InventoryItem> RegisteredInventoryItems { get; set; } = new List<InventoryItem>();
    public virtual ICollection<InventoryMovement> PerformedMovements { get; set; } = new List<InventoryMovement>();
}

[Table("InventoryItem")]
public class InventoryItem
{
    [Key]
    [Column("Id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [Column("SerialNumber")]
    [MaxLength(100)]
    public string SerialNumber { get; set; } = string.Empty;

    [Required]
    [Column("TagNumber")]
    [MaxLength(100)]
    public string TagNumber { get; set; } = string.Empty;

    [Required]
    [Column("LocationId")]
    [MaxLength(36)]
    public string LocationId { get; set; } = string.Empty;

    [ForeignKey(nameof(LocationId))]
    public virtual StoreLocation Location { get; set; } = null!;

    [Required]
    [Column("RegisteredByPersonnelId")]
    [MaxLength(36)]
    public string RegisteredByPersonnelId { get; set; } = string.Empty;

    [ForeignKey(nameof(RegisteredByPersonnelId))]
    public virtual Personnel RegisteredByPersonnel { get; set; } = null!;

    [Column("IsIssued")]
    public bool IsIssued { get; set; }

    [Column("CreatedAtUtc")]
    public DateTime CreatedAtUtc { get; set; }

    [Column("UpdatedAtUtc")]
    public DateTime UpdatedAtUtc { get; set; }

    public virtual ICollection<InventoryMovement> Movements { get; set; } = new List<InventoryMovement>();
    public virtual ICollection<StorageCycle> StorageCycles { get; set; } = new List<StorageCycle>();
}

/// <summary>One uninterrupted period in which an inventory item is in storage.</summary>
[Table("StorageCycle")]
public class StorageCycle
{
    [Key]
    [Column("Id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [Column("InventoryItemId")]
    [MaxLength(36)]
    public string InventoryItemId { get; set; } = string.Empty;

    [ForeignKey(nameof(InventoryItemId))]
    public virtual InventoryItem InventoryItem { get; set; } = null!;

    [Required]
    [Column("LocationId")]
    [MaxLength(36)]
    public string LocationId { get; set; } = string.Empty;

    [ForeignKey(nameof(LocationId))]
    public virtual StoreLocation Location { get; set; } = null!;

    [Column("StoredAtUtc")]
    public DateTime StoredAtUtc { get; set; }

    [Column("IssuedAtUtc")]
    public DateTime? IssuedAtUtc { get; set; }

    [Required]
    [Column("StoredByPersonnelId")]
    [MaxLength(36)]
    public string StoredByPersonnelId { get; set; } = string.Empty;

    [ForeignKey(nameof(StoredByPersonnelId))]
    public virtual Personnel StoredByPersonnel { get; set; } = null!;

    [Column("IssuedByPersonnelId")]
    [MaxLength(36)]
    public string? IssuedByPersonnelId { get; set; }

    [ForeignKey(nameof(IssuedByPersonnelId))]
    public virtual Personnel? IssuedByPersonnel { get; set; }

    [Required]
    [Column("LocationCodeSnapshot")]
    [MaxLength(50)]
    public string LocationCodeSnapshot { get; set; } = string.Empty;

    [Required]
    [Column("LocationNameSnapshot")]
    [MaxLength(150)]
    public string LocationNameSnapshot { get; set; } = string.Empty;

    [Required]
    [Column("StoredByPersonnelCodeSnapshot")]
    [MaxLength(50)]
    public string StoredByPersonnelCodeSnapshot { get; set; } = string.Empty;

    [Required]
    [Column("StoredByPersonnelNameSnapshot")]
    [MaxLength(150)]
    public string StoredByPersonnelNameSnapshot { get; set; } = string.Empty;

    [Column("IssuedByPersonnelCodeSnapshot")]
    [MaxLength(50)]
    public string? IssuedByPersonnelCodeSnapshot { get; set; }

    [Column("IssuedByPersonnelNameSnapshot")]
    [MaxLength(150)]
    public string? IssuedByPersonnelNameSnapshot { get; set; }

    public virtual ICollection<InventoryMovement> Movements { get; set; } = new List<InventoryMovement>();
}

/// <summary>Append-only ledger row. Corrections are new reversing rows, never edits.</summary>
[Table("InventoryMovement")]
public class InventoryMovement
{
    [Key]
    [Column("Id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [Column("InventoryItemId")]
    [MaxLength(36)]
    public string InventoryItemId { get; set; } = string.Empty;

    [ForeignKey(nameof(InventoryItemId))]
    public virtual InventoryItem InventoryItem { get; set; } = null!;

    [Column("StorageCycleId")]
    [MaxLength(36)]
    public string? StorageCycleId { get; set; }

    [ForeignKey(nameof(StorageCycleId))]
    public virtual StorageCycle? StorageCycle { get; set; }

    [Column("FromLocationId")]
    [MaxLength(36)]
    public string? FromLocationId { get; set; }

    [ForeignKey(nameof(FromLocationId))]
    public virtual StoreLocation? FromLocation { get; set; }

    [Column("ToLocationId")]
    [MaxLength(36)]
    public string? ToLocationId { get; set; }

    [ForeignKey(nameof(ToLocationId))]
    public virtual StoreLocation? ToLocation { get; set; }

    [Required]
    [Column("PerformedByPersonnelId")]
    [MaxLength(36)]
    public string PerformedByPersonnelId { get; set; } = string.Empty;

    [ForeignKey(nameof(PerformedByPersonnelId))]
    public virtual Personnel PerformedByPersonnel { get; set; } = null!;

    [Required]
    [Column("EventType")]
    [MaxLength(50)]
    public string EventType { get; set; } = MovementEventType.InitialRegistration;

    [Column("MovedAtUtc")]
    public DateTime MovedAtUtc { get; set; }

    /// <summary>Set from the Idempotency-Key header so a retried post returns the original row.</summary>
    [Column("IdempotencyKey")]
    [MaxLength(64)]
    public string? IdempotencyKey { get; set; }
}

[Table("AppUser")]
public class AppUser
{
    [Key]
    [Column("Id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [Column("UserName")]
    [MaxLength(50)]
    public string UserName { get; set; } = string.Empty;

    /// <summary>Empty means no usable password: the account cannot sign in until one is set.</summary>
    [Required]
    [Column("PasswordHash")]
    [MaxLength(400)]
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    [Column("Role")]
    [MaxLength(20)]
    public string Role { get; set; } = UserRole.Operator;

    [Column("IsActive")]
    public bool IsActive { get; set; } = true;

    [Column("MustChangePassword")]
    public bool MustChangePassword { get; set; }

    [Column("CreatedAtUtc")]
    public DateTime CreatedAtUtc { get; set; }

    [Column("UpdatedAtUtc")]
    public DateTime UpdatedAtUtc { get; set; }
}
