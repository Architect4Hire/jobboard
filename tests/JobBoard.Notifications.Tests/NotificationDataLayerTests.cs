using JobBoard.Notifications.Core.Data;
using JobBoard.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace JobBoard.Notifications.Tests;

/// <summary>
/// Data-layer idempotency over a real (SQLite) context + the shared <see cref="Inbox"/>: the first record
/// writes the log and stamps the inbox; a redelivery of the same message id is a no-op (no duplicate),
/// while a distinct message id writes its own row.
/// </summary>
public sealed class NotificationDataLayerTests
{
    private static NotificationDataLayer CreateDataLayer(NotificationsDbContext context) =>
        new(new NotificationRepository(context), new Inbox(context));

    [Fact]
    public async Task RecordAsync_WritesLog_AndDedupesRedelivery()
    {
        using var harness = new NotificationsSqliteHarness();
        var messageId = Guid.NewGuid();

        // First delivery.
        await using (var context = harness.CreateContext())
        {
            await CreateDataLayer(context).RecordAsync(TestData.NotificationLog(), messageId);
        }

        // Redelivery — same message id, a different log payload — must be a no-op.
        await using (var context = harness.CreateContext())
        {
            await CreateDataLayer(context).RecordAsync(TestData.NotificationLog(), messageId);
        }

        await using var assert = harness.CreateContext();
        Assert.Single(await assert.NotificationLogs.ToListAsync()); // exactly one — the inbox blocked the replay
    }

    [Fact]
    public async Task RecordAsync_WhenLogInsertFails_LeavesNoLogAndNoInboxRow()
    {
        using var harness = new NotificationsSqliteHarness();
        var duplicateId = Guid.NewGuid();
        var messageId = Guid.NewGuid();

        // Seed a log; a second insert with the same primary key fails the transaction mid-operation.
        await using (var seed = harness.CreateContext())
        {
            seed.NotificationLogs.Add(TestData.NotificationLog(id: duplicateId));
            await seed.SaveChangesAsync();
        }

        await using (var context = harness.CreateContext())
        {
            await Assert.ThrowsAsync<DbUpdateException>(
                () => CreateDataLayer(context).RecordAsync(TestData.NotificationLog(id: duplicateId), messageId));
        }

        await using var assert = harness.CreateContext();
        Assert.Single(await assert.NotificationLogs.ToListAsync());                       // only the seed row
        Assert.False(await assert.InboxMessages.AnyAsync(i => i.MessageId == messageId)); // inbox stamp rolled back too
    }

    [Fact]
    public async Task RecordAsync_DistinctMessages_WriteDistinctLogs()
    {
        using var harness = new NotificationsSqliteHarness();

        await using (var context = harness.CreateContext())
        {
            await CreateDataLayer(context).RecordAsync(TestData.NotificationLog(), Guid.NewGuid());
        }

        await using (var context = harness.CreateContext())
        {
            await CreateDataLayer(context).RecordAsync(TestData.NotificationLog(), Guid.NewGuid());
        }

        await using var assert = harness.CreateContext();
        Assert.Equal(2, await assert.NotificationLogs.CountAsync());
    }
}
