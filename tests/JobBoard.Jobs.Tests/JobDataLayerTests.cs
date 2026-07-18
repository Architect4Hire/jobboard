using JobBoard.Jobs.Core.Data;
using JobBoard.Jobs.Core.Managers.Mappers;
using JobBoard.Jobs.Core.Managers.Models.Domain;
using JobBoard.Jobs.Tests.Fakes;
using JobBoard.Shared.Errors;
using JobBoard.Shared.Messaging;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace JobBoard.Jobs.Tests;

public sealed class JobDataLayerTests
{
    // ---- Composition (hand fakes): the right calls, in the right order ----

    [Fact]
    public async Task CloseAsync_ClosesThenEnqueues_InsideATransaction()
    {
        var repository = new FakeJobRepository { CloseIfOpenResult = true };
        var outbox = new FakeOutbox();
        var dataLayer = new JobDataLayer(repository, outbox);

        var @event = TestData.Job().ToJobClosed();

        var didClose = await dataLayer.CloseAsync(@event.JobId, @event);

        Assert.True(didClose);
        // Close happens before enqueue, both inside the transaction.
        Assert.Equal(["tx:begin", "close", "tx:commit"], repository.Calls);
        Assert.Same(@event, Assert.Single(outbox.Enqueued));
    }

    [Fact]
    public async Task CloseAsync_WhenNotOpen_EnqueuesNothing_AndReturnsFalse()
    {
        var repository = new FakeJobRepository { CloseIfOpenResult = false };
        var outbox = new FakeOutbox();
        var dataLayer = new JobDataLayer(repository, outbox);

        var @event = TestData.Job().ToJobClosed();

        var didClose = await dataLayer.CloseAsync(@event.JobId, @event);

        Assert.False(didClose);
        Assert.Equal(["tx:begin", "close", "tx:commit"], repository.Calls);
        Assert.Empty(outbox.Enqueued);
    }

    [Fact]
    public async Task AddAsync_RunsInTransaction_AndEnqueuesJobPosted()
    {
        var repository = new FakeJobRepository();
        var outbox = new FakeOutbox();
        var dataLayer = new JobDataLayer(repository, outbox);

        var job = TestData.Job();
        var posted = job.ToJobPosted();

        await dataLayer.AddAsync(job, posted);

        // Insert then enqueue, both inside the transaction; the JobPosted event ships iff the row commits.
        Assert.Equal(["tx:begin", "add", "tx:commit"], repository.Calls);
        Assert.Same(posted, Assert.Single(outbox.Enqueued));
    }

    [Fact]
    public async Task AddAsync_MapsDuplicateSlugViolation_ToConflict()
    {
        // The reconcile INSERT lost the race to a concurrent post with the same brand-new slug.
        var slugViolation = new DbUpdateException(
            "insert failed", new Exception("SQLite Error 19: 'UNIQUE constraint failed: Categories.Slug'."));
        var repository = new FakeJobRepository { AddError = slugViolation };
        var dataLayer = new JobDataLayer(repository, new FakeOutbox());

        var job = TestData.Job();
        var ex = await Assert.ThrowsAsync<DomainException>(() => dataLayer.AddAsync(job, job.ToJobPosted()));

        Assert.Equal("job.classification_conflict", ex.Code);
        Assert.Equal(StatusCodes.Status409Conflict, ex.StatusCode);
    }

    [Fact]
    public async Task AddAsync_DoesNotMap_UnrelatedDbUpdateException()
    {
        var unrelated = new DbUpdateException("boom", new Exception("deadlock"));
        var repository = new FakeJobRepository { AddError = unrelated };
        var dataLayer = new JobDataLayer(repository, new FakeOutbox());

        // Not a slug conflict — it must surface as-is (→ the global handler's 500), not a 409.
        var job = TestData.Job();
        await Assert.ThrowsAsync<DbUpdateException>(() => dataLayer.AddAsync(job, job.ToJobPosted()));
    }

