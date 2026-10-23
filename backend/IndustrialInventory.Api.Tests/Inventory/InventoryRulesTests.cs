using IndustrialInventory.Api.DTOs;
using IndustrialInventory.Api.Services.Inventory;

namespace IndustrialInventory.Api.Tests.Inventory;

public class InventoryRulesTests
{
    [Theory]
    [InlineData(0, 0, 1, 1)]
    [InlineData(-5, 50, 1, 50)]
    [InlineData(3, 1000, 3, 200)]
    public void Page_and_page_size_are_clamped(int page, int size, int expectedPage, int expectedSize)
        => Assert.Equal((expectedPage, expectedSize), InventoryRules.ClampPaging(page, size));

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(" abc ", "abc")]
    public void Idempotency_key_is_trimmed_and_blank_means_none(string? raw, string? expected)
        => Assert.Equal(expected, InventoryRules.NormalizeIdempotencyKey(raw));

    [Fact]
    public void Idempotency_key_is_cut_to_the_column_length()
        => Assert.Equal(new string('k', 64), InventoryRules.NormalizeIdempotencyKey(new string('k', 80)));

    [Theory]
    [InlineData("PV-165", "%PV-165%")]
    [InlineData(" 50% ", @"%50\%%")]
    [InlineData("A_B", @"%A\_B%")]
    [InlineData("[x]", @"%\[x]%")]
    [InlineData(@"C:\x", @"%C:\\x%")]
    public void Search_text_is_escaped_for_like(string text, string expected)
        => Assert.Equal(expected, InventoryRules.ContainsPattern(text));

    [Fact]
    public void Duplicate_names_the_serial_when_the_serial_clashes()
    {
        var existing = Item("SER-1", "TAG-1");
        var conflict = InventoryRules.DuplicateConflict(existing, "SER-1", "TAG-9");

        Assert.Equal("DUPLICATE_IDENTIFIER", conflict.ErrorCode);
        Assert.Equal("SerialNumber", conflict.ConflictingField);
        Assert.Equal("SER-1", conflict.ConflictingValue);
        Assert.Contains("Serial Number 'SER-1'", conflict.Message);
        Assert.Same(existing, conflict.ExistingItem);
    }

    [Fact]
    public void Duplicate_names_the_tag_when_only_the_tag_clashes()
    {
        var conflict = InventoryRules.DuplicateConflict(Item("SER-1", "TAG-1"), "SER-9", "TAG-1");

        Assert.Equal("TagNumber", conflict.ConflictingField);
        Assert.Equal("TAG-1", conflict.ConflictingValue);
        Assert.Contains("Tag Number 'TAG-1'", conflict.Message);
    }

    private static InventoryItemResponse Item(string serial, string tag) =>
        new() { Id = "item-1", SerialNumber = serial, TagNumber = tag };
}
