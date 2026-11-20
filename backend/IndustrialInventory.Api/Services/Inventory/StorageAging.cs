using IndustrialInventory.Api.Models;

namespace IndustrialInventory.Api.Services.Inventory;

public static class StorageAging
{
    public sealed record Summary(string? CurrentStorageCycleId, int? CurrentAgingDays, int TotalStorageDays);

    public static DateTime AsUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    public static int WholeDays(DateTime storedAtUtc, DateTime endedAtUtc)
    {
        var storedUtc = AsUtc(storedAtUtc);
        var endedUtc = AsUtc(endedAtUtc);
        if (endedUtc < storedUtc)
        {
            throw new InvalidOperationException("A storage cycle ends before it starts.");
        }

        return checked((int)((endedUtc - storedUtc).Ticks / TimeSpan.TicksPerDay));
    }

    public static Summary Summarize(bool isIssued, IReadOnlyCollection<StorageCycle> cycles, DateTime nowUtc)
    {
        var activeCycles = cycles.Where(cycle => cycle.IssuedAtUtc is null).ToArray();
        if (activeCycles.Length > 1 ||
            (isIssued && (activeCycles.Length != 0 || cycles.Count == 0)) ||
            (!isIssued && activeCycles.Length != 1))
        {
            throw new InvalidOperationException("The inventory item's storage-cycle history is inconsistent.");
        }

        var activeCycle = activeCycles.SingleOrDefault();
        var totalDays = 0;
        foreach (var cycle in cycles)
        {
            totalDays = checked(totalDays + WholeDays(cycle.StoredAtUtc, cycle.IssuedAtUtc ?? nowUtc));
        }

        return new Summary(
            activeCycle?.Id,
            activeCycle is null ? null : WholeDays(activeCycle.StoredAtUtc, nowUtc),
            totalDays);
    }
}
