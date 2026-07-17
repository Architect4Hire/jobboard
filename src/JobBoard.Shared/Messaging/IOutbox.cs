using JobBoard.Contracts;

namespace JobBoard.Shared.Messaging;

/// <summary>
/// Writes an integration event to the current service's <c>OutboxMessages</c> table on the same scoped
/// <see cref="Persistence.BaseDbContext"/> the domain write uses, so the row enlists in that write's
/// transaction. It never touches Service Bus — the dispatcher relays the row later. The data layer calls
/// this inside <see cref="Persistence.IRepository.ExecuteInTransactionAsync{T}"/>.
/// </summary>
public interface IOutbox
{
    /// <summary>
    /// Serializes <paramref name="event"/> into an outbox row keyed by its own <see cref="IIntegrationEvent.Id"/>.
    /// Enqueuing the same event id twice is a no-op, so a replayed transaction cannot duplicate the row.
    /// </summary>
    Task EnqueueAsync(IIntegrationEvent @event, CancellationToken cancellationToken = default);
}
