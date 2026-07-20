using JobBoard.Applications.Core.Facade;
using JobBoard.Contracts;
using JobBoard.Shared.Messaging;

namespace JobBoard.Applications.Consumers;

/// <summary>
/// Reacts to Jobs' <see cref="JobPosted"/> event: mirrors the job's title and owning employer into this
/// service's local <c>JobReference</c> projection (ADR-0012 option B), so <c>GET /applications/mine</c> can
/// show a job title without calling back into Jobs. The shared <c>ServiceBusProcessorHost</c> resolves and
/// calls it when a message arrives on the <c>JobPosted</c> topic's <c>applications</c> subscription. Thin
/// by design — it maps the event to a facade call; idempotency (the inbox) lives in the data-layer
/// transaction, so a redelivered message is a no-op.
/// </summary>
public sealed class JobPostedConsumer : IIntegrationEventConsumer<JobPosted>
{
    private readonly IApplicationFacade _facade;

    public JobPostedConsumer(IApplicationFacade facade) => _facade = facade;

    public Task ConsumeAsync(JobPosted @event, CancellationToken cancellationToken = default) =>
        _facade.HandleJobPostedAsync(@event, cancellationToken);
}
