namespace JobBoard.Shared.Caching;

/// <summary>
/// The cache abstraction a service's facade uses to read-through and invalidate its ServiceModels. Only
/// the contract lives here; the Redis-backed implementation arrives with the Aspire cache wiring. Keeping
/// facades on this interface means they never bind to a concrete cache client.
/// </summary>
public interface ICache
{
    /// <summary>Returns the cached value for <paramref name="key"/>, or <c>default</c> on a miss.</summary>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>Stores <paramref name="value"/> under <paramref name="key"/>, with an optional TTL.</summary>
    Task SetAsync<T>(string key, T value, TimeSpan? timeToLive = null, CancellationToken cancellationToken = default);

    /// <summary>Removes <paramref name="key"/> (e.g. to invalidate after a write).</summary>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
}
