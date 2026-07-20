using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace JobBoard.Shared.Caching;

/// <summary>
/// The <see cref="ICache"/> implementation a facade resolves. It stores JSON-serialized ServiceModels in
/// whatever <see cref="IDistributedCache"/> the host wired — in this stack that is Redis, via the Aspire
/// <c>AddRedisDistributedCache</c> client integration keyed to the AppHost <c>cache</c> resource. Keeping
/// the adapter on <see cref="IDistributedCache"/> means <see cref="JobBoard.Shared"/> takes no Redis SDK
/// dependency; the concrete store is a host wiring decision.
/// </summary>
public sealed class RedisCache : ICache
{
    // System.Text.Json defaults are fine: ServiceModels are plain records with public members. A single
    // shared options instance avoids re-allocating the serializer metadata cache per call.
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IDistributedCache _cache;

    public RedisCache(IDistributedCache cache) => _cache = cache;

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var bytes = await _cache.GetAsync(key, cancellationToken);
        return bytes is null ? default : JsonSerializer.Deserialize<T>(bytes, SerializerOptions);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? timeToLive = null, CancellationToken cancellationToken = default)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, SerializerOptions);
        var options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = timeToLive };
        await _cache.SetAsync(key, bytes, options, cancellationToken);
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
        _cache.RemoveAsync(key, cancellationToken);
}
