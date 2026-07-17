using JobBoard.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace JobBoard.Shared.Tests;

public sealed class BaseRepositoryTests
{
    [Fact]
    public async Task ExecuteInTransactionAsync_RollsBackEverything_WhenOperationThrows()
    {
        using var harness = new SqliteHarness();

        await using (var context = harness.CreateContext())
        {
            var repository = new TestRepository(context);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                repository.ExecuteInTransactionAsync(async token =>
                {
                    context.OutboxMessages.Add(NewRow());
                    // Persist within the transaction, then fail: proves rollback, not just "never saved".
                    await context.SaveChangesAsync(token);
                    throw new InvalidOperationException("boom");
                }));
        }

        await using var assertContext = harness.CreateContext();
        Assert.Equal(0, await assertContext.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_CommitsStagedChanges_OnSuccess()
    {
        using var harness = new SqliteHarness();

        await using (var context = harness.CreateContext())
        {
            var repository = new TestRepository(context);

            await repository.ExecuteInTransactionAsync(token =>
            {
                // Only staged — the transaction wrapper flushes and commits it.
                context.OutboxMessages.Add(NewRow());
                return Task.CompletedTask;
            });
        }

        await using var assertContext = harness.CreateContext();
        Assert.Equal(1, await assertContext.OutboxMessages.CountAsync());
    }

    private static OutboxMessage NewRow() => new()
    {
        Id = Guid.NewGuid(),
        Type = "TestEvent",
        Destination = "TestEvent",
        Payload = "{}",
        OccurredOnUtc = DateTime.UtcNow,
    };
}
