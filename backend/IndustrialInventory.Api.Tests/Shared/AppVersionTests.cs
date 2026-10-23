using IndustrialInventory.Api.Services.Shared;

namespace IndustrialInventory.Api.Tests.Shared;

public class AppVersionTests
{
    [Theory]
    [InlineData("2.1.0+abc123", "2.1.0")]
    [InlineData(null, "0.0.0")]
    [InlineData(" ", "0.0.0")]
    public void Version_drops_the_commit_hash(string? informational, string expected)
        => Assert.Equal(expected, AppVersion.Clean(informational));

    [Fact]
    public void Build_date_is_read_as_utc()
        => Assert.Equal(new DateTime(2026, 10, 23, 1, 2, 3, DateTimeKind.Utc), AppVersion.ParseBuildDate("2026-10-23T01:02:03Z"));
}
