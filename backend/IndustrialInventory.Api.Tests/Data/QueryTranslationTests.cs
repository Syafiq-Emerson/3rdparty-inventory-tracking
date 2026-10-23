using IndustrialInventory.Api.Controllers;
using IndustrialInventory.Api.Data;
using IndustrialInventory.Api.Services.Inventory;
using IndustrialInventory.Api.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace IndustrialInventory.Api.Tests.Data;

/// <summary>
/// Runs the controllers' LINQ through the real SQL Server translator without a connection, so a query
/// that would only fail at runtime ("could not be translated") fails here instead.
/// </summary>
public class QueryTranslationTests
{
    private const string Schema = $"[{InventoryDbContext.Schema}]";

    [Fact]
    public void Inventory_page_translates_with_offset_paging()
    {
        using var db = OfflineDb.Create();
        var sql = InventoryController.PageQuery(db, "loc-1", 2, 50).ToQueryString();

        Assert.Contains($"{Schema}.[InventoryItem]", sql);
        Assert.Contains("OFFSET", sql);
        Assert.Contains("[LocationId] = @", sql);
    }

    [Fact]
    public void Search_translates_to_escaped_like()
    {
        using var db = OfflineDb.Create();
        var sql = InventoryController.SearchQuery(db, InventoryRules.ContainsPattern("PV_1"), null).ToQueryString();

        Assert.Contains("LIKE", sql);
        Assert.Contains("ESCAPE N'\\'", sql);
        Assert.DoesNotContain("PV_1", sql);
    }

    [Fact]
    public void History_translates_with_optional_locations()
    {
        using var db = OfflineDb.Create();
        var sql = InventoryController.HistoryQuery(db, "item-1").ToQueryString();

        Assert.Contains($"{Schema}.[InventoryMovement]", sql);
        Assert.Contains("LEFT JOIN", sql);
    }

    [Fact]
    public void Identification_projection_translates()
    {
        using var db = OfflineDb.Create();
        var sql = db.InventoryItems
            .Where(i => i.SerialNumber == "s" && i.TagNumber == "t")
            .Select(InventoryController.IdentificationProjection)
            .ToQueryString();

        Assert.Contains($"{Schema}.[StoreLocation]", sql);
    }

    [Fact]
    public void Dashboard_queries_translate()
    {
        using var db = OfflineDb.Create();

        Assert.Contains("COUNT", DashboardController.ItemCountsQuery(db).ToQueryString());
        Assert.Contains("COUNT", DashboardController.LocationCountsQuery(db).ToQueryString());
        Assert.Contains("TOP", DashboardController.RecentItemsQuery(db).ToQueryString());
    }

    [Fact]
    public void Master_data_projections_translate_with_item_counts()
    {
        using var db = OfflineDb.Create();

        Assert.Contains("COUNT", db.StoreLocations.Select(LocationsController.Projection).ToQueryString());
        Assert.Contains("COUNT", db.Personnel.Select(PersonnelController.Projection).ToQueryString());
    }
}
