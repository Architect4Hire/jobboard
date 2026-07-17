using JobBoard.Jobs.Core.Managers.Models.Domain;
using JobBoard.Jobs.Core.Managers.Models.ServiceModels;
using JobBoard.Shared.Persistence;

namespace JobBoard.Jobs.Core.Data;

/// <summary>
/// Data-only seam for the Jobs context. Extends <see cref="IRepository"/> so the data layer can run a
/// whole operation (domain writes + the outbox row) inside one transaction via
/// <see cref="IRepository.ExecuteInTransactionAsync{T}"/>. Detail reads/writes return the <see cref="Job"/>
/// entity; the list read projects to its summary service model in SQL. No outbox, cache, or rules here.
/// </summary>
public interface IJobRepository : IRepository
{
    /// <summary>Open jobs (optionally filtered to a category slug), projected to summaries in SQL.</summary>
    Task<IReadOnlyList<JobSummaryServiceModel>> ListAsync(string? categorySlug, CancellationToken cancellationToken = default);

    /// <summary>Loads one job with its categories and tags, untracked (read-only). Null if absent.</summary>
    Task<Job?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reconciles the job's categories/tags against existing rows by slug (reusing existing, creating the
    /// rest) and stages the job for insert. Multiple writes — callers run it inside a transaction.
    /// </summary>
    Task<Job> AddAsync(Job job, CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes the job in a single conditional statement — <c>UPDATE ... WHERE Id = id AND Status = Open</c> —
    /// and reports whether a row changed. Returning <c>false</c> means the job was absent or already not open
    /// (including a concurrent close that won the race), so the caller must not publish a second event. This
    /// is the authoritative open→closed guard; the read-side status check is only a fast path.
    /// </summary>
    Task<bool> CloseIfOpenAsync(Guid id, CancellationToken cancellationToken = default);
}
