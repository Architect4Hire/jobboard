using JobBoard.Jobs.Core.Managers.Models.Domain;
using JobBoard.Jobs.Core.Managers.Models.ServiceModels;
using JobBoard.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace JobBoard.Jobs.Core.Data;

/// <summary>
/// EF Core implementation of <see cref="IJobRepository"/> over <see cref="JobsDbContext"/>.
/// Inherits <c>ExecuteInTransactionAsync</c> from <see cref="BaseRepository{TContext}"/>.
/// </summary>
public sealed class JobRepository : BaseRepository<JobsDbContext>, IJobRepository
{
    public JobRepository(JobsDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<JobSummaryServiceModel>> ListAsync(
        string? categorySlug,
        CancellationToken cancellationToken = default)
    {
        var query = Context.Jobs.AsNoTracking().Where(j => j.Status == JobStatus.Open);

        if (!string.IsNullOrWhiteSpace(categorySlug))
        {
            query = query.Where(j => j.Categories.Any(c => c.Slug == categorySlug));
        }

        return await query
            .OrderByDescending(j => j.CreatedOnUtc)
            .Select(j => new JobSummaryServiceModel(
                j.Id,
                j.Title,
                j.Location,
                new SalaryBandServiceModel(j.Salary.Min, j.Salary.Max, j.Salary.Currency),
                j.Status,
                j.Categories.Select(c => c.Slug).ToList(),
                j.CreatedOnUtc))
            .ToListAsync(cancellationToken);
    }

    public Task<Job?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        Context.Jobs
            .AsNoTracking()
            .Include(j => j.Categories)
            .Include(j => j.Tags)
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

    public async Task<bool> CloseIfOpenAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Conditional UPDATE: only an Open row flips. Two concurrent closes both pass the read-side check,
        // but only the first UPDATE matches Status = Open; the second affects zero rows and is rejected.
        var affected = await Context.Jobs
            .Where(j => j.Id == id && j.Status == JobStatus.Open)
            .ExecuteUpdateAsync(setters => setters.SetProperty(j => j.Status, JobStatus.Closed), cancellationToken);

        return affected > 0;
    }

    public async Task<Job> AddAsync(Job job, CancellationToken cancellationToken = default)
    {
        job.Categories = await ReconcileAsync(job.Categories, Context.Categories, cancellationToken);
        job.Tags = await ReconcileAsync(job.Tags, Context.Tags, cancellationToken);

        await Context.Jobs.AddAsync(job, cancellationToken);
        return job;
    }

    /// <summary>
    /// True when <paramref name="exception"/> is the unique-index violation on a category/tag
    /// <c>Slug</c> — the narrow race where two concurrent posts insert the <i>same brand-new</i> slug in
    /// the window between the reconcile's <c>SELECT</c> and its <c>INSERT</c>. The classifier lives here
    /// (the repository owns provider knowledge), but the exception surfaces from <c>SaveChanges</c> inside
    /// <see cref="BaseRepository{TContext}.ExecuteInTransactionAsync{T}"/>, so the data layer — which owns
    /// the transaction — is where it's caught and mapped to a retryable conflict.
    /// </summary>
    public static bool IsDuplicateSlugViolation(DbUpdateException exception) =>
        exception.InnerException switch
        {
            // Npgsql (production): a unique_violation (23505) whose failing index is a Slug index.
            PostgresException pg => pg.SqlState == PostgresErrorCodes.UniqueViolation
                && (pg.ConstraintName?.Contains("Slug", StringComparison.OrdinalIgnoreCase) ?? false),
            // Other providers (e.g. SQLite in tests) name the offending Slug column in the failure text.
            { } inner => inner.Message.Contains("Slug", StringComparison.OrdinalIgnoreCase)
                && inner.Message.Contains("unique", StringComparison.OrdinalIgnoreCase),
            _ => false,
        };

    /// <summary>
    /// Returns one entity per requested classification, reusing the tracked existing row when a slug
    /// already exists and keeping the new one otherwise — so a post never duplicates a category/tag and
    /// never trips the unique slug index. Also de-duplicates repeated slugs within a single request.
    /// </summary>
    private static async Task<ICollection<T>> ReconcileAsync<T>(
        ICollection<T> requested,
        DbSet<T> set,
        CancellationToken cancellationToken)
        where T : class, IClassification
    {
        if (requested.Count == 0)
        {
            return requested;
        }

        var slugs = requested.Select(x => x.Slug).ToList();
        var bySlug = (await set.Where(x => slugs.Contains(x.Slug)).ToListAsync(cancellationToken))
            .ToDictionary(x => x.Slug);

        var reconciled = new List<T>();
        foreach (var item in requested)
        {
            if (bySlug.TryGetValue(item.Slug, out var existing))
            {
                reconciled.Add(existing);
            }
            else
            {
                reconciled.Add(item);
                bySlug[item.Slug] = item; // a second entry with the same slug in this request reuses it
            }
        }

        return reconciled;
    }
}
