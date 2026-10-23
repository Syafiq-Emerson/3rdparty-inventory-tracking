using IndustrialInventory.Api.Services.Deploy;

namespace IndustrialInventory.Api.Tests.Deploy;

public class DeployNoticeTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Counts_down_to_the_start()
    {
        var notice = DeployNoticeService.Evaluate("""{"startsAt":"2026-10-22T08:00:30Z","message":"Update soon"}""", Now);
        Assert.Equal(30, notice!.SecondsRemaining);
        Assert.Equal("Update soon", notice.Message);
    }

    [Fact]
    public void Partial_seconds_round_up()
        => Assert.Equal(30, DeployNoticeService.Evaluate("""{"startsAt":"2026-10-22T08:00:29.200Z"}""", Now)!.SecondsRemaining);

    [Fact]
    public void Start_time_with_an_offset_is_compared_in_utc()
        => Assert.Equal(10, DeployNoticeService.Evaluate("""{"startsAt":"2026-10-22T16:00:10+08:00"}""", Now)!.SecondsRemaining);

    [Fact]
    public void Started_update_reports_zero_until_stale()
    {
        Assert.Equal(0, DeployNoticeService.Evaluate("""{"startsAt":"2026-10-22T07:55:00Z"}""", Now)!.SecondsRemaining);
        Assert.Null(DeployNoticeService.Evaluate("""{"startsAt":"2026-10-22T07:44:00Z"}""", Now));
    }

    [Fact]
    public void Missing_message_uses_the_default_and_long_ones_are_cut()
    {
        Assert.Equal(DeployNoticeService.DefaultMessage,
            DeployNoticeService.Evaluate("""{"startsAt":"2026-10-22T08:00:10Z","message":"   "}""", Now)!.Message);

        var longText = new string('x', 500);
        var notice = DeployNoticeService.Evaluate($$"""{"startsAt":"2026-10-22T08:00:10Z","message":"{{longText}}"}""", Now);
        Assert.Equal(DeployNoticeService.MaxMessageLength, notice!.Message.Length);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{"message":"no start"}""")]
    [InlineData("""{"startsAt":"tomorrow"}""")]
    [InlineData("""{"startsAt":12345}""")]
    public void Bad_files_are_ignored(string json) => Assert.Null(DeployNoticeService.Evaluate(json, Now));
}
