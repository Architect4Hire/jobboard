using JobBoard.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobBoard.Shared.Messaging;

/// <inheritdoc cref="IInbox"/>
public sealed class Inbox : IInbox
{
    private readonly BaseDbContext _context;

    public Inbox(BaseDbContext context) => _context = context;

    public Task<bool> HasProcessedAsync(Guid messageId, CancellationToken cancellationToken = default) =>
        _context.InboxMessages.AnyAsync(m => m.MessageId == messageId, cancellationToken);

    public async Task MarkProcessedAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        // The message id is deterministic, so — like the outbox — guard against re-adding the same row
        // when the surrounding transaction is replayed by the execution strategy.
        var existing = await _context.InboxMessages.FindAsync([messageId], cancellationToken);
        if (existing is not null)
        {
            return;
        }

        var message = new InboxMessage
        {
            MessageId = messageId,
            ProcessedOnUtc = DateTime.UtcNow,
        };

        await _context.InboxMessages.AddAsync(message, cancellationToken);
    }
}
