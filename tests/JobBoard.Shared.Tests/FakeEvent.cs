using JobBoard.Contracts;

namespace JobBoard.Shared.Tests;

/// <summary>A minimal integration event for exercising the outbox.</summary>
public sealed record FakeEvent(Guid Id, string Name) : IIntegrationEvent;
