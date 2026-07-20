using System.Collections.Concurrent;
using Azure.Messaging.ServiceBus;
using JobBoard.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobBoard.Shared.Messaging;

/// <summary>
/// The send side of the transactional outbox — and the <b>only</b> thing that sends to Service Bus.
/// It relays unprocessed <see cref="OutboxMessage"/> rows (oldest-first) as <see cref="ServiceBusMessage"/>s
/// and stamps each once sent. Delivery is at-least-once: a crash after a send but before the stamp
/// commits re-sends the same <see cref="ServiceBusMessage.MessageId"/>, which the consumer's inbox dedupes.
/// </summary>
/// <remarks>
/// Extracted from <see cref="OutboxDispatcher"/> so the relay logic is exercised directly in tests against a
/// real relational context and a fake <see cref="ServiceBusClient"/>, without standing up the hosted loop.
/// </remarks>
public sealed class OutboxRelay
{
    private readonly ServiceBusClient _client;
    private readonly OutboxRelayOptions _options;
    private readonly ILogger<OutboxRelay> _logger;

    // One sender per destination topic, reused across polls. ServiceBusSender is thread-safe and cheap to hold.
    private readonly ConcurrentDictionary<string, ServiceBusSender> _senders = new();

    public OutboxRelay(ServiceBusClient client, IOptions<OutboxRelayOptions> options, ILogger<OutboxRelay> logger)
    {
        _client = client;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Sends every unprocessed outbox row on <paramref name="context"/> (oldest-first, up to the batch size),
    /// stamping <see cref="OutboxMessage.ProcessedOnUtc"/> as each send succeeds. A failed send stops the batch
    /// so ordering is preserved and the row is retried next poll; stamps for rows already sent are still saved.
    /// </summary>
    public async Task RelayAsync(BaseDbContext context, CancellationToken cancellationToken = default)
    {
        var pending = await context.OutboxMessages
            .Where(m => m.ProcessedOnUtc == null)
            .OrderBy(m => m.OccurredOnUtc)
            .Take(_options.BatchSize)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
        {
            return;
        }

        foreach (var row in pending)
        {
            try
            {
                var sender = _senders.GetOrAdd(row.Destination, _client.CreateSender);

                var message = new ServiceBusMessage(row.Payload)
                {
                    MessageId = row.Id.ToString(),
                    Subject = row.Type,
                };

                await sender.SendMessageAsync(message, cancellationToken);

                row.ProcessedOnUtc = DateTime.UtcNow;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Leave this row (and the rest of the batch) unstamped so the next poll retries in order.
                _logger.LogError(ex, "Failed to relay outbox message {MessageId} to {Destination}; will retry.",
                    row.Id, row.Destination);
                break;
            }
        }

        // Persist the stamps of whatever sent successfully before any failure.
        await context.SaveChangesAsync(cancellationToken);
    }
}
