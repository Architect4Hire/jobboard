# Read-Through Caching

*A fail-open cache in front of one read endpoint, invalidated by dropping a single generation token
instead of enumerating every cached variant.*

## The problem this solves

The Jobs list is read far more often than it changes, and it's filterable (`?category=`), so there's a
whole *family* of cache entries — one per filter value plus the unfiltered "all". `ICache` only offers
`RemoveAsync(key)`, so invalidating "every list variant" after a post or close would mean either
tracking every key that's ever been minted, or accepting staleness. Generation-token invalidation solves
that with one extra key: every real entry is namespaced under a generation id, and a write invalidates
the *whole family at once* by dropping that one token — every entry it namespaced is thereby orphaned,
with no need to know their individual keys.

## How it works here

The whole pattern lives in [`JobFacade.cs`](../../../src/JobBoard.Jobs.Core/Facade/JobFacade.cs) — the
facade is the only layer allowed to touch `ICache` (see
[Layered Service Architecture](./layered-service-architecture.md)); business and the data layer never
know a cache exists.

**Minting/reading the generation** — `BuildListKeyAsync` resolves the current generation, minting one
on first use, and namespaces the requested variant under it:

```csharp
private async Task<string> BuildListKeyAsync(string? categorySlug, CancellationToken cancellationToken)
{
    var generation = await _cache.GetAsync<string>(GenerationKey, cancellationToken);
    if (generation is null)
    {
        generation = Guid.NewGuid().ToString("N");
        await _cache.SetAsync(GenerationKey, generation, cancellationToken: cancellationToken);
    }
    return $"jobs:list:{generation}:{categorySlug ?? "all"}";
}
```

**Read-through, fail-open** — `ListAsync` tries the cache, falls through to the business layer on a
miss *or a cache failure*, and writes back best-effort:

```csharp
public async Task<IReadOnlyList<JobSummaryServiceModel>> ListAsync(string? categorySlug, CancellationToken cancellationToken = default)
{
    string? key = null;
    try
    {
        key = await BuildListKeyAsync(categorySlug, cancellationToken);
        var cached = await _cache.GetAsync<IReadOnlyList<JobSummaryServiceModel>>(key, cancellationToken);
        if (cached is not null) return cached;
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        _logger.LogWarning(ex, "Job-list cache read failed; serving from source.");
        key = null;   // signals "couldn't use the cache" — skip the write-back below too
    }

    var jobs = await _business.ListAsync(categorySlug, cancellationToken);

    if (key is not null)
    {
        try { await _cache.SetAsync(key, jobs, ListCacheTtl, cancellationToken); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Job-list cache write-back failed for {CacheKey}.", key);
        }
    }
    return jobs;
}
```

Every cache operation is wrapped separately so a Redis blip degrades to "serve from the database" rather
than failing the request — the repository query underneath stays authoritative regardless of whether
the cache is healthy.

**Invalidation on write** — `PostAsync` and `CloseAsync` both call `InvalidateListAsync` after the
domain write (and its outbox row — see [Transactional Outbox & Inbox](./transactional-outbox-and-inbox.md))
has already committed:

```csharp
private async Task InvalidateListAsync(CancellationToken cancellationToken)
{
    try { await _cache.RemoveAsync(GenerationKey, cancellationToken); }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        _logger.LogWarning(ex, "Job-list cache invalidation failed; entries will lapse via TTL.");
    }
}
```

Dropping `GenerationKey` doesn't touch `jobs:list:{gen}:all` or `jobs:list:{gen}:backend` directly — it
just means the *next* read mints a fresh generation and none of the old entries are ever looked up
again under it. The 5-minute `ListCacheTtl` is a backstop that reaps those now-orphaned entries out of
Redis; explicit invalidation is the primary freshness mechanism, the TTL only bounds how long a stale
orphan lingers. Because a cache failure here must never surface as a failed write, invalidation failure
is logged and swallowed — the entries still lapse via TTL either way.

## Why

[ADR-0009](../../adr/0009-read-through-cache-generation-invalidation.md) is the decision to cache at
all, why it's fail-open, and why generation-token invalidation over key enumeration.

## Pitfalls / rules to respect

- **Only the facade touches the cache.** Business and the data layer never see `ICache` — caching is a
  read-path optimization on ServiceModels, not a business decision.
- **Fail-open, always.** Every cache call (read, write-back, invalidation) is independently try/caught;
  a cache outage degrades service, it never fails a request.
- **The source of truth is still the repository.** The cache never filters or reshapes what the
  repository already returned — it only remembers the answer for next time.
- **A new filterable list needs the same generation-token treatment** if it's cached at all — an
  ad-hoc per-key invalidation scheme will miss a variant the next filter option introduces.

## Reference map

| Concern | Real file |
| --- | --- |
| Cache abstraction | [`ICache.cs`](../../../src/JobBoard.Shared/Caching/ICache.cs) |
| Redis implementation | [`RedisCache.cs`](../../../src/JobBoard.Shared/Caching/RedisCache.cs) |
| The full read-through + invalidation pattern | [`JobFacade.cs`](../../../src/JobBoard.Jobs.Core/Facade/JobFacade.cs) |
| Cache resource wiring | [`AppHost.cs`](../../../src/JobBoard.AppHost/AppHost.cs) (`AddRedis("cache")`) |
