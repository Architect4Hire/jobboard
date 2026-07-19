namespace JobBoard.Contracts;

/// <summary>
/// Fact: a user account was created (registration). Published by Identity from the register endpoint via
/// its outbox, and consumed by Audit to record the cradle of an account's life in the support trail
/// (ADR-0013/0014). Carries only what the trail needs to identify the account — its id, the login
/// <see cref="Email"/>, and the <see cref="Role"/> — and <b>never</b> the password or its hash. The acting
/// identity is the account itself (self-originated), so <see cref="IIntegrationEvent.ActorId"/> equals
/// <see cref="AccountId"/>.
/// </summary>
public sealed record AccountCreated(
    Guid Id,
    Guid AccountId,
    string Email,
    string Role,
    DateTime OccurredOnUtc) : IIntegrationEvent
{
    /// <inheritdoc/>
    public Guid CorrelationId { get; init; }

    /// <inheritdoc/>
    public Guid CausationId { get; init; }

    /// <inheritdoc/>
    public Guid? ActorId { get; init; }
}
