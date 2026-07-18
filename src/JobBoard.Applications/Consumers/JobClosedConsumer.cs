using JobBoard.Applications.Core.Facade;
using JobBoard.Contracts;
using JobBoard.Shared.Messaging;

namespace JobBoard.Applications.Consumers;

/// <summary>
/// Reacts to Jobs' <see cref="JobClosed"/> event: closes this service's open applications for the job.
/// The shared <c>ServiceBusProcessorHost</c> resolves and calls it when a message arrives on the
/// <c>JobClosed</c> topic's <c>applications</c> subscription. Thin by design — it maps the event to a
/// facade call; idempotency (the inbox) and the atomic close live in the data-layer transaction, so a
/// redelivered message is a no-op and the applications are closed exactly once.
/// </summary>
public sealed class JobClosedConsumer : IIntegrationEventConsumer<JobClosed>
{
    private readonly IApplicationFacade _facade;

    public JobClosedConsumer(IApplicationFacade facade) => _facade = facade;

    public Task ConsumeAsync(JobClosed @event, CancellationToken cancellationToken = default) =>
        _facade.HandleJobClosedAsync(@event, cancellationToken);
}
