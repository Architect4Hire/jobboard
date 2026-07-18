using JobBoard.Contracts;
using JobBoard.Notifications.Core.Business;

namespace JobBoard.Notifications.Tests.Fakes;

/// <summary>Hand-rolled <see cref="INotificationBusiness"/> for facade tests — records which handler ran.</summary>
public sealed class FakeNotificationBusiness : INotificationBusiness
{
    public ApplicationSubmitted? Submitted { get; private set; }

    public ApplicationStatusChanged? StatusChanged { get; private set; }

    public JobPosted? Posted { get; private set; }

    public Task HandleApplicationSubmittedAsync(ApplicationSubmitted @event, CancellationToken cancellationToken = default)
    {
        Submitted = @event;
        return Task.CompletedTask;
    }

    public Task HandleApplicationStatusChangedAsync(ApplicationStatusChanged @event, CancellationToken cancellationToken = default)
    {
        StatusChanged = @event;
        return Task.CompletedTask;
    }

    public Task HandleJobPostedAsync(JobPosted @event, CancellationToken cancellationToken = default)
    {
        Posted = @event;
        return Task.CompletedTask;
    }
}
