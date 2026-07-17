using JobBoard.Shared.Messaging;
using JobBoard.Shared.Persistence;

namespace JobBoard.Shared.Tests;

/// <summary>A shared, singleton tally of how many times a consumer's side effect actually ran.</summary>
public sealed class CallCounter
{
    public int Count { get; private set; }

    public void Increment() => Count++;
}

/// <summary>
/// A representative consumer that owns its own idempotency (as real consumers do): it dedupes on the event id via
/// <see cref="IInbox"/> and, only for a new message, performs its side effect (bumping the <see cref="CallCounter"/>)
/// and records the id — all against the same scoped context so they commit together.
/// </summary>
public sealed class RecordingConsumer : IIntegrationEventConsumer<FakeEvent>
{
    private readonly BaseDbContext _context;
    private readonly IInbox _inbox;
    private readonly CallCounter _counter;

    public RecordingConsumer(BaseDbContext context, IInbox inbox, CallCounter counter)
    {
        _context = context;
        _inbox = inbox;
        _counter = counter;
    }

    public async Task ConsumeAsync(FakeEvent @event, CancellationToken cancellationToken = default)
    {
        if (await _inbox.HasProcessedAsync(@event.Id, cancellationToken))
        {
            return;
        }

        _counter.Increment();
        await _inbox.MarkProcessedAsync(@event.Id, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
