using JobBoard.Contracts;
using JobBoard.Notifications.Core.Data;
using JobBoard.Notifications.Core.Managers.Mappers;

namespace JobBoard.Notifications.Core.Business;

/// <inheritdoc cref="INotificationBusiness"/>
/// <remarks>
/// Each handler maps the event to a <c>NotificationLog</c> and hands it to the data layer keyed by the
/// event's <c>Id</c> — the same id the outbox stamped as the Service Bus <c>MessageId</c>, so the inbox
/// dedupes an at-least-once redelivery.
/// </remarks>
public sealed class NotificationBusiness : INotificationBusiness
{
    private readonly INotificationDataLayer _dataLayer;

    public NotificationBusiness(INotificationDataLayer dataLayer) => _dataLayer = dataLayer;

    public Task HandleApplicationSubmittedAsync(ApplicationSubmitted @event, CancellationToken cancellationToken = default) =>
        _dataLayer.RecordAsync(@event.ToNotificationLog(), @event.Id, cancellationToken);

    public Task HandleApplicationStatusChangedAsync(ApplicationStatusChanged @event, CancellationToken cancellationToken = default) =>
        _dataLayer.RecordAsync(@event.ToNotificationLog(), @event.Id, cancellationToken);

    public Task HandleJobPostedAsync(JobPosted @event, CancellationToken cancellationToken = default) =>
        _dataLayer.RecordAsync(@event.ToNotificationLog(), @event.Id, cancellationToken);
}
