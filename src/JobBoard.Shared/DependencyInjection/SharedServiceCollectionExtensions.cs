using JobBoard.Contracts;
using JobBoard.Shared.Caching;
using JobBoard.Shared.Errors;
using JobBoard.Shared.Messaging;
using JobBoard.Shared.Persistence;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration entry points for the shared persistence/messaging spine, called from each host's
/// composition root. Base repositories and the cache implementation are deliberately not registered here
/// — a service registers its own <c>&lt;Feature&gt;Repository</c>, and the cache impl arrives with Redis.
/// </summary>
public static class SharedServiceCollectionExtensions
{
    /// <summary>
    /// Registers the outbox and inbox on the request scope. Assumes the service's
    /// <see cref="BaseDbContext"/> is already resolvable — use the
    /// <see cref="AddSharedPersistence{TContext}"/> overload to wire that bridge in one call.
    /// </summary>
    public static IServiceCollection AddSharedPersistence(this IServiceCollection services)
    {
        // TryAdd so this composes with AddSharedMessaging in either order without double-registering.
        services.TryAddScoped<IOutbox, Outbox>();
        services.TryAddScoped<IInbox, Inbox>();
        return services;
    }

    /// <summary>
    /// Registers the outbox and inbox and bridges the service's concrete <typeparamref name="TContext"/> to
    /// <see cref="BaseDbContext"/>, so the outbox/inbox rows land on the very same scoped context the
    /// repository writes through (and thus enlist in its transaction).
    /// </summary>
    public static IServiceCollection AddSharedPersistence<TContext>(this IServiceCollection services)
        where TContext : BaseDbContext
    {
        services.TryAddScoped<BaseDbContext>(sp => sp.GetRequiredService<TContext>());
        return services.AddSharedPersistence();
    }

    /// <summary>
    /// Registers the transactional-outbox relay: the outbox/inbox on the request scope (bridging
    /// <typeparamref name="TDbContext"/> to <see cref="BaseDbContext"/> so their rows enlist in the domain
    /// transaction), plus the <see cref="OutboxDispatcher"/> and <see cref="ServiceBusProcessorHost"/> background
    /// services. A superset of <see cref="AddSharedPersistence{TContext}"/> (uses <c>TryAdd</c>, so calling both
    /// stays safe). Assumes the host has registered a <c>ServiceBusClient</c> via <c>AddAzureServiceBusClient</c>.
    /// </summary>
    public static IServiceCollection AddSharedMessaging<TContext>(
        this IServiceCollection services,
        Action<OutboxRelayOptions>? configure = null)
        where TContext : BaseDbContext
    {
        // Bridge + outbox/inbox — same wiring as AddSharedPersistence, but idempotent so both can be called.
        services.TryAddScoped<BaseDbContext>(sp => sp.GetRequiredService<TContext>());
        services.TryAddScoped<IOutbox, Outbox>();
        services.TryAddScoped<IInbox, Inbox>();

        services.Configure(configure ?? (_ => { }));

        // The relay workers are shared, thread-safe singletons; the two hosts drive them. The registry is a
        // single instance populated at registration time (see AddIntegrationEventConsumer), so seed it here.
        GetOrCreateConsumerRegistry(services);
        services.TryAddSingleton<OutboxRelay>();
        services.TryAddSingleton<IntegrationEventProcessor>();

        services.AddHostedService<OutboxDispatcher>();
        services.AddHostedService<ServiceBusProcessorHost>();

        return services;
    }

    /// <summary>
    /// Registers a consumer for <typeparamref name="TEvent"/> under this service's <paramref name="subscription"/>.
    /// The topic is the event type's name — the same value <see cref="Outbox"/> stamps as the row's
    /// <c>Destination</c> — so a consumer can never subscribe to a topic the dispatcher doesn't publish to. The
    /// <see cref="ServiceBusProcessorHost"/> opens a processor per registered subscription and dispatches to the
    /// consumer. Call after <see cref="AddSharedMessaging{TContext}"/>.
    /// </summary>
    public static IServiceCollection AddIntegrationEventConsumer<TEvent, TConsumer>(
        this IServiceCollection services,
        string subscription)
        where TEvent : IIntegrationEvent
        where TConsumer : class, IIntegrationEventConsumer<TEvent>
    {
        services.AddScoped<IIntegrationEventConsumer<TEvent>, TConsumer>();

        // Topic == event type name, matching the dispatcher's per-event-type destination convention.
        var eventName = typeof(TEvent).Name;
        GetOrCreateConsumerRegistry(services)
            .Add(new ConsumerRegistration(eventName, typeof(TEvent), eventName, subscription));

        return services;
    }

    /// <summary>
    /// Returns the one <see cref="ConsumerRegistry"/> instance registered on <paramref name="services"/>,
    /// creating and registering it on first use. Populated during registration and resolved by the hosts, so it
    /// must be a shared instance regardless of whether <see cref="AddSharedMessaging{TContext}"/> or
    /// <see cref="AddIntegrationEventConsumer{TEvent, TConsumer}"/> runs first.
    /// </summary>
    private static ConsumerRegistry GetOrCreateConsumerRegistry(IServiceCollection services)
    {
        var registry = (ConsumerRegistry?)services
            .FirstOrDefault(d => d.ServiceType == typeof(ConsumerRegistry))?.ImplementationInstance;

        if (registry is null)
        {
            registry = new ConsumerRegistry();
            services.AddSingleton(registry);
        }

        return registry;
    }

    /// <summary>
    /// Registers the <see cref="ICache"/> facade cache (<see cref="RedisCache"/>). Assumes the host has
    /// registered an <c>IDistributedCache</c> — in this stack via the Aspire <c>AddRedisDistributedCache</c>
    /// integration keyed to the AppHost <c>cache</c> resource. Only services that cache call this.
    /// </summary>
    public static IServiceCollection AddSharedCaching(this IServiceCollection services)
    {
        services.TryAddScoped<ICache, RedisCache>();
        return services;
    }

    /// <summary>
    /// Registers the <see cref="GlobalExceptionHandler"/> (and problem-details services). The host still
    /// activates it with <c>app.UseExceptionHandler()</c>.
    /// </summary>
    public static IServiceCollection AddSharedExceptionHandler(this IServiceCollection services)
    {
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();
        return services;
    }
}
