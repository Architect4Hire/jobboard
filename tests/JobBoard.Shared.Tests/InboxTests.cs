using JobBoard.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace JobBoard.Shared.Tests;

public sealed class InboxTests
{
    [Fact]
    public async Task HasProcessedAsync_IsFalse_BeforeMarking_AndTrue_After()
    {
        using var harness = new SqliteHarness();
        var messageId = Guid.NewGuid();

        await using (var context = harness.CreateContext())
        {
            Assert.False(await new Inbox(context).HasProcessedAsync(messageId));

            await new Inbox(context).MarkProcessedAsync(messageId);
            await context.SaveChangesAsync();
        }

        await using var assertContext = harness.CreateContext();
        Assert.True(await new Inbox(assertContext).HasProcessedAsync(messageId));
    }

    [Fact]
    public async Task MarkProcessedAsync_SameMessageTwice_WritesOneRow()
    {
        using var harness = new SqliteHarness();
        var messageId = Guid.NewGuid();

        await using (var context = harness.CreateContext())
        {
            await new Inbox(context).MarkProcessedAsync(messageId);
            await context.SaveChangesAsync();
        }

        await using (var context = harness.CreateContext())
        {
            await new Inbox(context).MarkProcessedAsync(messageId);
            await context.SaveChangesAsync();
        }

        await using var assertContext = harness.CreateContext();
        Assert.Equal(1, await assertContext.InboxMessages.CountAsync());
    }
}
