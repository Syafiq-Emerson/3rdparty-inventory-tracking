using IndustrialInventory.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace IndustrialInventory.Api.Tests.Support;

/// <summary>A clock the test moves by hand.</summary>
internal sealed class FakeClock(DateTimeOffset start) : TimeProvider
{
    public DateTimeOffset Now { get; private set; } = start;

    public override DateTimeOffset GetUtcNow() => Now;

    public void Advance(TimeSpan by) => Now += by;
}

internal static class OfflineDb
{
    /// <summary>
    /// A context with the real SQL Server provider that never opens a connection: enough for the model,
    /// ToQueryString and the change tracker.
    /// </summary>
    public static InventoryDbContext Create() =>
        new(new DbContextOptionsBuilder<InventoryDbContext>()
            .UseSqlServer("Server=offline;Database=offline;Trusted_Connection=True")
            .Options);
}
