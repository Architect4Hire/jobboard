using JobBoard.Contracts;

namespace JobBoard.Shared.Tests;

/// <summary>A minimal integration event for exercising the outbox.</summary>
public sealed record FakeEvent(Guid Id, string Name) : IIntegrationEvent
{
    /// <inheritdoc/>
    public Guid CorrelationId { get; init; }

    /// <inheritdoc/>
    public Guid CausationId { get; init; }

    /// <inheritdoc/>
    public Guid? ActorId { get; init; }
}
