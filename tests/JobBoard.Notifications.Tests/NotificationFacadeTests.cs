using JobBoard.Notifications.Core.Facade;
using JobBoard.Notifications.Tests.Fakes;
using Xunit;

namespace JobBoard.Notifications.Tests;

/// <summary>The facade is a thin pass-through — each handler forwards its event to the matching business call.</summary>
public sealed class NotificationFacadeTests
{
    [Fact]
    public async Task EachHandler_DelegatesToBusiness()
    {
        var business = new FakeNotificationBusiness();
        var facade = new NotificationFacade(business);

        var submitted = TestData.ApplicationSubmitted();
        var statusChanged = TestData.ApplicationStatusChanged();
        var posted = TestData.JobPosted();

        await facade.HandleApplicationSubmittedAsync(submitted);
        await facade.HandleApplicationStatusChangedAsync(statusChanged);
        await facade.HandleJobPostedAsync(posted);

        Assert.Same(submitted, business.Submitted);
        Assert.Same(statusChanged, business.StatusChanged);
        Assert.Same(posted, business.Posted);
    }
}
