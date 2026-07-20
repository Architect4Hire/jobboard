using JobBoard.Applications.Core.Facade;
using JobBoard.Contracts;
using JobBoard.Shared.Messaging;

namespace JobBoard.Applications.Consumers;

/// <summary>
/// Reacts to Profiles' <see cref="EmployerProfileChanged"/> event: mirrors the employer's company name into
/// this service's local <c>EmployerReference</c> projection (ADR-0012 option B), so
/// <c>GET /applications/mine</c> can show an employer name without calling back into Profiles. The shared
/// <c>ServiceBusProcessorHost</c> resolves and calls it when a message arrives on the
/// <c>EmployerProfileChanged</c> topic's <c>applications</c> subscription. Thin by design — it maps the
/// event to a facade call; idempotency (the inbox) lives in the data-layer transaction, so a redelivered
/// message is a no-op.
/// </summary>
public sealed class EmployerProfileChangedConsumer : IIntegrationEventConsumer<EmployerProfileChanged>
{
    private readonly IApplicationFacade _facade;

    public EmployerProfileChangedConsumer(IApplicationFacade facade) => _facade = facade;

    public Task ConsumeAsync(EmployerProfileChanged @event, CancellationToken cancellationToken = default) =>
        _facade.HandleEmployerProfileChangedAsync(@event, cancellationToken);
}
