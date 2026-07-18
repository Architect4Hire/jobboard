using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JobBoard.Shared.Messaging;

/// <summary>
/// The receive-side host: opens one <see cref="ServiceBusProcessor"/> per registered <c>(topic, subscription)</c>
/// and hands each delivered message to <see cref="IntegrationEventProcessor"/>. Auto-complete is off — a message
/// is completed only after its consumer succeeds; a throw leaves it unsettled so Service Bus redelivers it (the
/// consumer's inbox makes that safe). With no consumers registered it opens nothing and stays dormant.
/// </summary>
public sealed class ServiceBusProcessorHost : BackgroundService
{
    private readonly ServiceBusClient _client;
    private readonly ConsumerRegistry _registry;
    private readonly IntegrationEventProcessor _processor;
    private readonly ILogger<ServiceBusProcessorHost> _logger;

    private readonly List<ServiceBusProcessor> _processors = [];

    public ServiceBusProcessorHost(
        ServiceBusClient client,
        ConsumerRegistry registry,
        IntegrationEventProcessor processor,
        ILogger<ServiceBusProcessorHost> logger)
    {
        _client = client;
        _registry = registry;
        _processor = processor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        foreach (var (topic, subscription) in _registry.Subscriptions)
        {
            try
            {
                var processor = _client.CreateProcessor(topic, subscription, new ServiceBusProcessorOptions
                {
                    AutoCompleteMessages = false,
                });

                processor.ProcessMessageAsync += async args =>
                {
                    await _processor.ProcessAsync(args.Message, args.CancellationToken);
                    await args.CompleteMessageAsync(args.Message, args.CancellationToken);
                };

                processor.ProcessErrorAsync += args =>
                {
                    _logger.LogError(args.Exception, "Service Bus processor error on {Topic}/{Subscription} ({Source}).",
                        topic, subscription, args.ErrorSource);
                    return Task.CompletedTask;
                };

                _processors.Add(processor);
                await processor.StartProcessingAsync(stoppingToken);
                _logger.LogInformation("Listening on {Topic}/{Subscription}.", topic, subscription);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One subscription failing to open must not stop the host or the other subscriptions.
                _logger.LogError(ex, "Failed to start processor on {Topic}/{Subscription}.", topic, subscription);
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var processor in _processors)
        {
            await processor.StopProcessingAsync(cancellationToken);
            await processor.DisposeAsync();
        }

        _processors.Clear();
        await base.StopAsync(cancellationToken);
    }
}
