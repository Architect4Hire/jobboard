namespace JobBoard.Contracts;

/// <summary>
/// Fact: an employer's company profile was written (created or updated). Published by Profiles from the
/// employer-profile write path via its outbox, alongside (not instead of) <see cref="ProfileUpdated"/> —
/// that event is the PII-free audit fact Audit consumes; this one exists purely for state transfer, so it
/// carries the field a consumer actually needs. Consumed by Applications to keep its local
/// <c>EmployerReference</c> projection current for the "my applications" read (ADR-0012).
/// </summary>
public sealed record EmployerProfileChanged(
    Guid Id,
    Guid EmployerId,
    string CompanyName,
    DateTime OccurredOnUtc) : IIntegrationEvent
{
    /// <inheritdoc/>
    public Guid CorrelationId { get; init; }

    /// <inheritdoc/>
    public Guid CausationId { get; init; }

    /// <inheritdoc/>
    public Guid? ActorId { get; init; }
}
