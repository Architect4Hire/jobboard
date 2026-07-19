namespace JobBoard.Contracts;

/// <summary>
/// Fact: a login attempt was rejected. Published by Identity from the login endpoint via its outbox, and
/// consumed by Audit so support can see failed attempts against an <see cref="Email"/> in the trail
/// (ADR-0013/0014). Carries the attempted <see cref="Email"/> and a coarse <see cref="Reason"/> only —
/// <b>never</b> the password, and deliberately no account id: the reason is uniform ("invalid_credentials")
/// so the trail can't be used to tell whether an email exists, mirroring the endpoint's uniform 401. There
/// is no authenticated actor, so <see cref="IIntegrationEvent.ActorId"/> is <c>null</c>.
/// </summary>
public sealed record LoginFailed(
    Guid Id,
    string Email,
    string Reason,
    DateTime OccurredOnUtc) : IIntegrationEvent
{
    /// <inheritdoc/>
    public Guid CorrelationId { get; init; }

    /// <inheritdoc/>
    public Guid CausationId { get; init; }

    /// <inheritdoc/>
    public Guid? ActorId { get; init; }
}
