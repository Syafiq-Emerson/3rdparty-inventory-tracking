using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IndustrialInventory.Api.Models.Audit;

/// <summary>Values for <see cref="AuditEvent.ActionType"/>.</summary>
public static class AuditAction
{
    public const string Create = "Create";
    public const string Update = "Update";
    public const string Deactivate = "Deactivate";
    public const string Restore = "Restore";
    public const string Login = "Login";
    public const string LoginFailed = "LoginFailed";
    public const string Logout = "Logout";
    public const string PasswordChanged = "PasswordChanged";
    public const string Post = "Post";
}

/// <summary>Values for <see cref="AuditEvent.EntityName"/>.</summary>
public static class AuditEntity
{
    public const string InventoryItem = "InventoryItem";
    public const string StoreLocation = "StoreLocation";
    public const string Personnel = "Personnel";
    public const string AppUser = "AppUser";
}

/// <summary>
/// Append-only audit row. TR_AuditEvent_Immutable blocks UPDATE and DELETE at the database.
/// </summary>
[Table("AuditEvent")]
public class AuditEvent
{
    [Key]
    [Column("Id")]
    public long Id { get; set; }

    [Column("OccurredAtUtc")]
    public DateTime OccurredAtUtc { get; set; }

    [Required]
    [Column("ActionType")]
    [MaxLength(50)]
    public string ActionType { get; set; } = string.Empty;

    [Required]
    [Column("EntityName")]
    [MaxLength(50)]
    public string EntityName { get; set; } = string.Empty;

    [Column("EntityId")]
    [MaxLength(50)]
    public string? EntityId { get; set; }

    [Required]
    [Column("UserName")]
    [MaxLength(50)]
    public string UserName { get; set; } = string.Empty;

    [Column("OperatorName")]
    [MaxLength(100)]
    public string? OperatorName { get; set; }

    [Column("UserAgent")]
    [MaxLength(400)]
    public string? UserAgent { get; set; }

    [Column("IpAddress")]
    [MaxLength(45)]
    public string? IpAddress { get; set; }

    [Column("CorrelationId")]
    public Guid? CorrelationId { get; set; }

    /// <summary>JSON of changed properties only. Never holds credentials.</summary>
    [Column("PreviousValue")]
    public string? PreviousValue { get; set; }

    /// <summary>JSON of changed properties only. Never holds credentials.</summary>
    [Column("NewValue")]
    public string? NewValue { get; set; }

    [Column("ReferenceNo")]
    [MaxLength(50)]
    public string? ReferenceNo { get; set; }

    [Column("Remarks")]
    [MaxLength(500)]
    public string? Remarks { get; set; }
}
