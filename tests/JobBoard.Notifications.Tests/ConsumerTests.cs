using JobBoard.Notifications.Consumers;
using JobBoard.Notifications.Tests.Fakes;
using Xunit;

namespace JobBoard.Notifications.Tests;

/// <summary>Each consumer is a thin entry point — it forwards its event to the matching facade call.</summary>
public sealed class ConsumerTests
{
    [Fact]
    public async Task ApplicationSubmittedConsumer_DelegatesToFacade()
    {
        var facade = new FakeNotificationFacade();
        var @event = TestData.ApplicationSubmitted();

        await new ApplicationSubmittedConsumer(facade).ConsumeAsync(@event);

        Assert.Same(@event, facade.Submitted);
    }

    [Fact]
    public async Task ApplicationStatusChangedConsumer_DelegatesToFacade()
    {
        var facade = new FakeNotificationFacade();
        var @event = TestData.ApplicationStatusChanged();

        await new ApplicationStatusChangedConsumer(facade).ConsumeAsync(@event);

        Assert.Same(@event, facade.StatusChanged);
    }

    [Fact]
    public async Task JobPostedConsumer_DelegatesToFacade()
    {
        var facade = new FakeNotificationFacade();
        var @event = TestData.JobPosted();

        await new JobPostedConsumer(facade).ConsumeAsync(@event);

        Assert.Same(@event, facade.Posted);
    }
}
