using JobBoard.Shared.Messaging;

namespace JobBoard.Applications.Tests.Fakes;

/// <summary>
/// Hand-rolled <see cref="IInbox"/> for data-layer composition tests. <see cref="AlreadyProcessed"/>
/// simulates a redelivery (the message id is already recorded); <see cref="Marked"/> captures what the
/// operation stamped so a test can prove the inbox row was written in the same unit as the side effect.
/// </summary>
public sealed class FakeInbox : IInbox
{
    public bool AlreadyProcessed { get; init; }

    public List<Guid> Marked { get; } = [];

    public Task<bool> HasProcessedAsync(Guid messageId, CancellationToken cancellationToken = default) =>
        Task.FromResult(AlreadyProcessed);

    public Task MarkProcessedAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        Marked.Add(messageId);
        return Task.CompletedTask;
    }
}
