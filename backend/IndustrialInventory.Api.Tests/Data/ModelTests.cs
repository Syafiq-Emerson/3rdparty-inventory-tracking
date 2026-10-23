using IndustrialInventory.Api.Data;
using IndustrialInventory.Api.Models;
using IndustrialInventory.Api.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace IndustrialInventory.Api.Tests.Data;

/// <summary>Checks the model against the database standard before a migration is ever generated.</summary>
public class ModelTests
{
    private static readonly HashSet<string> UnboundedColumns = ["PreviousValue", "NewValue"];

    [Fact]
    public void Every_table_is_in_the_app_schema()
    {
        using var db = OfflineDb.Create();
        Assert.Equal("industrialinventory", InventoryDbContext.Schema);
        Assert.All(db.Model.GetEntityTypes(), e => Assert.Equal(InventoryDbContext.Schema, e.GetSchema()));
    }

    [Fact]
    public void Every_string_column_has_a_max_length()
    {
        using var db = OfflineDb.Create();
        var strings = db.Model.GetEntityTypes()
            .SelectMany(e => e.GetProperties())
            .Where(p => p.ClrType == typeof(string) && !UnboundedColumns.Contains(p.Name));

        Assert.All(strings, p => Assert.True(p.GetMaxLength() is not null,
            $"{p.DeclaringType.DisplayName()}.{p.Name} needs HasMaxLength."));
    }

    [Fact]
    public void Every_utc_timestamp_has_millisecond_precision()
    {
        using var db = OfflineDb.Create();
        var timestamps = db.Model.GetEntityTypes()
            .SelectMany(e => e.GetProperties())
            .Where(p => p.Name.EndsWith("AtUtc", StringComparison.Ordinal));

        Assert.All(timestamps, p => Assert.Equal(3, p.GetPrecision()));
    }

    [Fact]
    public void Only_the_admin_account_is_seeded()
    {
        using var db = OfflineDb.Create();
        var model = db.GetService<IDesignTimeModel>().Model;
        var seeded = model.GetEntityTypes().Where(e => e.GetSeedData().Any()).ToList();

        var only = Assert.Single(seeded);
        Assert.Equal(typeof(AppUser), only.ClrType);
        var admin = Assert.Single(only.GetSeedData());
        Assert.Equal("admin", admin[nameof(AppUser.UserName)]);
        Assert.Equal(string.Empty, admin[nameof(AppUser.PasswordHash)]);
    }

    [Fact]
    public void Ledger_foreign_keys_never_cascade()
    {
        using var db = OfflineDb.Create();
        var keys = db.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys());

        Assert.All(keys, k => Assert.NotEqual(DeleteBehavior.Cascade, k.DeleteBehavior));
    }
}
