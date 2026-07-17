using JobBoard.Shared.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobBoard.Shared.Messaging;

/// <summary>
/// The background loop that drives <see cref="OutboxRelay"/>. On each tick it opens a DI scope, resolves the
/// service's scoped <see cref="BaseDbContext"/>, and relays that service's pending outbox rows. Registered once
/// per host by <c>AddSharedMessaging&lt;TDbContext&gt;()</c>.
/// </summary>
public sealed class OutboxDispatcher : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OutboxRelay _relay;
    private readonly OutboxRelayOptions _options;
    private readonly ILogger<OutboxDispatcher> _logger;

    public OutboxDispatcher(
        IServiceScopeFactory scopeFactory,
        OutboxRelay relay,
        IOptions<OutboxRelayOptions> options,
        ILogger<OutboxDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _relay = relay;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_options.PollInterval);

        try
        {
            // Relay once immediately, then on every tick, until the host stops. WaitForNextTickAsync throws
            // OperationCanceledException on shutdown, so the whole loop sits inside the cancellation guard.
            do
            {
                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var context = scope.ServiceProvider.GetRequiredService<BaseDbContext>();
                    await _relay.RelayAsync(context, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // A poll failure (e.g. the DB is briefly unavailable) must not tear the loop down; the next
                    // tick retries. Unstamped rows are picked up again.
                    _logger.LogError(ex, "Outbox relay poll failed; retrying on the next tick.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }
}
