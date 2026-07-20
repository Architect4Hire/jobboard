namespace JobBoard.Shared.Messaging;

/// <summary>
/// One entry in the <see cref="ConsumerRegistry"/>: a consuming service subscribes to an event's topic under
/// a named subscription. Recorded by <c>AddIntegrationEventConsumer&lt;TEvent, TConsumer&gt;()</c> so the
/// <see cref="ServiceBusProcessorHost"/> knows which subscriptions to open and how to deserialize each message.
/// </summary>
/// <param name="EventName">The event type's simple name — matched against a message's <c>Subject</c>.</param>
/// <param name="EventType">The concrete event type, used to deserialize the message body.</param>
/// <param name="Topic">The Service Bus topic the event is published to (the event type name, per convention).</param>
/// <param name="Subscription">This service's subscription on that topic (the consuming service's name).</param>
public sealed record ConsumerRegistration(string EventName, Type EventType, string Topic, string Subscription);
