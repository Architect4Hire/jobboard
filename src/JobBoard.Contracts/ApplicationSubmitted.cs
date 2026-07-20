namespace JobBoard.Contracts;

/// <summary>
/// A candidate submitted an application to a job posting. A fact, published by Applications when a new
/// application is accepted into the <c>Submitted</c> state, carried across the bus so other services can
/// react (e.g. Notifications emailing the employer). Carries only what a consumer needs: the application
/// and the candidate/job it links, plus when it happened. No behavior, no EF, no reference to
/// Applications' Domain types.
/// </summary>
/// <param name="Id">The event's own identity — the outbox row key and Service Bus <c>MessageId</c>.
/// Stamped fresh by the business layer when it builds the event; consumers dedupe on it.</param>
/// <param name="ApplicationId">The application that was submitted (its identity in the Applications service).</param>
/// <param name="CandidateId">The candidate that applied; duplicated reference data, not a cross-service FK.</param>
/// <param name="JobId">The posting applied to; duplicated reference data, not a cross-service FK.</param>
/// <param name="SubmittedOnUtc">When the application was submitted.</param>
public sealed record ApplicationSubmitted(
    Guid Id,
    Guid ApplicationId,
    Guid CandidateId,
    Guid JobId,
    DateTime SubmittedOnUtc) : IIntegrationEvent
{
    /// <inheritdoc/>
    public Guid CorrelationId { get; init; }

    /// <inheritdoc/>
    public Guid CausationId { get; init; }

    /// <inheritdoc/>
    public Guid? ActorId { get; init; }
}
