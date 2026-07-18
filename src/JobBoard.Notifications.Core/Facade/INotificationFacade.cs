using JobBoard.Contracts;

namespace JobBoard.Notifications.Core.Facade;

/// <summary>
/// The seam the consumers call — one entry per consumed event. Thin: it delegates to
/// <see cref="Business.INotificationBusiness"/> (there is nothing to validate or cache; events are facts).
/// </summary>
public interface INotificationFacade
{
    Task HandleApplicationSubmittedAsync(ApplicationSubmitted @event, CancellationToken cancellationToken = default);

    Task HandleApplicationStatusChangedAsync(ApplicationStatusChanged @event, CancellationToken cancellationToken = default);

    Task HandleJobPostedAsync(JobPosted @event, CancellationToken cancellationToken = default);
}
