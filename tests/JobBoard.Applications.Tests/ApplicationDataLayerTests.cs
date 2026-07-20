using JobBoard.Applications.Core.Data;
using JobBoard.Applications.Core.Managers.Mappers;
using JobBoard.Applications.Core.Managers.Models.Domain;
using JobBoard.Applications.Tests.Fakes;
using JobBoard.Contracts;
using JobBoard.Shared.Errors;
using JobBoard.Shared.Messaging;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace JobBoard.Applications.Tests;

public sealed class ApplicationDataLayerTests
{
    // ---- Composition (hand fakes): the right calls, in the right order ----

    [Fact]
    public async Task SubmitAsync_AddsThenEnqueues_InsideATransaction()
    {
        var repository = new FakeApplicationRepository();
        var outbox = new FakeOutbox();
        var dataLayer = new ApplicationDataLayer(repository, outbox, new FakeInbox());

        var application = TestData.Application();
        var @event = application.ToApplicationSubmitted(default);

        await dataLayer.SubmitAsync(application, @event);

        Assert.Equal(["tx:begin", "add", "tx:commit"], repository.Calls);
        Assert.Same(@event, Assert.Single(outbox.Enqueued));
    }

    [Fact]
    public async Task SubmitAsync_MapsDuplicateApplicationViolation_ToConflict()
    {
        // Two concurrent submits inserted the same (CandidateId, JobId) — the unique index tripped.
        var duplicate = new DbUpdateException(
            "insert failed",
            new Exception("SQLite Error 19: 'UNIQUE constraint failed: Applications.CandidateId, Applications.JobId'."));
        var repository = new FakeApplicationRepository { AddError = duplicate };
        var dataLayer = new ApplicationDataLayer(repository, new FakeOutbox(), new FakeInbox());

        var application = TestData.Application();
        var ex = await Assert.ThrowsAsync<DomainException>(
            () => dataLayer.SubmitAsync(application, application.ToApplicationSubmitted(default)));

        Assert.Equal("application.duplicate", ex.Code);
        Assert.Equal(StatusCodes.Status409Conflict, ex.StatusCode);
    }

    [Fact]
    public async Task SubmitAsync_DoesNotMap_UnrelatedDbUpdateException()
    {
        var unrelated = new DbUpdateException("boom", new Exception("deadlock"));
        var repository = new FakeApplicationRepository { AddError = unrelated };
        var dataLayer = new ApplicationDataLayer(repository, new FakeOutbox(), new FakeInbox());

        var application = TestData.Application();
        await Assert.ThrowsAsync<DbUpdateException>(
            () => dataLayer.SubmitAsync(application, application.ToApplicationSubmitted(default)));
    }

    [Fact]
    public async Task WithdrawAsync_WhenActive_WithdrawsThenEnqueues_InsideATransaction()
    {
        var repository = new FakeApplicationRepository { WithdrawResult = true };
        var outbox = new FakeOutbox();
        var dataLayer = new ApplicationDataLayer(repository, outbox, new FakeInbox());

        var application = TestData.Application(status: ApplicationStatus.Submitted);
        var @event = application.ToStatusChanged(ApplicationStatus.Submitted, ApplicationStatus.Withdrawn, default);

        var withdrawn = await dataLayer.WithdrawAsync(application.Id, @event);

        Assert.True(withdrawn);
        Assert.Equal(["tx:begin", "withdraw", "tx:commit"], repository.Calls);
        Assert.Same(@event, Assert.Single(outbox.Enqueued));
    }

    [Fact]
    public async Task WithdrawAsync_WhenNotActive_EnqueuesNothing_AndReturnsFalse()
    {
        var repository = new FakeApplicationRepository { WithdrawResult = false };
        var outbox = new FakeOutbox();
        var dataLayer = new ApplicationDataLayer(repository, outbox, new FakeInbox());

        var application = TestData.Application(status: ApplicationStatus.Submitted);
        var @event = application.ToStatusChanged(ApplicationStatus.Submitted, ApplicationStatus.Withdrawn, default);

        var withdrawn = await dataLayer.WithdrawAsync(application.Id, @event);

        Assert.False(withdrawn);
        Assert.Equal(["tx:begin", "withdraw", "tx:commit"], repository.Calls);
        Assert.Empty(outbox.Enqueued);
    }

    [Fact]
    public async Task AdvanceAsync_WhenInExpectedStatus_AdvancesThenEnqueues()
    {
        var repository = new FakeApplicationRepository { AdvanceResult = true };
        var outbox = new FakeOutbox();
        var dataLayer = new ApplicationDataLayer(repository, outbox, new FakeInbox());

        var application = TestData.Application(status: ApplicationStatus.Submitted);
        var @event = application.ToStatusChanged(ApplicationStatus.Submitted, ApplicationStatus.Reviewed, default);

        var advanced = await dataLayer.AdvanceAsync(
            application.Id, ApplicationStatus.Submitted, ApplicationStatus.Reviewed, @event);

        Assert.True(advanced);
        Assert.Equal(["tx:begin", "advance", "tx:commit"], repository.Calls);
        Assert.Same(@event, Assert.Single(outbox.Enqueued));
    }

