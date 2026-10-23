using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace IndustrialInventory.Api.Data;

/// <summary>
/// Used by "dotnet ef" at design time. Reads user-secrets, then environment variables,
/// and falls back to LocalDB so migrations can be scaffolded on a clean machine.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<InventoryDbContext>
{
    private const string LocalDbFallback =
        "Server=(localdb)\\MSSQLLocalDB;Database=IndustrialInventory;Trusted_Connection=True;TrustServerCertificate=True";

    public InventoryDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddUserSecrets<DesignTimeDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("InventoryConnection");
        if (string.IsNullOrWhiteSpace(connectionString)) connectionString = LocalDbFallback;

        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseSqlServer(connectionString, sql =>
                sql.MigrationsHistoryTable("__EFMigrationsHistory", InventoryDbContext.Schema))
            .Options;

        return new InventoryDbContext(options);
    }
}
