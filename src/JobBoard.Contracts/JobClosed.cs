namespace JobBoard.Contracts;

/// <summary>
/// A job posting was closed. A fact, published by Jobs when an open posting transitions to Closed,
/// carried across the bus so other services can react (e.g. Applications closing its open applications).
/// Carries only what a consumer needs: the posting and its employer, plus when it happened. No behavior,
/// no EF, no reference to Jobs' Domain types.
/// </summary>
/// <param name="Id">The event's own identity — the outbox row key and Service Bus <c>MessageId</c>.
/// Stamped fresh by the business layer when it builds the event; consumers dedupe on it.</param>
/// <param name="JobId">The posting that was closed (its identity in the Jobs service).</param>
/// <param name="EmployerId">The employer that owned the posting; duplicated reference data, not a cross-service FK.</param>
/// <param name="ClosedOnUtc">When the posting was closed.</param>
public sealed record JobClosed(Guid Id, Guid JobId, Guid EmployerId, DateTime ClosedOnUtc) : IIntegrationEvent;