    [Fact]
    public async Task AdvanceAsync_WhenNotInExpectedStatus_EnqueuesNothing_AndReturnsFalse()
    {
        var repository = new FakeApplicationRepository { AdvanceResult = false };
        var outbox = new FakeOutbox();
        var dataLayer = new ApplicationDataLayer(repository, outbox, new FakeInbox());

        var application = TestData.Application(status: ApplicationStatus.Submitted);
        var @event = application.ToStatusChanged(ApplicationStatus.Submitted, ApplicationStatus.Reviewed, default);

        var advanced = await dataLayer.AdvanceAsync(
            application.Id, ApplicationStatus.Submitted, ApplicationStatus.Reviewed, @event);

        Assert.False(advanced);
        Assert.Empty(outbox.Enqueued);
    }

    [Fact]
    public async Task CloseOpenApplicationsForJobAsync_ClosesSnapshot_EnqueuesPerApp_AndMarksInbox()
    {
        var jobId = Guid.NewGuid();
        var active = new[]
        {
            TestData.Application(jobId: jobId, status: ApplicationStatus.Submitted),
            TestData.Application(jobId: jobId, status: ApplicationStatus.Reviewed),
        };
        var repository = new FakeApplicationRepository { ActiveResult = active, CloseActiveResult = 2 };
        var outbox = new FakeOutbox();
        var inbox = new FakeInbox { AlreadyProcessed = false };
        var dataLayer = new ApplicationDataLayer(repository, outbox, inbox);

        var messageId = Guid.NewGuid();
        var closed = await dataLayer.CloseOpenApplicationsForJobAsync(
            jobId, messageId, ApplicationStatus.Rejected,
            app => app.ToStatusChanged(app.Status, ApplicationStatus.Rejected, default));

        Assert.Equal(2, closed);
        // Snapshot, the authoritative close, then a read-back of what actually moved — all in the transaction.
        Assert.Equal(["tx:begin", "getActive", "closeActive", "closedIds", "tx:commit"], repository.Calls);
        Assert.Equal(2, outbox.Enqueued.Count);
        Assert.Equal(messageId, Assert.Single(inbox.Marked));
    }

    [Fact]
    public async Task CloseOpenApplicationsForJobAsync_PublishesEventOnly_ForRowsActuallyClosed()
    {
        // A row in the snapshot was transitioned concurrently (e.g. withdrawn) between the snapshot and the
        // conditional close, so the close skipped it — it must get no event.
        var jobId = Guid.NewGuid();
        var closedApp = TestData.Application(jobId: jobId, status: ApplicationStatus.Submitted);
        var racedApp = TestData.Application(jobId: jobId, status: ApplicationStatus.Offered);
        var repository = new FakeApplicationRepository
        {
            ActiveResult = [closedApp, racedApp],
            ClosedIds = new HashSet<Guid> { closedApp.Id },   // only this one actually reached Rejected
        };
        var outbox = new FakeOutbox();
        var dataLayer = new ApplicationDataLayer(repository, outbox, new FakeInbox());

        var closed = await dataLayer.CloseOpenApplicationsForJobAsync(
            jobId, Guid.NewGuid(), ApplicationStatus.Rejected,
            app => app.ToStatusChanged(app.Status, ApplicationStatus.Rejected, default));

        Assert.Equal(1, closed);
        var @event = Assert.IsType<ApplicationStatusChanged>(Assert.Single(outbox.Enqueued));
        Assert.Equal(closedApp.Id, @event.ApplicationId);   // never the raced row
    }

    [Fact]
    public async Task CloseOpenApplicationsForJobAsync_WhenAlreadyProcessed_IsANoOp()
    {
        var repository = new FakeApplicationRepository { ActiveResult = [TestData.Application()] };
        var outbox = new FakeOutbox();
        var inbox = new FakeInbox { AlreadyProcessed = true };
        var dataLayer = new ApplicationDataLayer(repository, outbox, inbox);

        var closed = await dataLayer.CloseOpenApplicationsForJobAsync(
            Guid.NewGuid(), Guid.NewGuid(), ApplicationStatus.Rejected,
            app => app.ToStatusChanged(app.Status, ApplicationStatus.Rejected, default));

        Assert.Equal(0, closed);
        // The inbox short-circuits before any read or write — no close, no events, no second inbox row.
        Assert.Equal(["tx:begin", "tx:commit"], repository.Calls);
        Assert.Empty(outbox.Enqueued);
        Assert.Empty(inbox.Marked);
    }

    // ---- Atomicity (real SQLite): the domain change and the outbox row are one unit ----

