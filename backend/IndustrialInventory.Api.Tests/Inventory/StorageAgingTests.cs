using System.Text.Json;
using IndustrialInventory.Api.Controllers;
using IndustrialInventory.Api.DTOs;
using IndustrialInventory.Api.Models;
using IndustrialInventory.Api.Services.Inventory;
using IndustrialInventory.Api.Tests.Support;

namespace IndustrialInventory.Api.Tests.Inventory;

public class StorageAgingTests
{
    [Theory]
    [InlineData(23, 59, 0)]
    [InlineData(24, 0, 1)]
    [InlineData(25, 0, 1)]
    public void Active_cycle_age_uses_elapsed_whole_days(int hours, int minutes, int expectedDays)
    {
        var storedAt = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var clock = new FakeClock(new DateTimeOffset(storedAt.AddHours(hours).AddMinutes(minutes), TimeSpan.Zero));

        var summary = StorageAging.Summarize(false, [Cycle("active", storedAt)], clock.GetUtcNow().UtcDateTime);

        Assert.Equal(expectedDays, summary.CurrentAgingDays);
        Assert.Equal(expectedDays, summary.TotalStorageDays);
    }

    [Fact]
    public void Closed_cycle_uses_its_issue_timestamp_and_does_not_change_with_the_clock()
    {
        var storedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var issuedAt = storedAt.AddDays(12).AddHours(20);
        var cycle = Cycle("closed", storedAt, issuedAt);
        var clock = new FakeClock(new DateTimeOffset(issuedAt.AddDays(1), TimeSpan.Zero));

        var first = StorageAging.Summarize(true, [cycle], clock.GetUtcNow().UtcDateTime);
        clock.Advance(TimeSpan.FromDays(60));
        var later = StorageAging.Summarize(true, [cycle], clock.GetUtcNow().UtcDateTime);

        Assert.Equal(12, first.TotalStorageDays);
        Assert.Null(first.CurrentAgingDays);
        Assert.Equal(first.TotalStorageDays, later.TotalStorageDays);
        Assert.Null(later.CurrentAgingDays);
    }

    [Fact]
    public void Active_cycle_age_advances_when_the_next_whole_day_boundary_passes()
    {
        var storedAt = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var clock = new FakeClock(new DateTimeOffset(storedAt.AddHours(23).AddMinutes(59), TimeSpan.Zero));
        var cycle = Cycle("active", storedAt);

        Assert.Equal(0, StorageAging.Summarize(false, [cycle], clock.GetUtcNow().UtcDateTime).CurrentAgingDays);
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(1, StorageAging.Summarize(false, [cycle], clock.GetUtcNow().UtcDateTime).CurrentAgingDays);
    }

    [Fact]
    public void Cycle_fractions_are_floored_independently_before_totaling()
    {
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var closedCycles = new[]
        {
            Cycle("first", start, start.AddDays(12).AddHours(20)),
            Cycle("second", start, start.AddDays(25).AddHours(8))
        };

        Assert.Equal(37, StorageAging.Summarize(true, closedCycles, start).TotalStorageDays);

        var allCycles = closedCycles.Append(Cycle("active", start)).ToArray();
        var now = start.AddDays(3).AddHours(6);
        Assert.Equal(40, StorageAging.Summarize(false, allCycles, now).TotalStorageDays);
    }

    [Fact]
    public void Unspecified_materialized_timestamps_are_treated_as_utc()
    {
        var storedAt = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Unspecified);
        var issuedAt = new DateTime(2026, 1, 2, 10, 0, 0, DateTimeKind.Unspecified);
        var response = new StorageCycleResponse
        {
            StoredAtUtc = StorageAging.AsUtc(storedAt),
            IssuedAtUtc = StorageAging.AsUtc(issuedAt)
        };
        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal(DateTimeKind.Utc, StorageAging.AsUtc(storedAt).Kind);
        Assert.Equal(1, StorageAging.Summarize(true, [Cycle("closed", storedAt, issuedAt)], issuedAt).TotalStorageDays);
        Assert.Contains("\"storedAtUtc\":\"2026-01-01T10:00:00Z\"", json);
        Assert.Contains("\"issuedAtUtc\":\"2026-01-02T10:00:00Z\"", json);
    }

    [Fact]
    public void Future_active_start_is_reported_as_an_integrity_error()
    {
        var storedAt = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        var now = storedAt.AddTicks(-1);

        Assert.Throws<InvalidOperationException>(() =>
            StorageAging.Summarize(false, [Cycle("active", storedAt)], now));
    }

    [Fact]
    public void Enriched_issued_item_has_no_current_age_but_keeps_historical_total()
    {
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var cycles = new[]
        {
            Cycle("first", start, start.AddDays(12).AddHours(20)),
            Cycle("second", start, start.AddDays(25).AddHours(8))
        };
        var item = new InventoryItemResponse { Id = "item", IsIssued = true };
        var now = start.AddDays(40);

        var enriched = Assert.Single(InventoryController.WithAging([item], cycles, now));

        Assert.Null(enriched.CurrentStorageCycleId);
        Assert.Null(enriched.CurrentAgingDays);
        Assert.Equal(37, enriched.TotalStorageDays);
    }

    [Fact]
    public void Enriched_in_storage_item_exposes_current_and_accumulated_days()
    {
        var storedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var cycles = new[]
        {
            Cycle("closed", storedAt, storedAt.AddDays(2).AddHours(20)),
            Cycle("active", storedAt.AddDays(10))
        };
        var item = new InventoryItemResponse { Id = "item", IsIssued = false };
        var now = StorageAging.AsUtc(cycles[1].StoredAtUtc).AddHours(25);

        var enriched = Assert.Single(InventoryController.WithAging([item], cycles, now));

        Assert.Equal("active", enriched.CurrentStorageCycleId);
        Assert.Equal(1, enriched.CurrentAgingDays);
        Assert.Equal(3, enriched.TotalStorageDays);
    }

    [Fact]
    public void Missing_active_cycle_does_not_allow_an_in_storage_item()
    {
        Assert.Throws<InvalidOperationException>(() => StorageAging.Summarize(false, [], DateTime.UtcNow));
    }

    [Fact]
    public void Closed_cycle_with_negative_duration_is_reported_as_an_integrity_error()
    {
        var start = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc);

        Assert.Throws<InvalidOperationException>(() =>
            StorageAging.Summarize(true, [Cycle("closed", start, start.AddTicks(-1))], start));
    }

    private static StorageCycle Cycle(string id, DateTime storedAtUtc, DateTime? issuedAtUtc = null) =>
        new()
        {
            Id = id,
            InventoryItemId = "item",
            StoredAtUtc = storedAtUtc,
            IssuedAtUtc = issuedAtUtc
        };
}
