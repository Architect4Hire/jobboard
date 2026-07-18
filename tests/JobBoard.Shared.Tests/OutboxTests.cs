using JobBoard.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace JobBoard.Shared.Tests;

public sealed class OutboxTests
{
    [Fact]
    public async Task EnqueueAsync_WritesRow_KeyedByEventId()
    {
        using var harness = new SqliteHarness();
        var @event = new FakeEvent(Guid.NewGuid(), "hello");

        await using (var context = harness.CreateContext())
        {
            var outbox = new Outbox(context);
            await outbox.EnqueueAsync(@event);
            await context.SaveChangesAsync();
        }

        await using var assertContext = harness.CreateContext();
        var row = await assertContext.OutboxMessages.SingleAsync();

        Assert.Equal(@event.Id, row.Id);
        Assert.Equal(nameof(FakeEvent), row.Type);
        Assert.Equal(nameof(FakeEvent), row.Destination);
        Assert.Contains("hello", row.Payload);
        Assert.Null(row.ProcessedOnUtc);
        Assert.NotEqual(default, row.OccurredOnUtc);
    }

    [Fact]
    public async Task EnqueueAsync_SameEventTwice_WritesOneRow()
    {
        using var harness = new SqliteHarness();
        var @event = new FakeEvent(Guid.NewGuid(), "hello");

        await using (var context = harness.CreateContext())
        {
            await new Outbox(context).EnqueueAsync(@event);
            await context.SaveChangesAsync();
        }

        // A second enqueue of the same deterministic id (e.g. a replayed operation) must not duplicate.
        await using (var context = harness.CreateContext())
        {
            await new Outbox(context).EnqueueAsync(@event);
            await context.SaveChangesAsync();
        }

        await using var assertContext = harness.CreateContext();
        Assert.Equal(1, await assertContext.OutboxMessages.CountAsync());
    }
}
