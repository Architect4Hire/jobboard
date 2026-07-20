using JobBoard.Audit.Core.Data;
using JobBoard.Audit.Core.Managers.Models.Domain;
using JobBoard.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace JobBoard.Audit.Tests;

/// <summary>
/// Data-layer append + idempotency over a real (SQLite) context + the shared <see cref="Inbox"/>: the
/// first append writes the row and stamps the inbox; a redelivery of the same message id is a no-op (no
/// duplicate), a mid-operation failure rolls the inbox stamp back too, and distinct messages write
/// distinct rows.
/// </summary>
public sealed class AuditDataLayerTests
{
    private static AuditDataLayer CreateDataLayer(AuditDbContext context) =>
        new(new AuditRepository(context), new Inbox(context));

    private static AuditEntry Entry(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        EventType = "JobPosted",
        CorrelationId = Guid.NewGuid(),
        CausationId = Guid.NewGuid(),
        ActorId = Guid.NewGuid(),
        SubjectId = Guid.NewGuid(),
        OccurredOnUtc = DateTime.UtcNow,
        Payload = "{}",
    };

    [Fact]
    public async Task AppendAsync_WritesRow_AndDedupesRedelivery()
    {
        using var harness = new AuditSqliteHarness();
        var messageId = Guid.NewGuid();

        // First delivery.
        await using (var context = harness.CreateContext())
        {
            await CreateDataLayer(context).AppendAsync(Entry(messageId), messageId);
        }

        // Redelivery — same message id — must be a no-op.
        await using (var context = harness.CreateContext())
        {
            await CreateDataLayer(context).AppendAsync(Entry(messageId), messageId);
        }

        await using var assert = harness.CreateContext();
        Assert.Single(await assert.AuditEntries.ToListAsync()); // exactly one — the inbox blocked the replay
    }

    [Fact]
    public async Task AppendAsync_WhenRowInsertFails_LeavesNoRowAndNoInboxStamp()
    {
        using var harness = new AuditSqliteHarness();
        var duplicateId = Guid.NewGuid();
        var messageId = Guid.NewGuid();

        // Seed a row; a second append with the same primary key fails the transaction mid-operation.
        await using (var seed = harness.CreateContext())
        {
            seed.AuditEntries.Add(Entry(duplicateId));
            await seed.SaveChangesAsync();
        }

        await using (var context = harness.CreateContext())
        {
            await Assert.ThrowsAsync<DbUpdateException>(
                () => CreateDataLayer(context).AppendAsync(Entry(duplicateId), messageId));
        }

        await using var assert = harness.CreateContext();
        Assert.Single(await assert.AuditEntries.ToListAsync());                           // only the seed row
        Assert.False(await assert.InboxMessages.AnyAsync(i => i.MessageId == messageId)); // inbox stamp rolled back too
    }

    [Fact]
    public async Task AppendAsync_DistinctMessages_WriteDistinctRows()
    {
        using var harness = new AuditSqliteHarness();

        await using (var context = harness.CreateContext())
        {
            var id = Guid.NewGuid();
            await CreateDataLayer(context).AppendAsync(Entry(id), id);
        }

        await using (var context = harness.CreateContext())
        {
            var id = Guid.NewGuid();
            await CreateDataLayer(context).AppendAsync(Entry(id), id);
        }

        await using var assert = harness.CreateContext();
        Assert.Equal(2, await assert.AuditEntries.CountAsync());
    }
}
