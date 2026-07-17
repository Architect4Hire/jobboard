using JobBoard.Contracts;
using JobBoard.Shared.Messaging;

namespace JobBoard.Applications.Tests.Fakes;

/// <summary>
/// Records the events enqueued so a data-layer test can assert what was published — and, when
/// <see cref="ThrowOnEnqueue"/> is set, fails inside the transaction so a test can prove rollback.
/// </summary>
public sealed class FakeOutbox : IOutbox
{
    public List<IIntegrationEvent> Enqueued { get; } = [];

    public bool ThrowOnEnqueue { get; init; }

    public Task EnqueueAsync(IIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        if (ThrowOnEnqueue)
        {
            throw new InvalidOperationException("outbox boom");
        }

        Enqueued.Add(@event);
        return Task.CompletedTask;
    }
}
