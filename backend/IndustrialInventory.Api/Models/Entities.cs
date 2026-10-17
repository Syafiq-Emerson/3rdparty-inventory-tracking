using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IndustrialInventory.Api.Models;

[Table("store_locations")]
public class StoreLocation
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [Column("location_code")]
    [MaxLength(50)]
    public string LocationCode { get; set; } = string.Empty;

    [Required]
    [Column("location_name")]
    [MaxLength(150)]
    public string LocationName { get; set; } = string.Empty;

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ICollection<InventoryItem> InventoryItems { get; set; } = new List<InventoryItem>();
    public virtual ICollection<InventoryMovement> MovementsTo { get; set; } = new List<InventoryMovement>();
    public virtual ICollection<InventoryMovement> MovementsFrom { get; set; } = new List<InventoryMovement>();
}

[Table("personnel")]
public class Personnel
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [Column("personnel_code")]
    [MaxLength(50)]
    public string PersonnelCode { get; set; } = string.Empty;

    [Required]
    [Column("name")]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ICollection<InventoryItem> RegisteredInventoryItems { get; set; } = new List<InventoryItem>();
    public virtual ICollection<InventoryMovement> PerformedMovements { get; set; } = new List<InventoryMovement>();
}

[Table("inventory_items")]
public class InventoryItem
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [Column("serial_number")]
    [MaxLength(100)]
    public string SerialNumber { get; set; } = string.Empty;

    [Required]
    [Column("tag_number")]
    [MaxLength(100)]
    public string TagNumber { get; set; } = string.Empty;

    [Required]
    [Column("location_id")]
    [MaxLength(36)]
    public string LocationId { get; set; } = string.Empty;

    [ForeignKey(nameof(LocationId))]
    public virtual StoreLocation Location { get; set; } = null!;

    [Required]
    [Column("registered_by_personnel_id")]
    [MaxLength(36)]
    public string RegisteredByPersonnelId { get; set; } = string.Empty;

    [ForeignKey(nameof(RegisteredByPersonnelId))]
    public virtual Personnel RegisteredByPersonnel { get; set; } = null!;

    [Column("is_issued")]
    public bool IsIssued { get; set; } = false;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ICollection<InventoryMovement> Movements { get; set; } = new List<InventoryMovement>();
}

[Table("inventory_movements")]
public class InventoryMovement
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [Column("inventory_item_id")]
    [MaxLength(36)]
    public string InventoryItemId { get; set; } = string.Empty;

    [ForeignKey(nameof(InventoryItemId))]
    public virtual InventoryItem InventoryItem { get; set; } = null!;

    [Column("from_location_id")]
    [MaxLength(36)]
    public string? FromLocationId { get; set; }

    [ForeignKey(nameof(FromLocationId))]
    public virtual StoreLocation? FromLocation { get; set; }

    [Column("to_location_id")]
    [MaxLength(36)]
    public string? ToLocationId { get; set; }

    [ForeignKey(nameof(ToLocationId))]
    public virtual StoreLocation? ToLocation { get; set; }

    [Required]
    [Column("performed_by_personnel_id")]
    [MaxLength(36)]
    public string PerformedByPersonnelId { get; set; } = string.Empty;

    [ForeignKey(nameof(PerformedByPersonnelId))]
    public virtual Personnel PerformedByPersonnel { get; set; } = null!;

    [Required]
    [Column("event_type")]
    [MaxLength(50)]
    public string EventType { get; set; } = "INITIAL_REGISTRATION";

    [Column("moved_at")]
    public DateTime MovedAt { get; set; } = DateTime.UtcNow;
}
