namespace JobBoard.Contracts;

/// <summary>
/// Marker for every integration event published across the JobBoard bus.
/// The <see cref="Id"/> is the event's stable identity — used as the Service Bus
/// <c>MessageId</c> and by the inbox for idempotent, at-least-once delivery.
/// Contracts is a leaf library: it holds event records only and references nothing.
/// </summary>
public interface IIntegrationEvent
{
    Guid Id { get; }
}
