using JobBoard.Shared.Messaging;
using JobBoard.Shared.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace JobBoard.Shared.Tests;

public sealed class OutboxDispatcherLifecycleTests
{
    [Fact]
    public async Task Dispatcher_RelaysPendingRows_ThenShutsDownCleanly()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var services = new ServiceCollection();
        services.AddDbContext<TestDbContext>(options => options.UseSqlite(connection));
        services.AddScoped<BaseDbContext>(sp => sp.GetRequiredService<TestDbContext>());
        await using var provider = services.BuildServiceProvider();

        var @event = new FakeEvent(Guid.NewGuid(), "hello");
        await using (var scope = provider.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            await context.Database.EnsureCreatedAsync();
            await new Outbox(context).EnqueueAsync(@event);
            await context.SaveChangesAsync();
        }

        var sender = new FakeServiceBusSender();
        var relay = new OutboxRelay(
            new FakeServiceBusClient(_ => sender),
            Options.Create(new OutboxRelayOptions { PollInterval = TimeSpan.FromMilliseconds(20) }),
            NullLogger<OutboxRelay>.Instance);

        var dispatcher = new OutboxDispatcher(
            provider.GetRequiredService<IServiceScopeFactory>(),
            relay,
            Options.Create(new OutboxRelayOptions { PollInterval = TimeSpan.FromMilliseconds(20) }),
            NullLogger<OutboxDispatcher>.Instance);

        await dispatcher.StartAsync(CancellationToken.None);

        // Wait (bounded) for the loop to relay the seeded row.
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (sender.Sent.Count == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        await dispatcher.StopAsync(CancellationToken.None);

        Assert.Single(sender.Sent);
        Assert.Equal(@event.Id.ToString(), sender.Sent[0].MessageId);

        // The background loop must end on cancellation, not fault with OperationCanceledException.
        Assert.NotNull(dispatcher.ExecuteTask);
        Assert.True(dispatcher.ExecuteTask!.IsCompletedSuccessfully);
    }
}
