using System.Text.Json;
using JobBoard.Contracts;
using JobBoard.Shared.Persistence;

namespace JobBoard.Shared.Messaging;

/// <inheritdoc cref="IOutbox"/>
public sealed class Outbox : IOutbox
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly BaseDbContext _context;

    public Outbox(BaseDbContext context) => _context = context;

    public async Task EnqueueAsync(IIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        // Deterministic id → replay-safe. FindAsync checks the change tracker first, so a strategy retry
        // that re-runs the operation finds the row it already staged and does not add a second one.
        var existing = await _context.OutboxMessages.FindAsync([@event.Id], cancellationToken);
        if (existing is not null)
        {
            return;
        }

        var eventType = @event.GetType();

        var message = new OutboxMessage
        {
            Id = @event.Id,
            Type = eventType.Name,
            // Topic-per-event-type convention for now; the real topic mapping arrives with the dispatcher.
            Destination = eventType.Name,
            // Serialize by the runtime type so a derived record's fields are captured, not just IIntegrationEvent's.
            Payload = JsonSerializer.Serialize(@event, eventType, SerializerOptions),
            OccurredOnUtc = DateTime.UtcNow,
            ProcessedOnUtc = null,
        };

        await _context.OutboxMessages.AddAsync(message, cancellationToken);
    }
}
