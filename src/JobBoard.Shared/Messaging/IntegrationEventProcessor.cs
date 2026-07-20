using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using JobBoard.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace JobBoard.Shared.Messaging;

/// <summary>
/// The receive-side core: turns one <see cref="ServiceBusReceivedMessage"/> into a call on the registered
/// <see cref="IIntegrationEventConsumer{TEvent}"/>. It only deserializes, resolves, and invokes — the consumer
/// owns idempotency (the inbox check + its side effect in one transaction). Extracted from
/// <see cref="ServiceBusProcessorHost"/> so the dispatch/dedup path is testable without the Service Bus plumbing.
/// </summary>
public sealed class IntegrationEventProcessor
{
    // Match Outbox.cs so what the dispatcher serialized round-trips exactly.
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    // typeof(IIntegrationEventConsumer<TEvent>).ConsumeAsync, cached per event type.
    private static readonly ConcurrentDictionary<Type, (Type Service, MethodInfo Method)> ConsumerCache = new();

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ConsumerRegistry _registry;
    private readonly ILogger<IntegrationEventProcessor> _logger;

    public IntegrationEventProcessor(
        IServiceScopeFactory scopeFactory,
        ConsumerRegistry registry,
        ILogger<IntegrationEventProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _registry = registry;
        _logger = logger;
    }

    /// <summary>
    /// Resolves the event type from <see cref="ServiceBusReceivedMessage.Subject"/>, deserializes the body, and
    /// invokes the consumer registered for it. Throws on an unknown subject, a missing consumer, or a consumer
    /// failure — the caller leaves the message unsettled so Service Bus redelivers it.
    /// </summary>
    public async Task ProcessAsync(ServiceBusReceivedMessage message, CancellationToken cancellationToken = default)
    {
        var eventType = _registry.ResolveEventType(message.Subject)
            ?? throw new InvalidOperationException($"No consumer registered for event '{message.Subject}'.");

        var @event = (IIntegrationEvent)(JsonSerializer.Deserialize(message.Body.ToString(), eventType, SerializerOptions)
            ?? throw new InvalidOperationException($"Message {message.MessageId} deserialized to null for '{message.Subject}'."));

        var (serviceType, consumeMethod) = ConsumerCache.GetOrAdd(eventType, static type =>
        {
            var service = typeof(IIntegrationEventConsumer<>).MakeGenericType(type);
            var method = service.GetMethod(nameof(IIntegrationEventConsumer<IIntegrationEvent>.ConsumeAsync))!;
            return (service, method);
        });

        await using var scope = _scopeFactory.CreateAsyncScope();
        var consumer = scope.ServiceProvider.GetService(serviceType)
            ?? throw new InvalidOperationException($"No {serviceType} resolved for event '{message.Subject}'.");

        _logger.LogDebug("Dispatching {EventName} {MessageId} to {Consumer}.",
            message.Subject, message.MessageId, consumer.GetType().Name);

        // The consumer dedupes on the event id and applies its side effect in one transaction (see messaging.md).
        await (Task)consumeMethod.Invoke(consumer, [@event, cancellationToken])!;
    }
}
