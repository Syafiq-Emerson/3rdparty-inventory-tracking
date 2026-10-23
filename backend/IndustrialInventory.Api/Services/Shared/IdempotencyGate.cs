using System.Collections.Concurrent;

namespace IndustrialInventory.Api.Services.Shared;

/// <summary>
/// Stops a retried post from being applied twice. In-memory, so it is valid for a single
/// IIS worker process; the Idempotency-Key is also stored on the ledger row as the durable record.
/// </summary>
public sealed class IdempotencyGate(TimeProvider timeProvider)
{
    private static readonly TimeSpan Retention = TimeSpan.FromHours(6);

    private readonly ConcurrentDictionary<string, DateTimeOffset> _seen = new();

    /// <summary>True when this key has not been used before, meaning the caller may proceed.</summary>
    public bool TryBegin(string key)
    {
        Prune();
        return _seen.TryAdd(key, timeProvider.GetUtcNow());
    }

    /// <summary>Releases a key whose operation failed, so an honest retry is not blocked.</summary>
    public void Release(string key) => _seen.TryRemove(key, out _);

    private void Prune()
    {
        var cutoff = timeProvider.GetUtcNow() - Retention;
        foreach (var (key, seenAt) in _seen)
        {
            if (seenAt < cutoff) _seen.TryRemove(key, out _);
        }
    }
}
