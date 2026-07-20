using JobBoard.Notifications.Core.Business;
using JobBoard.Notifications.Tests.Fakes;
using Xunit;

namespace JobBoard.Notifications.Tests;

public sealed class NotificationBusinessTests
{
    [Fact]
    public async Task HandleApplicationSubmitted_LogsForCandidate_KeyedByEventId()
    {
        var dataLayer = new FakeNotificationDataLayer();
        var business = new NotificationBusiness(dataLayer);
        var @event = TestData.ApplicationSubmitted(candidateId: Guid.NewGuid());

        await business.HandleApplicationSubmittedAsync(@event);

        Assert.NotNull(dataLayer.Recorded);
        Assert.Equal(@event.CandidateId, dataLayer.Recorded!.RecipientId);
        Assert.Equal("ApplicationSubmitted", dataLayer.Recorded.Kind);
        Assert.Equal(@event.Id, dataLayer.MessageId); // dedupe key = the event id (the SB MessageId)
    }

    [Fact]
    public async Task HandleApplicationStatusChanged_LogsForCandidate_WithStatusInBody()
    {
        var dataLayer = new FakeNotificationDataLayer();
        var business = new NotificationBusiness(dataLayer);
        var @event = TestData.ApplicationStatusChanged(candidateId: Guid.NewGuid(), from: "Submitted", to: "Offered");

        await business.HandleApplicationStatusChangedAsync(@event);

        Assert.Equal(@event.CandidateId, dataLayer.Recorded!.RecipientId);
        Assert.Equal("ApplicationStatusChanged", dataLayer.Recorded.Kind);
        Assert.Contains("Offered", dataLayer.Recorded.Body);
        Assert.Equal(@event.Id, dataLayer.MessageId);
    }

    [Fact]
    public async Task HandleJobPosted_LogsForEmployer_WithTitle()
    {
        var dataLayer = new FakeNotificationDataLayer();
        var business = new NotificationBusiness(dataLayer);
        var @event = TestData.JobPosted(employerId: Guid.NewGuid(), title: "Staff SRE");

        await business.HandleJobPostedAsync(@event);

        Assert.Equal(@event.EmployerId, dataLayer.Recorded!.RecipientId); // employer, not candidate
        Assert.Equal("JobPosted", dataLayer.Recorded.Kind);
        Assert.Contains("Staff SRE", dataLayer.Recorded.Body);
        Assert.Equal(@event.Id, dataLayer.MessageId);
    }
}
