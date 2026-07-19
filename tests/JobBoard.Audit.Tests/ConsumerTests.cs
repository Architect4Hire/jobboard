using JobBoard.Audit.Consumers;
using JobBoard.Audit.Tests.Fakes;
using JobBoard.Contracts;
using Xunit;

namespace JobBoard.Audit.Tests;

/// <summary>The generic sink is a thin entry point — for every event type it forwards the event to the
/// facade unchanged.</summary>
public sealed class ConsumerTests
{
    [Fact]
    public async Task AuditConsumer_JobPosted_DelegatesToFacade()
    {
        var facade = new FakeAuditFacade();
        var @event = TestData.JobPosted();

        await new AuditConsumer<JobPosted>(facade).ConsumeAsync(@event);

        Assert.Same(@event, facade.Recorded);
    }

    [Fact]
    public async Task AuditConsumer_JobClosed_DelegatesToFacade()
    {
        var facade = new FakeAuditFacade();
        var @event = TestData.JobClosed();

        await new AuditConsumer<JobClosed>(facade).ConsumeAsync(@event);

        Assert.Same(@event, facade.Recorded);
    }

    [Fact]
    public async Task AuditConsumer_ApplicationSubmitted_DelegatesToFacade()
    {
        var facade = new FakeAuditFacade();
        var @event = TestData.ApplicationSubmitted();

        await new AuditConsumer<ApplicationSubmitted>(facade).ConsumeAsync(@event);

        Assert.Same(@event, facade.Recorded);
    }

    [Fact]
    public async Task AuditConsumer_ApplicationStatusChanged_DelegatesToFacade()
    {
        var facade = new FakeAuditFacade();
        var @event = TestData.ApplicationStatusChanged();

        await new AuditConsumer<ApplicationStatusChanged>(facade).ConsumeAsync(@event);

        Assert.Same(@event, facade.Recorded);
    }

    [Fact]
    public async Task AuditConsumer_AccountCreated_DelegatesToFacade()
    {
        var facade = new FakeAuditFacade();
        var @event = TestData.AccountCreated();

        await new AuditConsumer<AccountCreated>(facade).ConsumeAsync(@event);

        Assert.Same(@event, facade.Recorded);
    }

    [Fact]
    public async Task AuditConsumer_LoggedIn_DelegatesToFacade()
    {
        var facade = new FakeAuditFacade();
        var @event = TestData.LoggedIn();

        await new AuditConsumer<LoggedIn>(facade).ConsumeAsync(@event);

        Assert.Same(@event, facade.Recorded);
    }

    [Fact]
    public async Task AuditConsumer_LoginFailed_DelegatesToFacade()
    {
        var facade = new FakeAuditFacade();
        var @event = TestData.LoginFailed();

        await new AuditConsumer<LoginFailed>(facade).ConsumeAsync(@event);

        Assert.Same(@event, facade.Recorded);
    }

    [Fact]
    public async Task AuditConsumer_ProfileUpdated_DelegatesToFacade()
    {
        var facade = new FakeAuditFacade();
        var @event = TestData.ProfileUpdated();

        await new AuditConsumer<ProfileUpdated>(facade).ConsumeAsync(@event);

        Assert.Same(@event, facade.Recorded);
    }
}
