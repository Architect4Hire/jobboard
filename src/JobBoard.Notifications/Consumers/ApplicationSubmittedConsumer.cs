using JobBoard.Contracts;
using JobBoard.Notifications.Core.Facade;
using JobBoard.Shared.Messaging;

namespace JobBoard.Notifications.Consumers;

/// <summary>
/// Reacts to Applications' <see cref="ApplicationSubmitted"/> event by logging a notification for the
/// candidate. The shared <c>ServiceBusProcessorHost</c> resolves and calls it when a message arrives on
/// the <c>ApplicationSubmitted</c> topic's <c>notifications-submitted</c> subscription. Thin by design —
/// idempotency (the inbox) lives in the data-layer transaction, so a redelivery is a no-op.
/// </summary>
public sealed class ApplicationSubmittedConsumer : IIntegrationEventConsumer<ApplicationSubmitted>
{
    private readonly INotificationFacade _facade;

    public ApplicationSubmittedConsumer(INotificationFacade facade) => _facade = facade;

    public Task ConsumeAsync(ApplicationSubmitted @event, CancellationToken cancellationToken = default) =>
        _facade.HandleApplicationSubmittedAsync(@event, cancellationToken);
}
