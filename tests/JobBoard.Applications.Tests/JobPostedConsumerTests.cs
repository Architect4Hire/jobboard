using JobBoard.Applications.Consumers;
using JobBoard.Applications.Core.Business;
using JobBoard.Applications.Core.Data;
using JobBoard.Applications.Core.Facade;
using JobBoard.Applications.Core.Managers.Validators;
using JobBoard.Contracts;
using JobBoard.Shared.Messaging;
using JobBoard.Shared.Requests;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace JobBoard.Applications.Tests;

/// <summary>
/// The read-model seam from the consuming side: a <c>JobPosted</c> event mirrors the job's title and
/// employer into this service's own <c>JobReference</c> projection (ADR-0012 option B) — exactly once,
/// even when the message is redelivered (Service Bus is at-least-once). The whole real stack runs
/// (consumer → facade → business → data → repository + inbox) over SQLite, one fresh context per delivery.
/// </summary>
public sealed class JobPostedConsumerTests
{
    private static JobPostedConsumer BuildConsumer(ApplicationsDbContext context)
    {
        var dataLayer = new ApplicationDataLayer(
            new ApplicationRepository(context), new Outbox(context), new Inbox(context));
        var business = new ApplicationBusiness(dataLayer, new AmbientRequestContext());
        var facade = new ApplicationFacade(
            business, new SubmitApplicationViewModelValidator(), new AdvanceApplicationStatusViewModelValidator());
        return new JobPostedConsumer(facade);
    }

    [Fact]
    public async Task ConsumeAsync_UpsertsJobReference_ExactlyOnce_OnRedelivery()
    {
        using var harness = new ApplicationsSqliteHarness();
        var jobId = Guid.NewGuid();
        var employerId = Guid.NewGuid();

        var jobPosted = new JobPosted(Guid.NewGuid(), jobId, employerId, "Senior Engineer", "Remote", DateTime.UtcNow);

        // Deliver twice — the shared processor host delivers at-least-once; the inbox makes the replay a no-op.
        await using (var context = harness.CreateContext())
        {
            await BuildConsumer(context).ConsumeAsync(jobPosted);
        }

        await using (var context = harness.CreateContext())
        {
            await BuildConsumer(context).ConsumeAsync(jobPosted);
        }

        await using var assert = harness.CreateContext();
        var reference = await assert.JobReferences.SingleAsync();
        Assert.Equal(jobId, reference.JobId);
        Assert.Equal("Senior Engineer", reference.Title);
        Assert.Equal(employerId, reference.EmployerId);

        // Exactly one inbox row for the JobPosted message id — the redelivery matched and no-opped.
        Assert.Equal(jobPosted.Id, (await assert.InboxMessages.SingleAsync()).MessageId);
    }

    [Fact]
    public async Task ConsumeAsync_ForASecondJob_UpsertsASecondRow_LeavingTheFirstUntouched()
    {
        using var harness = new ApplicationsSqliteHarness();
        var firstEmployer = Guid.NewGuid();
        var secondEmployer = Guid.NewGuid();

        var first = new JobPosted(Guid.NewGuid(), Guid.NewGuid(), firstEmployer, "First Role", "Remote", DateTime.UtcNow);
        var second = new JobPosted(Guid.NewGuid(), Guid.NewGuid(), secondEmployer, "Second Role", "Onsite", DateTime.UtcNow);

        await using (var context = harness.CreateContext())
        {
            await BuildConsumer(context).ConsumeAsync(first);
        }

        await using (var context = harness.CreateContext())
        {
            await BuildConsumer(context).ConsumeAsync(second);
        }

        await using var assert = harness.CreateContext();
        Assert.Equal(2, await assert.JobReferences.CountAsync());
        Assert.Equal("First Role", (await assert.JobReferences.FindAsync(first.JobId))!.Title);
        Assert.Equal("Second Role", (await assert.JobReferences.FindAsync(second.JobId))!.Title);
    }
}
