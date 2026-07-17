using JobBoard.Contracts;
using JobBoard.Jobs.Core.Managers.Models.Domain;
using JobBoard.Jobs.Core.Managers.Models.ServiceModels;

namespace JobBoard.Jobs.Core.Data;

/// <summary>
/// Composes repository calls into whole operations and owns the transaction boundary — including the
/// atomic outbox write on close. Reads pass straight through; writes that touch more than one row, or
/// emit an event, run inside a single transaction. Depends only on <see cref="IJobRepository"/> and
/// <c>IOutbox</c>; holds no <c>DbContext</c>.
/// </summary>
public interface IJobDataLayer
{
    Task<IReadOnlyList<JobSummaryServiceModel>> ListAsync(string? categorySlug, CancellationToken cancellationToken = default);

    Task<Job?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Job> AddAsync(Job job, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a close atomically: the conditional status UPDATE and the <paramref name="event"/> outbox
    /// row commit together, or neither does. Returns <c>false</c> — enqueuing nothing — when the job was
    /// not open (e.g. a concurrent close already won), so no duplicate event is published.
    /// </summary>
    Task<bool> CloseAsync(Guid id, JobClosed @event, CancellationToken cancellationToken = default);
}
