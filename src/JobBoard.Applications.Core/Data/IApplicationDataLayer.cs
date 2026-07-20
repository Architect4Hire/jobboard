using JobBoard.Applications.Core.Managers.Models.Domain;
using JobBoard.Applications.Core.Managers.Models.ServiceModels;
using JobBoard.Contracts;

namespace JobBoard.Applications.Core.Data;

/// <summary>
/// Composes repository calls into whole operations and owns the transaction boundary — including the
/// atomic outbox write on every state change, and the inbox write on the consumer path. Reads pass
/// straight through; writes that emit an event run inside a single transaction. Depends on
/// <see cref="IApplicationRepository"/>, <c>IOutbox</c>, and <c>IInbox</c>; holds no <c>DbContext</c>.
/// </summary>
public interface IApplicationDataLayer
{
    Task<IReadOnlyList<ApplicationSummaryServiceModel>> ListByCandidateAsync(Guid candidateId, CancellationToken cancellationToken = default);

    Task<Application?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a new application and its <paramref name="event"/> outbox row in one transaction. Maps the
    /// unique-index race on <c>(CandidateId, JobId)</c> to a retryable 409 <c>DomainException</c>.
    /// </summary>
    Task<Application> SubmitAsync(Application application, ApplicationSubmitted @event, CancellationToken cancellationToken = default);

    /// <summary>
    /// Withdraws atomically: the conditional status UPDATE and the <paramref name="event"/> outbox row commit
    /// together, or neither does. Returns <c>false</c> — enqueuing nothing — when the application was not
    /// active (e.g. a concurrent transition already won), so no duplicate event is published.
    /// </summary>
    Task<bool> WithdrawAsync(Guid id, ApplicationStatusChanged @event, CancellationToken cancellationToken = default);

    /// <summary>
    /// Advances atomically from <paramref name="expected"/> to <paramref name="target"/>: the conditional
    /// UPDATE and the <paramref name="event"/> outbox row commit together, or neither does. Returns
    /// <c>false</c> — enqueuing nothing — when the application had already left <paramref name="expected"/>.
    /// </summary>
    Task<bool> AdvanceAsync(Guid id, ApplicationStatus expected, ApplicationStatus target, ApplicationStatusChanged @event, CancellationToken cancellationToken = default);

    /// <summary>
    /// The <c>JobClosed</c> consumer's atomic unit. In one transaction: no-ops if <paramref name="messageId"/>
    /// is already in the inbox (idempotency); otherwise snapshots the active applications for the job, closes
    /// them to <paramref name="target"/>, enqueues one event per closed application via
    /// <paramref name="buildEvent"/>, and records <paramref name="messageId"/> in the inbox. Returns the
    /// number of applications closed. A redelivery finds the inbox row and does nothing.
    /// </summary>
    Task<int> CloseOpenApplicationsForJobAsync(
        Guid jobId,
        Guid messageId,
        ApplicationStatus target,
        Func<Application, ApplicationStatusChanged> buildEvent,
        CancellationToken cancellationToken = default);
}