    [Fact]
    public async Task SubmitAsync_CommitsApplicationAndOutboxRow_Together()
    {
        using var harness = new ApplicationsSqliteHarness();
        var application = TestData.Application(status: ApplicationStatus.Submitted);

        await using (var context = harness.CreateContext())
        {
            var dataLayer = new ApplicationDataLayer(
                new ApplicationRepository(context), new Outbox(context), new Inbox(context));
            await dataLayer.SubmitAsync(application, application.ToApplicationSubmitted(default));
        }

        await using var assert = harness.CreateContext();
        Assert.Equal(application.Id, (await assert.Applications.SingleAsync()).Id);
        var row = await assert.OutboxMessages.SingleAsync();
        Assert.Equal(nameof(ApplicationSubmitted), row.Type);
        Assert.Equal(nameof(ApplicationSubmitted), row.Destination);
        Assert.Null(row.ProcessedOnUtc);
    }

    [Fact]
    public async Task WithdrawAsync_LeavesNeitherStatusNorOutbox_WhenEnqueueThrows()
    {
        using var harness = new ApplicationsSqliteHarness();
        var application = TestData.Application(status: ApplicationStatus.Submitted);

        await using (var seed = harness.CreateContext())
        {
            seed.Applications.Add(application);
            await seed.SaveChangesAsync();
        }

        await using (var context = harness.CreateContext())
        {
            var repository = new ApplicationRepository(context);
            // The conditional UPDATE flips the row inside the transaction, then the outbox write throws —
            // the status change must roll back with it.
            var dataLayer = new ApplicationDataLayer(repository, new FakeOutbox { ThrowOnEnqueue = true }, new Inbox(context));
            var @event = application.ToStatusChanged(ApplicationStatus.Submitted, ApplicationStatus.Withdrawn, default);
            await Assert.ThrowsAsync<InvalidOperationException>(() => dataLayer.WithdrawAsync(application.Id, @event));
        }

        await using var assert = harness.CreateContext();
        Assert.Equal(ApplicationStatus.Submitted, (await assert.Applications.SingleAsync()).Status);
        Assert.Empty(await assert.OutboxMessages.ToListAsync());
    }

    [Fact]
    public async Task CloseOpenApplicationsForJobAsync_ClosesActive_LeavesTerminal_AndIsIdempotentOnRedelivery()
    {
        using var harness = new ApplicationsSqliteHarness();
        var jobId = Guid.NewGuid();
        var otherJobId = Guid.NewGuid();

        var submitted = TestData.Application(jobId: jobId, status: ApplicationStatus.Submitted);
        var offered = TestData.Application(jobId: jobId, status: ApplicationStatus.Offered);
        var alreadyRejected = TestData.Application(jobId: jobId, status: ApplicationStatus.Rejected);
        var otherJob = TestData.Application(jobId: otherJobId, status: ApplicationStatus.Submitted);

        await using (var seed = harness.CreateContext())
        {
            seed.Applications.AddRange(submitted, offered, alreadyRejected, otherJob);
            await seed.SaveChangesAsync();
        }

        var messageId = Guid.NewGuid();

        Func<ApplicationsDbContext, Task<int>> deliver = context =>
        {
            var dataLayer = new ApplicationDataLayer(
                new ApplicationRepository(context), new Outbox(context), new Inbox(context));
            return dataLayer.CloseOpenApplicationsForJobAsync(
                jobId, messageId, ApplicationStatus.Rejected,
                app => app.ToStatusChanged(app.Status, ApplicationStatus.Rejected, default));
        };

        int firstClosed;
        await using (var context = harness.CreateContext())
        {
            firstClosed = await deliver(context);
        }

        // Redeliver the same JobClosed message — the inbox must make it a no-op.
        int secondClosed;
        await using (var context = harness.CreateContext())
        {
            secondClosed = await deliver(context);
        }

        Assert.Equal(2, firstClosed);   // submitted + offered
        Assert.Equal(0, secondClosed);  // redelivery closed nothing

        await using var assert = harness.CreateContext();
        Assert.Equal(ApplicationStatus.Rejected, (await assert.Applications.FindAsync(submitted.Id))!.Status);
        Assert.Equal(ApplicationStatus.Rejected, (await assert.Applications.FindAsync(offered.Id))!.Status);
        Assert.Equal(ApplicationStatus.Rejected, (await assert.Applications.FindAsync(alreadyRejected.Id))!.Status);
        Assert.Equal(ApplicationStatus.Submitted, (await assert.Applications.FindAsync(otherJob.Id))!.Status);

        // Exactly two status-changed events (one per closed app) — the redelivery added none.
        Assert.Equal(2, await assert.OutboxMessages.CountAsync());
        Assert.All(await assert.OutboxMessages.ToListAsync(), r => Assert.Equal(nameof(ApplicationStatusChanged), r.Type));
        // And exactly one inbox row for the message.
        Assert.Equal(messageId, (await assert.InboxMessages.SingleAsync()).MessageId);
    }
}
