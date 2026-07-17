using JobBoard.Shared.Errors;
using JobBoard.Shared.Messaging;
using JobBoard.Shared.Persistence;

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
        services.AddScoped<IOutbox, Outbox>();
        services.AddScoped<IInbox, Inbox>();
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
        services.AddScoped<BaseDbContext>(sp => sp.GetRequiredService<TContext>());
        return services.AddSharedPersistence();
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
