using IndustrialInventory.Api.Services.Shared;
using IndustrialInventory.Api.Tests.Support;

namespace IndustrialInventory.Api.Tests.Shared;

public class IdempotencyGateTests
{
    private readonly FakeClock clock = new(new DateTimeOffset(2026, 10, 23, 8, 0, 0, TimeSpan.Zero));

    [Fact]
    public void First_use_of_a_key_proceeds_and_a_repeat_is_refused()
    {
        var gate = new IdempotencyGate(clock);

        Assert.True(gate.TryBegin("key-1"));
        Assert.False(gate.TryBegin("key-1"));
        Assert.True(gate.TryBegin("key-2"));
    }

    [Fact]
    public void Released_key_can_be_retried()
    {
        var gate = new IdempotencyGate(clock);
        gate.TryBegin("key-1");

        gate.Release("key-1");

        Assert.True(gate.TryBegin("key-1"));
    }

    [Fact]
    public void Key_is_forgotten_after_the_retention_window()
    {
        var gate = new IdempotencyGate(clock);
        gate.TryBegin("key-1");

        clock.Advance(TimeSpan.FromHours(5));
        Assert.False(gate.TryBegin("key-1"));

        clock.Advance(TimeSpan.FromHours(2));
        Assert.True(gate.TryBegin("key-1"));
    }

    [Fact]
    public async Task Only_one_of_many_concurrent_callers_wins()
    {
        var gate = new IdempotencyGate(clock);

        var results = await Task.WhenAll(Enumerable.Range(0, 50).Select(_ => Task.Run(() => gate.TryBegin("same"))));

        Assert.Equal(1, results.Count(r => r));
    }
}
