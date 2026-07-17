using JobBoard.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace JobBoard.Shared.Tests;

public sealed class OutboxDispatcherTests
{
    private static OutboxRelay CreateRelay(FakeServiceBusClient client) =>
        new(client, Options.Create(new OutboxRelayOptions()), NullLogger<OutboxRelay>.Instance);

    [Fact]
    public async Task RelayAsync_SendsUnprocessedRows_AndStampsThem()
    {
        using var harness = new SqliteHarness();
        var first = new FakeEvent(Guid.NewGuid(), "one");
        var second = new FakeEvent(Guid.NewGuid(), "two");

        await using (var context = harness.CreateContext())
        {
            await new Outbox(context).EnqueueAsync(first);
            await new Outbox(context).EnqueueAsync(second);
            await context.SaveChangesAsync();
        }

        var sender = new FakeServiceBusSender();
        var relay = CreateRelay(new FakeServiceBusClient(_ => sender));

        await using (var context = harness.CreateContext())
        {
            await relay.RelayAsync(context);
        }

        // Each row went out as a ServiceBusMessage keyed by the event id and subjected with the type name.
        Assert.Equal(2, sender.Sent.Count);
        Assert.Contains(sender.Sent, m => m.MessageId == first.Id.ToString() && m.Subject == nameof(FakeEvent));
        Assert.Contains(sender.Sent, m => m.MessageId == second.Id.ToString() && m.Subject == nameof(FakeEvent));

        // And every relayed row is now stamped, so a later poll skips it.
        await using var assertContext = harness.CreateContext();
        Assert.False(await assertContext.OutboxMessages.AnyAsync(m => m.ProcessedOnUtc == null));
    }

    [Fact]
    public async Task RelayAsync_LeavesRowUnprocessed_WhenSendFails()
    {
        using var harness = new SqliteHarness();
        var @event = new FakeEvent(Guid.NewGuid(), "boom");

        await using (var context = harness.CreateContext())
        {
            await new Outbox(context).EnqueueAsync(@event);
            await context.SaveChangesAsync();
        }

        var sender = new FakeServiceBusSender(throwOnSend: true);
        var relay = CreateRelay(new FakeServiceBusClient(_ => sender));

        await using (var context = harness.CreateContext())
        {
            // The relay swallows the send failure and returns; nothing is stamped.
            await relay.RelayAsync(context);
        }

        Assert.Empty(sender.Sent);

        await using var assertContext = harness.CreateContext();
        var row = await assertContext.OutboxMessages.SingleAsync();
        Assert.Null(row.ProcessedOnUtc);
    }
}
