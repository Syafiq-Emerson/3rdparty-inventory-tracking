using Microsoft.EntityFrameworkCore;
using IndustrialInventory.Api.Models;

namespace IndustrialInventory.Api.Data;

public class InventoryDbContext : DbContext
{
    public InventoryDbContext(DbContextOptions<InventoryDbContext> options)
        : base(options)
    {
    }

    public DbSet<StoreLocation> StoreLocations => Set<StoreLocation>();
    public DbSet<Personnel> Personnel => Set<Personnel>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // StoreLocation configuration
        modelBuilder.Entity<StoreLocation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.LocationCode).IsUnique().HasDatabaseName("uq_store_locations_code");
            entity.HasIndex(e => e.IsActive).HasDatabaseName("ix_store_locations_is_active");
            entity.Property(e => e.LocationCode).IsRequired().HasMaxLength(50);
            entity.Property(e => e.LocationName).IsRequired().HasMaxLength(150);
        });

        // Personnel configuration
        modelBuilder.Entity<Personnel>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.PersonnelCode).IsUnique().HasDatabaseName("uq_personnel_code");
            entity.HasIndex(e => e.IsActive).HasDatabaseName("ix_personnel_is_active");
            entity.Property(e => e.PersonnelCode).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(150);
        });

        // InventoryItem configuration
        modelBuilder.Entity<InventoryItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SerialNumber).IsUnique().HasDatabaseName("uq_inventory_items_serial");
            entity.HasIndex(e => e.TagNumber).IsUnique().HasDatabaseName("uq_inventory_items_tag");
            entity.HasIndex(e => e.LocationId).HasDatabaseName("ix_inventory_items_location");
            entity.HasIndex(e => e.RegisteredByPersonnelId).HasDatabaseName("ix_inventory_items_personnel");

            entity.Property(e => e.SerialNumber).IsRequired().HasMaxLength(100);
            entity.Property(e => e.TagNumber).IsRequired().HasMaxLength(100);
            entity.Property(e => e.IsIssued).IsRequired().HasDefaultValue(false);

            entity.HasOne(d => d.Location)
                .WithMany(p => p.InventoryItems)
                .HasForeignKey(d => d.LocationId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_inventory_items_location");

            entity.HasOne(d => d.RegisteredByPersonnel)
                .WithMany(p => p.RegisteredInventoryItems)
                .HasForeignKey(d => d.RegisteredByPersonnelId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_inventory_items_personnel");
        });

        // InventoryMovement configuration
        modelBuilder.Entity<InventoryMovement>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.InventoryItemId).HasDatabaseName("ix_inventory_movements_item");
            entity.HasIndex(e => e.ToLocationId).HasDatabaseName("ix_inventory_movements_to_loc");
            entity.HasIndex(e => e.PerformedByPersonnelId).HasDatabaseName("ix_inventory_movements_perf_by");

            entity.HasOne(d => d.InventoryItem)
                .WithMany(p => p.Movements)
                .HasForeignKey(d => d.InventoryItemId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_movements_item");

            entity.HasOne(d => d.FromLocation)
                .WithMany(p => p.MovementsFrom)
                .HasForeignKey(d => d.FromLocationId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_movements_from_location");

            entity.HasOne(d => d.ToLocation)
                .WithMany(p => p.MovementsTo)
                .HasForeignKey(d => d.ToLocationId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_movements_to_location");

            entity.HasOne(d => d.PerformedByPersonnel)
                .WithMany(p => p.PerformedMovements)
                .HasForeignKey(d => d.PerformedByPersonnelId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_movements_personnel");
        });
    }
}
