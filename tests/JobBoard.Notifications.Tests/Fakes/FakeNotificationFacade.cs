using JobBoard.Contracts;
using JobBoard.Notifications.Core.Facade;

namespace JobBoard.Notifications.Tests.Fakes;

/// <summary>Hand-rolled <see cref="INotificationFacade"/> for consumer tests — records which handler ran.</summary>
public sealed class FakeNotificationFacade : INotificationFacade
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
