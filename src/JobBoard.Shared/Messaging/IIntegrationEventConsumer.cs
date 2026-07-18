using JobBoard.Contracts;

namespace JobBoard.Shared.Messaging;

/// <summary>
/// Implemented by a service's <c>&lt;Event&gt;Consumer</c> for each integration event it reacts to. The
/// Service Bus processor host (added in the messaging phase) resolves and calls it when the event
/// arrives. A consumer is idempotent — it dedupes on the message id via <see cref="IInbox"/> in the same
/// transaction as its side effect — and writes only its own service's database.
/// </summary>
public interface IIntegrationEventConsumer<in TEvent>
    where TEvent : IIntegrationEvent
{
    Task ConsumeAsync(TEvent @event, CancellationToken cancellationToken = default);
}
