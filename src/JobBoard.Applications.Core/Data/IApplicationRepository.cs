using JobBoard.Applications.Core.Managers.Models.Domain;
using JobBoard.Applications.Core.Managers.Models.ServiceModels;
using JobBoard.Shared.Persistence;

namespace JobBoard.Applications.Core.Data;

/// <summary>
/// Data-only seam for the Applications context. Extends <see cref="IRepository"/> so the data layer can
/// run a whole operation (domain writes + the outbox row, and — on the consumer path — the inbox row)
/// inside one transaction via <see cref="IRepository.ExecuteInTransactionAsync{T}"/>. Detail reads return
/// the <see cref="Application"/> entity; the list read projects to its summary service model in SQL. No
/// outbox, inbox, cache, or rules here.
/// </summary>
public interface IApplicationRepository : IRepository
{
    /// <summary>A candidate's applications, newest first, projected to summaries in SQL.</summary>
    Task<IReadOnlyList<ApplicationSummaryServiceModel>> ListByCandidateAsync(Guid candidateId, CancellationToken cancellationToken = default);

    /// <summary>Loads one application, untracked (read-only). Null if absent.</summary>
    Task<Application?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// An untracked snapshot of the <i>active</i> (Submitted/Reviewed/Offered) applications for a job —
    /// the consumer reads these to build one status-changed event per application before closing them.
    /// Untracked on purpose: the snapshot must reflect pre-close state even if the surrounding transaction
    /// replays.
    /// </summary>
    Task<IReadOnlyList<Application>> GetActiveByJobAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>Stages a new application for insert. The caller runs it inside a transaction with the outbox write.</summary>
    Task<Application> AddAsync(Application application, CancellationToken cancellationToken = default);

    /// <summary>
    /// Withdraws in a single conditional statement — <c>UPDATE ... WHERE Id = id AND Status IN (active)</c> —
    /// and reports whether a row changed. <c>false</c> means the application was absent or already terminal
    /// (including a concurrent transition that won the race), so the caller must not publish an event.
    /// </summary>
    Task<bool> WithdrawIfActiveAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Advances in a single conditional statement — <c>UPDATE ... WHERE Id = id AND Status = expected</c> —
    /// and reports whether a row changed. <c>false</c> means the application moved on before this ran, so no
    /// event is published. This is the authoritative transition guard; the read-side check is only a fast path.
    /// </summary>
    Task<bool> AdvanceIfInStatusAsync(Guid id, ApplicationStatus expected, ApplicationStatus target, CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes every <i>active</i> application for a job in one conditional statement —
    /// <c>UPDATE ... WHERE JobId = jobId AND Status IN (active)</c> — moving them to <paramref name="target"/>
    /// and returning how many rows changed. Idempotent by construction: a replay finds no active rows left.
    /// </summary>
    Task<int> CloseActiveByJobAsync(Guid jobId, ApplicationStatus target, CancellationToken cancellationToken = default);

    /// <summary>
    /// Of the given ids, those whose application is currently in <paramref name="status"/>. Read right after
    /// the bulk close so the caller can publish a status-changed event only for rows this operation actually
    /// transitioned — a row moved concurrently (e.g. withdrawn) between the snapshot and the close is excluded,
    /// keeping the emitted event a true fact.
    /// </summary>
    Task<IReadOnlySet<Guid>> GetIdsInStatusAsync(IReadOnlyCollection<Guid> ids, ApplicationStatus status, CancellationToken cancellationToken = default);
}
