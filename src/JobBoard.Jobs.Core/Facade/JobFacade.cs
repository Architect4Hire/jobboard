using FluentValidation;
using JobBoard.Jobs.Core.Business;
using JobBoard.Jobs.Core.Managers.Models.ServiceModels;
using JobBoard.Jobs.Core.Managers.Models.ViewModels;
using JobBoard.Shared.Caching;
using Microsoft.Extensions.Logging;

namespace JobBoard.Jobs.Core.Facade;

/// <inheritdoc cref="IJobFacade"/>
/// <remarks>
/// The facade owns the two seams the layers below must not: validation of inbound view models, and
/// read-through caching of the outbound job-list ServiceModels (business/data never touch the cache).
/// <para>
/// The list is filterable by category, so there is a family of cache entries (one per <c>?category=</c>
/// value plus the unfiltered "all"). To invalidate the whole family with the single <see cref="ICache"/>
/// primitive available (<see cref="ICache.RemoveAsync"/>), entries are namespaced by a <em>generation</em>
/// token held under <see cref="GenerationKey"/>: a write drops that one key, which orphans every variant
/// at once (they lapse via <see cref="ListCacheTtl"/>) and the next read mints a fresh generation. The
/// underlying repository query stays authoritative — the facade never filters ServiceModels itself.
/// </para>
/// </remarks>
public sealed class JobFacade : IJobFacade
{
    // The generation token namespaces every list entry; dropping it invalidates the whole family at once.
    private const string GenerationKey = "jobs:list:gen";

    // A backstop TTL that reaps entries orphaned by a generation bump; explicit invalidation is the primary
    // freshness mechanism, so this only bounds how long a stale orphan lingers in Redis.
    private static readonly TimeSpan ListCacheTtl = TimeSpan.FromMinutes(5);

    private readonly IJobBusiness _business;
    private readonly IValidator<PostJobViewModel> _postValidator;
    private readonly ICache _cache;
    private readonly ILogger<JobFacade> _logger;

    public JobFacade(
        IJobBusiness business,
        IValidator<PostJobViewModel> postValidator,
        ICache cache,
        ILogger<JobFacade> logger)
    {
        _business = business;
        _postValidator = postValidator;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IReadOnlyList<JobSummaryServiceModel>> ListAsync(string? categorySlug, CancellationToken cancellationToken = default)
    {
        // The cache is fail-open: it's an optimization, not a source of truth (the generation-bump plus
        // TTL keep it eventually consistent), so a Redis blip must degrade to serving from the source
        // rather than failing the read. A null key signals "couldn't use the cache — skip the write-back".
        string? key = null;
        try
        {
            key = await BuildListKeyAsync(categorySlug, cancellationToken);
            var cached = await _cache.GetAsync<IReadOnlyList<JobSummaryServiceModel>>(key, cancellationToken);
            if (cached is not null)
            {
                _logger.LogDebug("Job-list cache hit for {CacheKey}", key);
                return cached;
            }

            _logger.LogDebug("Job-list cache miss for {CacheKey}", key);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Job-list cache read failed; serving from source.");
            key = null;
        }

        var jobs = await _business.ListAsync(categorySlug, cancellationToken);

        if (key is not null)
        {
            try
            {
                await _cache.SetAsync(key, jobs, ListCacheTtl, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Job-list cache write-back failed for {CacheKey}.", key);
            }
        }

        return jobs;
    }

    public Task<JobDetailServiceModel?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        _business.GetAsync(id, cancellationToken);

    public async Task<JobDetailServiceModel> PostAsync(PostJobViewModel viewModel, CancellationToken cancellationToken = default)
    {
        // The global exception handler maps the thrown ValidationException to a 400 with field detail.
        await _postValidator.ValidateAndThrowAsync(viewModel, cancellationToken);
        var posted = await _business.PostAsync(viewModel, cancellationToken);
        // A new posting joins the list (and possibly a category variant) — refresh the whole family.
        await InvalidateListAsync(cancellationToken);
        return posted;
    }

    public async Task<JobDetailServiceModel> CloseAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // No inbound view model to validate; the "must be open" check is a domain rule in business.
        var closed = await _business.CloseAsync(id, cancellationToken);
        // A close changes the posting's status, so any cached list that showed it is now stale.
        await InvalidateListAsync(cancellationToken);
        return closed;
    }

    // Resolves the current generation (minting one on first use) and namespaces the variant key under it.
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

    // Drops the generation token; every list entry namespaced under it is thereby orphaned and lapses.
    // Best-effort: the domain write (and its outbox row) has already committed by the time this runs, so a
    // cache failure here must not surface as a failed post/close — the entries lapse via TTL regardless.
    private async Task InvalidateListAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _cache.RemoveAsync(GenerationKey, cancellationToken);
            _logger.LogDebug("Job-list cache invalidated");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Job-list cache invalidation failed; entries will lapse via TTL.");
        }
    }
}
