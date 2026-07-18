using JobBoard.Contracts;
using JobBoard.Notifications.Core.Business;

namespace JobBoard.Notifications.Core.Facade;

/// <inheritdoc cref="INotificationFacade"/>
public sealed class NotificationFacade : INotificationFacade
{
    private readonly INotificationBusiness _business;

    public NotificationFacade(INotificationBusiness business) => _business = business;

    public Task HandleApplicationSubmittedAsync(ApplicationSubmitted @event, CancellationToken cancellationToken = default) =>
        _business.HandleApplicationSubmittedAsync(@event, cancellationToken);

    public Task HandleApplicationStatusChangedAsync(ApplicationStatusChanged @event, CancellationToken cancellationToken = default) =>
        _business.HandleApplicationStatusChangedAsync(@event, cancellationToken);

    public Task HandleJobPostedAsync(JobPosted @event, CancellationToken cancellationToken = default) =>
        _business.HandleJobPostedAsync(@event, cancellationToken);
}
