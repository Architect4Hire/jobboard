namespace JobBoard.Shared.Messaging;

/// <summary>
/// The host-wide catalogue of integration-event consumers, populated at registration time by
/// <c>AddIntegrationEventConsumer&lt;TEvent, TConsumer&gt;()</c> and read once by the
/// <see cref="ServiceBusProcessorHost"/> (to open a processor per subscription) and by the
/// <see cref="IntegrationEventProcessor"/> (to resolve an event type from a message's <c>Subject</c>).
/// A singleton; mutated only during service registration, read-only thereafter.
/// </summary>
public sealed class ConsumerRegistry
{
    private readonly List<ConsumerRegistration> _registrations = [];

    public void Add(ConsumerRegistration registration) => _registrations.Add(registration);

    /// <summary>Every registered consumer.</summary>
    public IReadOnlyList<ConsumerRegistration> Registrations => _registrations;

    /// <summary>The distinct <c>(topic, subscription)</c> pairs to open a processor for.</summary>
    public IEnumerable<(string Topic, string Subscription)> Subscriptions =>
        _registrations.Select(r => (r.Topic, r.Subscription)).Distinct();

    /// <summary>Resolves the event type for a message <c>Subject</c>, or <c>null</c> if none is registered.</summary>
    public Type? ResolveEventType(string eventName) =>
        _registrations.FirstOrDefault(r => r.EventName == eventName)?.EventType;
}
