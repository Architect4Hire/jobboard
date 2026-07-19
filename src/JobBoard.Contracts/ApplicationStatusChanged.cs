namespace JobBoard.Contracts;

/// <summary>
/// An application moved from one status to another. A fact, published by Applications whenever an
/// application transitions — advanced or withdrawn by request, or closed because its job closed — carried
/// across the bus so other services can react (e.g. Notifications emailing the candidate). The statuses
/// are plain strings: Contracts is a leaf and never references Applications' <c>ApplicationStatus</c>
/// enum. No behavior, no EF, no Domain types.
/// </summary>
/// <param name="Id">The event's own identity — the outbox row key and Service Bus <c>MessageId</c>.
/// Stamped fresh by the business layer when it builds the event; consumers dedupe on it.</param>
/// <param name="ApplicationId">The application that changed (its identity in the Applications service).</param>
/// <param name="CandidateId">The candidate that owns the application; duplicated reference data, not a cross-service FK.</param>
/// <param name="JobId">The posting the application is for; duplicated reference data, not a cross-service FK.</param>
/// <param name="FromStatus">The status the application moved out of.</param>
/// <param name="ToStatus">The status the application moved into.</param>
/// <param name="ChangedOnUtc">When the transition happened.</param>
public sealed record ApplicationStatusChanged(
    Guid Id,
    Guid ApplicationId,
    Guid CandidateId,
    Guid JobId,
    string FromStatus,
    string ToStatus,
    DateTime ChangedOnUtc) : IIntegrationEvent
{
    /// <inheritdoc/>
    public Guid CorrelationId { get; init; }

    /// <inheritdoc/>
    public Guid CausationId { get; init; }

    /// <inheritdoc/>
    public Guid? ActorId { get; init; }
}
