using JobBoard.Contracts;

namespace JobBoard.Notifications.Core.Business;

/// <summary>
/// The Notifications domain: turns each consumed event into a notification and records it (idempotently,
/// via the data layer). One handler per consumed event. Depends only on
/// <see cref="Data.INotificationDataLayer"/>.
/// </summary>
public interface INotificationBusiness
{
    Task HandleApplicationSubmittedAsync(ApplicationSubmitted @event, CancellationToken cancellationToken = default);

    Task HandleApplicationStatusChangedAsync(ApplicationStatusChanged @event, CancellationToken cancellationToken = default);

    Task HandleJobPostedAsync(JobPosted @event, CancellationToken cancellationToken = default);
}
