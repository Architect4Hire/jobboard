using JobBoard.Notifications.Core.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace JobBoard.Notifications.Tests;

public sealed class NotificationRepositoryTests
{
    [Fact]
    public async Task AddAsync_StagesLog_ThatPersistsOnSave()
    {
        using var harness = new NotificationsSqliteHarness();
        var log = TestData.NotificationLog();

        await using (var context = harness.CreateContext())
        {
            await new NotificationRepository(context).AddAsync(log);
            await context.SaveChangesAsync();
        }

        await using var assert = harness.CreateContext();
        Assert.Equal(log.Id, (await assert.NotificationLogs.SingleAsync()).Id);
    }
}
