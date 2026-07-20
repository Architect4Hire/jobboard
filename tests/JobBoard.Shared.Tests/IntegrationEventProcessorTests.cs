using System.Text.Json;
using Azure.Messaging.ServiceBus;
using JobBoard.Shared.Messaging;
using JobBoard.Shared.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace JobBoard.Shared.Tests;

public sealed class IntegrationEventProcessorTests
{
    [Fact]
    public async Task ProcessAsync_AppliesOnce_OnDuplicateMessageId()
    {
        // A shared in-memory SQLite connection so state survives across the per-message scopes the processor opens.
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var services = new ServiceCollection();
        services.AddDbContext<TestDbContext>(options => options.UseSqlite(connection));
        services.AddScoped<BaseDbContext>(sp => sp.GetRequiredService<TestDbContext>());
        services.AddScoped<IInbox, Inbox>();
        services.AddSingleton<CallCounter>();
        services.AddScoped<IIntegrationEventConsumer<FakeEvent>, RecordingConsumer>();
        await using var provider = services.BuildServiceProvider();

        await using (var scope = provider.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<TestDbContext>().Database.EnsureCreatedAsync();
        }

        var registry = new ConsumerRegistry();
        registry.Add(new ConsumerRegistration(nameof(FakeEvent), typeof(FakeEvent), nameof(FakeEvent), "test"));

        var processor = new IntegrationEventProcessor(
            provider.GetRequiredService<IServiceScopeFactory>(),
            registry,
            NullLogger<IntegrationEventProcessor>.Instance);

        var @event = new FakeEvent(Guid.NewGuid(), "hello");
        var message = BuildMessage(@event);

        // First delivery applies the side effect; the redelivery (same MessageId) is deduped by the inbox.
        await processor.ProcessAsync(message);
        await processor.ProcessAsync(message);

        Assert.Equal(1, provider.GetRequiredService<CallCounter>().Count);

        await using var assertScope = provider.CreateAsyncScope();
        var context = assertScope.ServiceProvider.GetRequiredService<TestDbContext>();
        Assert.Equal(1, await context.InboxMessages.CountAsync());
    }

    private static ServiceBusReceivedMessage BuildMessage(FakeEvent @event)
    {
        var json = JsonSerializer.Serialize(@event, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        // Mirror what the dispatcher sends: body = JSON payload, MessageId = event id, Subject = event type name.
        return ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString(json),
            messageId: @event.Id.ToString(),
            subject: nameof(FakeEvent));
    }
}