    // ---- Atomicity (real SQLite): the status change and the outbox row are one unit ----

    [Fact]
    public async Task AddAsync_LeavesNoJobAndNoOutboxRow_WhenEnqueueThrows()
    {
        using var harness = new JobsSqliteHarness();
        var job = TestData.Job();

        await using (var context = harness.CreateContext())
        {
            var repository = new JobRepository(context);

            // The insert stages inside the transaction, then the JobPosted outbox write throws — the
            // job (and its classifications) must roll back with it, leaving nothing committed.
            var dataLayer = new JobDataLayer(repository, new FakeOutbox { ThrowOnEnqueue = true });
            await Assert.ThrowsAsync<InvalidOperationException>(() => dataLayer.AddAsync(job, job.ToJobPosted()));
        }

        await using var assert = harness.CreateContext();
        Assert.Empty(await assert.Jobs.ToListAsync());
        Assert.Empty(await assert.OutboxMessages.ToListAsync());
    }

    [Fact]
    public async Task CloseAsync_CommitsStatusChangeAndOutboxRow_Together()
    {
        using var harness = new JobsSqliteHarness();
        var job = TestData.Job(status: JobStatus.Open);

        await using (var seed = harness.CreateContext())
        {
            seed.Jobs.Add(job);
            await seed.SaveChangesAsync();
        }

        await using (var context = harness.CreateContext())
        {
            var repository = new JobRepository(context);
            var @event = job.ToJobClosed();

            var dataLayer = new JobDataLayer(repository, new Outbox(context));
            var didClose = await dataLayer.CloseAsync(job.Id, @event);

            Assert.True(didClose);
        }

        await using var assert = harness.CreateContext();
        Assert.Equal(JobStatus.Closed, (await assert.Jobs.SingleAsync()).Status);
        var row = await assert.OutboxMessages.SingleAsync();
        Assert.Equal(nameof(JobBoard.Contracts.JobClosed), row.Type);
        Assert.Equal(nameof(JobBoard.Contracts.JobClosed), row.Destination);
    }

    [Fact]
    public async Task CloseAsync_OnAlreadyClosedJob_ReturnsFalse_AndEnqueuesNothing()
    {
        using var harness = new JobsSqliteHarness();
        var job = TestData.Job(status: JobStatus.Closed);

        await using (var seed = harness.CreateContext())
        {
            seed.Jobs.Add(job);
            await seed.SaveChangesAsync();
        }

        await using (var context = harness.CreateContext())
        {
            var repository = new JobRepository(context);
            var dataLayer = new JobDataLayer(repository, new Outbox(context));

            var didClose = await dataLayer.CloseAsync(job.Id, job.ToJobClosed());

            Assert.False(didClose);
        }

        await using var assert = harness.CreateContext();
        Assert.Equal(JobStatus.Closed, (await assert.Jobs.SingleAsync()).Status);
        Assert.Empty(await assert.OutboxMessages.ToListAsync());
    }

    [Fact]
    public async Task CloseAsync_LeavesNeitherStatusNorOutbox_WhenEnqueueThrows()
    {
        using var harness = new JobsSqliteHarness();
        var job = TestData.Job(status: JobStatus.Open);

        await using (var seed = harness.CreateContext())
        {
            seed.Jobs.Add(job);
            await seed.SaveChangesAsync();
        }

        await using (var context = harness.CreateContext())
        {
            var repository = new JobRepository(context);

            // The conditional UPDATE flips the row inside the transaction, then the outbox write throws —
            // the status change must roll back with it.
            var dataLayer = new JobDataLayer(repository, new FakeOutbox { ThrowOnEnqueue = true });
            await Assert.ThrowsAsync<InvalidOperationException>(() => dataLayer.CloseAsync(job.Id, job.ToJobClosed()));
        }

        await using var assert = harness.CreateContext();
        Assert.Equal(JobStatus.Open, (await assert.Jobs.SingleAsync()).Status);
        Assert.Empty(await assert.OutboxMessages.ToListAsync());
    }
}
