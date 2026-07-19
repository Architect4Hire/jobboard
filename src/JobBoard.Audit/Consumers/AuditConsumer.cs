using JobBoard.Audit.Core.Facade;
using JobBoard.Contracts;
using JobBoard.Shared.Messaging;

namespace JobBoard.Audit.Consumers;

/// <summary>
/// The one audit sink, generic over every business event. Registered once per event type
/// (<c>AuditConsumer&lt;JobPosted&gt;</c>, <c>AuditConsumer&lt;JobClosed&gt;</c>, …), so the shared
/// <c>ServiceBusProcessorHost</c> resolves and calls it when a message arrives on any <c>audit-*</c>
/// subscription. Thin by design — it forwards the event to the facade; the mapping to one immutable row
/// and the inbox idempotency live in the append transaction, so a redelivery is a no-op and each event is
/// recorded exactly once. A single generic sink is preferred over a consumer per type because the audit
/// row shape is uniform (see <see cref="Core.Managers.Mappers.AuditEntryMapper"/>).
/// </summary>
public sealed class AuditConsumer<TEvent> : IIntegrationEventConsumer<TEvent>
    where TEvent : IIntegrationEvent
{
    private readonly IAuditFacade _facade;

    public AuditConsumer(IAuditFacade facade) => _facade = facade;

    public Task ConsumeAsync(TEvent @event, CancellationToken cancellationToken = default) =>
        _facade.RecordAsync(@event, cancellationToken);
}
