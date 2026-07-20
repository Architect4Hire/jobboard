using JobBoard.Contracts;
using JobBoard.Notifications.Core.Facade;
using JobBoard.Shared.Messaging;

namespace JobBoard.Notifications.Consumers;

/// <summary>
/// Reacts to Applications' <see cref="ApplicationStatusChanged"/> event by logging a notification for the
/// candidate. Delivered on the <c>ApplicationStatusChanged</c> topic's <c>notifications-status-changed</c>
/// subscription. Thin — the inbox in the data-layer transaction makes a redelivery a no-op.
/// </summary>
public sealed class ApplicationStatusChangedConsumer : IIntegrationEventConsumer<ApplicationStatusChanged>
{
    private readonly INotificationFacade _facade;

    public ApplicationStatusChangedConsumer(INotificationFacade facade) => _facade = facade;

    public Task ConsumeAsync(ApplicationStatusChanged @event, CancellationToken cancellationToken = default) =>
        _facade.HandleApplicationStatusChangedAsync(@event, cancellationToken);
}
