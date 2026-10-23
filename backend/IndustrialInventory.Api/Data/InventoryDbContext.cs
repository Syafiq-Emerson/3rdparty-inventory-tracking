using IndustrialInventory.Api.Models;
using IndustrialInventory.Api.Models.Audit;
using Microsoft.EntityFrameworkCore;

namespace IndustrialInventory.Api.Data;

public class InventoryDbContext(DbContextOptions<InventoryDbContext> options) : DbContext(options)
{
    public const string Schema = "industrialinventory";

    /// <summary>Fixed so seeded rows do not produce a new migration on every build.</summary>
    private static readonly DateTime SeedTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public DbSet<StoreLocation> StoreLocations => Set<StoreLocation>();
    public DbSet<Personnel> Personnel => Set<Personnel>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<StoreLocation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.LocationCode).IsUnique().HasDatabaseName("UX_StoreLocation_LocationCode");
            entity.HasIndex(e => e.IsActive).HasDatabaseName("IX_StoreLocation_IsActive");
            entity.Property(e => e.Id).HasMaxLength(36);
            entity.Property(e => e.LocationCode).IsRequired().HasMaxLength(50);
            entity.Property(e => e.LocationName).IsRequired().HasMaxLength(150);
            entity.Property(e => e.CreatedAtUtc).HasPrecision(3);
            entity.Property(e => e.UpdatedAtUtc).HasPrecision(3);
        });

        modelBuilder.Entity<Personnel>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.PersonnelCode).IsUnique().HasDatabaseName("UX_Personnel_PersonnelCode");
            entity.HasIndex(e => e.IsActive).HasDatabaseName("IX_Personnel_IsActive");
            entity.Property(e => e.Id).HasMaxLength(36);
            entity.Property(e => e.PersonnelCode).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(150);
            entity.Property(e => e.CreatedAtUtc).HasPrecision(3);
            entity.Property(e => e.UpdatedAtUtc).HasPrecision(3);
        });

        modelBuilder.Entity<InventoryItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SerialNumber).IsUnique().HasDatabaseName("UX_InventoryItem_SerialNumber");
            entity.HasIndex(e => e.TagNumber).IsUnique().HasDatabaseName("UX_InventoryItem_TagNumber");
            entity.HasIndex(e => e.LocationId).HasDatabaseName("IX_InventoryItem_LocationId");
            entity.HasIndex(e => e.RegisteredByPersonnelId).HasDatabaseName("IX_InventoryItem_RegisteredByPersonnelId");
            entity.HasIndex(e => e.CreatedAtUtc).IsDescending().HasDatabaseName("IX_InventoryItem_CreatedAtUtc");

            entity.Property(e => e.Id).HasMaxLength(36);
            entity.Property(e => e.SerialNumber).IsRequired().HasMaxLength(100);
            entity.Property(e => e.TagNumber).IsRequired().HasMaxLength(100);
            entity.Property(e => e.IsIssued).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.CreatedAtUtc).HasPrecision(3);
            entity.Property(e => e.UpdatedAtUtc).HasPrecision(3);

            entity.HasOne(d => d.Location)
                .WithMany(p => p.InventoryItems)
                .HasForeignKey(d => d.LocationId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_InventoryItem_StoreLocation");

            entity.HasOne(d => d.RegisteredByPersonnel)
                .WithMany(p => p.RegisteredInventoryItems)
                .HasForeignKey(d => d.RegisteredByPersonnelId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_InventoryItem_Personnel");
        });

        modelBuilder.Entity<InventoryMovement>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.InventoryItemId).HasDatabaseName("IX_InventoryMovement_InventoryItemId");
            entity.HasIndex(e => e.ToLocationId).HasDatabaseName("IX_InventoryMovement_ToLocationId");
            entity.HasIndex(e => e.PerformedByPersonnelId).HasDatabaseName("IX_InventoryMovement_PerformedByPersonnelId");
            entity.HasIndex(e => e.MovedAtUtc).IsDescending().HasDatabaseName("IX_InventoryMovement_MovedAtUtc");
            entity.HasIndex(e => e.IdempotencyKey)
                .IsUnique()
                .HasFilter("[IdempotencyKey] IS NOT NULL")
                .HasDatabaseName("UX_InventoryMovement_IdempotencyKey");

            entity.Property(e => e.Id).HasMaxLength(36);
            entity.Property(e => e.EventType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.IdempotencyKey).HasMaxLength(64);
            entity.Property(e => e.MovedAtUtc).HasPrecision(3);

            entity.ToTable(t => t.HasCheckConstraint(
                "CK_InventoryMovement_EventType",
                $"[EventType] IN ('{MovementEventType.InitialRegistration}', '{MovementEventType.IssueForPacking}')"));

            // Restrict, not Cascade: ledger history outlives the item, and SQL Server forbids an
            // INSTEAD OF trigger on a table reached by a cascading delete.
            entity.HasOne(d => d.InventoryItem)
                .WithMany(p => p.Movements)
                .HasForeignKey(d => d.InventoryItemId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_InventoryMovement_InventoryItem");

            entity.HasOne(d => d.FromLocation)
                .WithMany(p => p.MovementsFrom)
                .HasForeignKey(d => d.FromLocationId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_InventoryMovement_FromLocation");

            entity.HasOne(d => d.ToLocation)
                .WithMany(p => p.MovementsTo)
                .HasForeignKey(d => d.ToLocationId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_InventoryMovement_ToLocation");

            entity.HasOne(d => d.PerformedByPersonnel)
                .WithMany(p => p.PerformedMovements)
                .HasForeignKey(d => d.PerformedByPersonnelId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_InventoryMovement_Personnel");
        });

        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserName).IsUnique().HasDatabaseName("UX_AppUser_UserName");
            entity.Property(e => e.Id).HasMaxLength(36);
            entity.Property(e => e.UserName).IsRequired().HasMaxLength(50);
            entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(400);
            entity.Property(e => e.Role).IsRequired().HasMaxLength(20);
            entity.Property(e => e.CreatedAtUtc).HasPrecision(3);
            entity.Property(e => e.UpdatedAtUtc).HasPrecision(3);

            entity.ToTable(t => t.HasCheckConstraint(
                "CK_AppUser_Role",
                $"[Role] IN ('{UserRole.Admin}', '{UserRole.Operator}')"));
        });

        modelBuilder.Entity<AuditEvent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.HasIndex(e => e.OccurredAtUtc).IsDescending().HasDatabaseName("IX_AuditEvent_OccurredAtUtc");
            entity.HasIndex(e => new { e.EntityName, e.EntityId }).HasDatabaseName("IX_AuditEvent_Entity");
            entity.HasIndex(e => e.UserName).HasDatabaseName("IX_AuditEvent_UserName");
            entity.HasIndex(e => e.ActionType).HasDatabaseName("IX_AuditEvent_ActionType");

            entity.Property(e => e.OccurredAtUtc).HasPrecision(3);
            entity.Property(e => e.ActionType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.EntityName).IsRequired().HasMaxLength(50);
            entity.Property(e => e.EntityId).HasMaxLength(50);
            entity.Property(e => e.UserName).IsRequired().HasMaxLength(50);
            entity.Property(e => e.OperatorName).HasMaxLength(100);
            entity.Property(e => e.UserAgent).HasMaxLength(400);
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.Property(e => e.ReferenceNo).HasMaxLength(50);
            entity.Property(e => e.Remarks).HasMaxLength(500);
        });

        // No usable password: the account cannot sign in until "set-admin-password" is run.
        modelBuilder.Entity<AppUser>().HasData(new AppUser
        {
            Id = "usr-admin",
            UserName = "admin",
            PasswordHash = string.Empty,
            Role = UserRole.Admin,
            IsActive = true,
            MustChangePassword = true,
            CreatedAtUtc = SeedTimestamp,
            UpdatedAtUtc = SeedTimestamp
        });
    }
}
