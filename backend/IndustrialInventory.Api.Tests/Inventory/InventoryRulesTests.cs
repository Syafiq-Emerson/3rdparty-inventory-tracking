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
    [InlineData(null, true, null)]
    [InlineData("", true, null)]
    [InlineData("   ", true, null)]
    [InlineData(" abc ", true, "abc")]
    [InlineData(" 64 ", true, "64")]
    public void Idempotency_key_is_trimmed_and_blank_means_none(string? raw, bool expectedValid, string? expected)
    {
        Assert.Equal(expectedValid, InventoryRules.TryNormalizeIdempotencyKey(raw, out var actual));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Oversize_idempotency_key_is_rejected_without_truncation()
    {
        Assert.False(InventoryRules.TryNormalizeIdempotencyKey(new string('k', 65), out var normalized));
        Assert.Null(normalized);
    }

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
