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
/// The read-model seam from the consuming side: an <c>EmployerProfileChanged</c> event mirrors the
/// employer's company name into this service's own <c>EmployerReference</c> projection (ADR-0012 option
/// B) — exactly once, even when the message is redelivered. The whole real stack runs (consumer → facade →
/// business → data → repository + inbox) over SQLite, one fresh context per delivery.
/// </summary>
public sealed class EmployerProfileChangedConsumerTests
{
    private static EmployerProfileChangedConsumer BuildConsumer(ApplicationsDbContext context)
    {
        var dataLayer = new ApplicationDataLayer(
            new ApplicationRepository(context), new Outbox(context), new Inbox(context));
        var business = new ApplicationBusiness(dataLayer, new AmbientRequestContext());
        var facade = new ApplicationFacade(
            business, new SubmitApplicationViewModelValidator(), new AdvanceApplicationStatusViewModelValidator());
        return new EmployerProfileChangedConsumer(facade);
    }

    [Fact]
    public async Task ConsumeAsync_UpsertsEmployerReference_ExactlyOnce_OnRedelivery()
    {
        using var harness = new ApplicationsSqliteHarness();
        var employerId = Guid.NewGuid();

        var changed = new EmployerProfileChanged(Guid.NewGuid(), employerId, "Globex Corp", DateTime.UtcNow);

        await using (var context = harness.CreateContext())
        {
            await BuildConsumer(context).ConsumeAsync(changed);
        }

        await using (var context = harness.CreateContext())
        {
            await BuildConsumer(context).ConsumeAsync(changed);
        }

        await using var assert = harness.CreateContext();
        var reference = await assert.EmployerReferences.SingleAsync();
        Assert.Equal(employerId, reference.EmployerId);
        Assert.Equal("Globex Corp", reference.CompanyName);

        Assert.Equal(changed.Id, (await assert.InboxMessages.SingleAsync()).MessageId);
    }

    [Fact]
    public async Task ConsumeAsync_OnACompanyRename_UpdatesTheExistingRow()
    {
        using var harness = new ApplicationsSqliteHarness();
        var employerId = Guid.NewGuid();

        var original = new EmployerProfileChanged(Guid.NewGuid(), employerId, "Old Name Inc", DateTime.UtcNow);
        var renamed = new EmployerProfileChanged(Guid.NewGuid(), employerId, "New Name LLC", DateTime.UtcNow);

        await using (var context = harness.CreateContext())
        {
            await BuildConsumer(context).ConsumeAsync(original);
        }

        await using (var context = harness.CreateContext())
        {
            await BuildConsumer(context).ConsumeAsync(renamed);
        }

        await using var assert = harness.CreateContext();
        // One row, updated in place — a rename does not fan out to any application row (read-time join).
        var reference = await assert.EmployerReferences.SingleAsync();
        Assert.Equal("New Name LLC", reference.CompanyName);
    }
}
