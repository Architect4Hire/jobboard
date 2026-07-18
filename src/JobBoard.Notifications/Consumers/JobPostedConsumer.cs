using JobBoard.Contracts;
using JobBoard.Notifications.Core.Facade;
using JobBoard.Shared.Messaging;

namespace JobBoard.Notifications.Consumers;

/// <summary>
/// Reacts to Jobs' <see cref="JobPosted"/> event by logging a confirmation for the employer. Delivered on
/// the <c>JobPosted</c> topic's <c>notifications-jobposted</c> subscription. Thin — the inbox in the
/// data-layer transaction makes a redelivery a no-op.
/// </summary>
public sealed class JobPostedConsumer : IIntegrationEventConsumer<JobPosted>
{
    private readonly INotificationFacade _facade;

    public JobPostedConsumer(INotificationFacade facade) => _facade = facade;

    public Task ConsumeAsync(JobPosted @event, CancellationToken cancellationToken = default) =>
        _facade.HandleJobPostedAsync(@event, cancellationToken);
}
