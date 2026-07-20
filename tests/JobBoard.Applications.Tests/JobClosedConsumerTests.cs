using System.Text.Json;
using JobBoard.Applications.Consumers;
using JobBoard.Applications.Core.Business;
using JobBoard.Applications.Core.Data;
using JobBoard.Applications.Core.Facade;
using JobBoard.Applications.Core.Managers.Models.Domain;
using JobBoard.Applications.Core.Managers.Validators;
using JobBoard.Contracts;
using JobBoard.Shared.Messaging;
using JobBoard.Shared.Requests;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace JobBoard.Applications.Tests;

/// <summary>
/// The two-service seam from the consuming side: a <c>JobClosed</c> event closes this service's open
/// applications for the job — in its OWN database — exactly once, even when the message is redelivered
/// (Service Bus is at-least-once). The whole real stack runs (consumer → facade → business → data →
/// repository + outbox/inbox) over SQLite, one fresh context per delivery, as the processor host would.
/// </summary>
public sealed class JobClosedConsumerTests
{
    private static JobClosedConsumer BuildConsumer(ApplicationsDbContext context)
    {
        var dataLayer = new ApplicationDataLayer(
            new ApplicationRepository(context), new Outbox(context), new Inbox(context));
        // The consumer path never reads the request context (there is no HTTP request), so an empty one is
        // correct here — the audit thread is inherited from the consumed event instead (SCRUB A3).
        var business = new ApplicationBusiness(dataLayer, new AmbientRequestContext());
        var facade = new ApplicationFacade(
            business, new SubmitApplicationViewModelValidator(), new AdvanceApplicationStatusViewModelValidator());
        return new JobClosedConsumer(facade);
    }

    [Fact]
    public async Task ConsumeAsync_ClosesOpenApplicationsForJob_ExactlyOnce_OnRedelivery()
    {
        using var harness = new ApplicationsSqliteHarness();
        var jobId = Guid.NewGuid();

        var submitted = TestData.Application(jobId: jobId, status: ApplicationStatus.Submitted);
        var offered = TestData.Application(jobId: jobId, status: ApplicationStatus.Offered);
        var otherJob = TestData.Application(status: ApplicationStatus.Submitted);

        await using (var seed = harness.CreateContext())
        {
            seed.Applications.AddRange(submitted, offered, otherJob);
            await seed.SaveChangesAsync();
        }

        // JobClosed arrives carrying its own audit thread; every ApplicationStatusChanged it triggers must
        // inherit correlation + actor and name JobClosed as its cause (SCRUB A3).
        var correlationId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var jobClosed = new JobClosed(Guid.NewGuid(), jobId, Guid.NewGuid(), DateTime.UtcNow)
        {
            CorrelationId = correlationId,
            CausationId = Guid.NewGuid(),
            ActorId = actorId,
        };

        // Deliver twice — the shared processor host delivers at-least-once; the inbox makes the replay a no-op.
        await using (var context = harness.CreateContext())
        {
            await BuildConsumer(context).ConsumeAsync(jobClosed);
        }

        await using (var context = harness.CreateContext())
        {
            await BuildConsumer(context).ConsumeAsync(jobClosed);
        }

        await using var assert = harness.CreateContext();

        // The job's active applications were closed to Rejected; the other job's application is untouched.
        Assert.Equal(ApplicationStatus.Rejected, (await assert.Applications.FindAsync(submitted.Id))!.Status);
        Assert.Equal(ApplicationStatus.Rejected, (await assert.Applications.FindAsync(offered.Id))!.Status);
        Assert.Equal(ApplicationStatus.Submitted, (await assert.Applications.FindAsync(otherJob.Id))!.Status);

        // Exactly one ApplicationStatusChanged per closed application — the redelivery published none.
        var outbox = await assert.OutboxMessages.ToListAsync();
        Assert.Equal(2, outbox.Count);
        Assert.All(outbox, r => Assert.Equal(nameof(ApplicationStatusChanged), r.Type));

        // Each published event inherited JobClosed's thread: same correlation and actor, caused by JobClosed.
        var serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Assert.All(outbox, r =>
        {
            var published = JsonSerializer.Deserialize<ApplicationStatusChanged>(r.Payload, serializerOptions)!;
            Assert.Equal(correlationId, published.CorrelationId);
            Assert.Equal(jobClosed.Id, published.CausationId);
            Assert.Equal(actorId, published.ActorId);
        });

        // And exactly one inbox row for the JobClosed message id.
        Assert.Equal(jobClosed.Id, (await assert.InboxMessages.SingleAsync()).MessageId);
    }
}
