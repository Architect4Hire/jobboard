using System.Collections.Concurrent;
using JobBoard.Shared.Caching;

namespace JobBoard.Jobs.Tests.Fakes;

/// <summary>
/// In-memory <see cref="ICache"/> for facade tests: stores boxed values and counts each operation, so a
/// test can prove a read-through hit skips business, a miss populates the cache, and a write invalidates.
/// Values are held as-is (no serialization) — enough to exercise the facade's key/generation logic.
/// </summary>
public sealed class FakeCache : ICache
{
    private readonly ConcurrentDictionary<string, object?> _store = new();

    public int SetCount { get; private set; }

    public int RemoveCount { get; private set; }

    public IReadOnlyCollection<string> Keys => _store.Keys.ToArray();

    public bool Contains(string key) => _store.ContainsKey(key);

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.TryGetValue(key, out var value) ? (T?)value : default);

    public Task SetAsync<T>(string key, T value, TimeSpan? timeToLive = null, CancellationToken cancellationToken = default)
    {
        SetCount++;
        _store[key] = value;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        RemoveCount++;
        _store.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}
