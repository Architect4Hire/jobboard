namespace JobBoard.Contracts;

/// <summary>
/// Fact: a candidate or employer profile was updated. Published by Profiles from its profile-write paths
/// (company/résumé profile upsert and résumé upload/delete) via its outbox, and consumed by Audit to record
/// the change in the support trail (ADR-0013/0014). Carries only the owning profile id, a
/// <see cref="ProfileType"/> discriminator ("Candidate"/"Employer"), and when it changed — <b>never</b> the
/// profile's field values (no résumé/company PII lands in the trail).
/// </summary>
public sealed record ProfileUpdated(
    Guid Id,
    Guid ProfileId,
    string ProfileType,
    DateTime OccurredOnUtc) : IIntegrationEvent
{
    /// <inheritdoc/>
    public Guid CorrelationId { get; init; }

    /// <inheritdoc/>
    public Guid CausationId { get; init; }

    /// <inheritdoc/>
    public Guid? ActorId { get; init; }
}
