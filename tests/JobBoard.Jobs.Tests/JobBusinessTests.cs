using JobBoard.Jobs.Core.Business;
using JobBoard.Jobs.Core.Managers.Models.Domain;
using JobBoard.Jobs.Core.Managers.Models.ViewModels;
using JobBoard.Jobs.Tests.Fakes;
using JobBoard.Shared.Errors;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace JobBoard.Jobs.Tests;

public sealed class JobBusinessTests
{
    [Fact]
    public async Task PostAsync_TranslatesViewModelToOpenJob()
    {
        var dataLayer = new FakeJobDataLayer();
        var business = new JobBusiness(dataLayer);
        var employerId = Guid.NewGuid();

        var vm = TestData.PostViewModel(
            title: "Platform Engineer",
            employerId: employerId,
            categories: [new JobClassificationViewModel { Name = "Engineering", Slug = "engineering" }]);

        var result = await business.PostAsync(vm);

        var added = dataLayer.AddedJob;
        Assert.NotNull(added);
        Assert.NotEqual(Guid.Empty, added!.Id);
        Assert.Equal(JobStatus.Open, added.Status);
        Assert.Equal("Platform Engineer", added.Title);
        Assert.Equal(employerId, added.EmployerId);
        Assert.Equal("engineering", Assert.Single(added.Categories).Slug);
        Assert.Equal("USD", added.Salary.Currency);

        Assert.Equal(added.Id, result.Id);
        Assert.Equal(JobStatus.Open, result.Status);

        // A post builds the JobPosted fact (fresh id, denormalized title/location) for the data layer to enqueue.
        var posted = dataLayer.PostedEvent;
        Assert.NotNull(posted);
        Assert.NotEqual(Guid.Empty, posted!.Id);
        Assert.Equal(added.Id, posted.JobId);
        Assert.Equal(employerId, posted.EmployerId);
        Assert.Equal("Platform Engineer", posted.Title);
        Assert.Equal(added.Location, posted.Location);
    }

    [Fact]
    public async Task CloseAsync_OnOpenJob_SetsClosedAndBuildsMatchingEvent()
    {
        var job = TestData.Job(status: JobStatus.Open);
        var dataLayer = new FakeJobDataLayer { GetResult = job };
        var business = new JobBusiness(dataLayer);

        var result = await business.CloseAsync(job.Id);

        Assert.Equal(JobStatus.Closed, job.Status);
        Assert.Equal(JobStatus.Closed, result.Status);

        var @event = dataLayer.ClosedEvent;
        Assert.NotNull(@event);
        Assert.NotEqual(Guid.Empty, @event!.Id);
        Assert.Equal(job.Id, @event.JobId);
        Assert.Equal(job.EmployerId, @event.EmployerId);
        Assert.Equal(job.Id, dataLayer.ClosedId);
    }

    [Fact]
    public async Task CloseAsync_WhenDataLayerReportsLostRace_Throws409()
    {
        // Read said Open, but a concurrent close won: the conditional close reports false.
        var job = TestData.Job(status: JobStatus.Open);
        var dataLayer = new FakeJobDataLayer { GetResult = job, CloseResult = false };
        var business = new JobBusiness(dataLayer);

        var ex = await Assert.ThrowsAsync<DomainException>(() => business.CloseAsync(job.Id));

        Assert.Equal("job.not_open", ex.Code);
        Assert.Equal(StatusCodes.Status409Conflict, ex.StatusCode);
        Assert.NotNull(dataLayer.ClosedEvent); // it did attempt the close (built the event)...
        // ...but the event was never published because the data layer enqueued nothing.
    }

    [Theory]
    [InlineData(JobStatus.Closed)]
    [InlineData(JobStatus.Draft)]
    public async Task CloseAsync_OnNonOpenJob_Throws_AndEmitsNoEvent(JobStatus status)
    {
        var job = TestData.Job(status: status);
        var dataLayer = new FakeJobDataLayer { GetResult = job };
        var business = new JobBusiness(dataLayer);

        var ex = await Assert.ThrowsAsync<DomainException>(() => business.CloseAsync(job.Id));

        Assert.Equal("job.not_open", ex.Code);
        Assert.Equal(StatusCodes.Status409Conflict, ex.StatusCode);
        Assert.Null(dataLayer.ClosedEvent);   // fast path: the data layer was never asked to close
        Assert.Null(dataLayer.ClosedId);
    }

    [Fact]
    public async Task CloseAsync_OnMissingJob_Throws404_AndEmitsNoEvent()
    {
        var dataLayer = new FakeJobDataLayer { GetResult = null };
        var business = new JobBusiness(dataLayer);

        var ex = await Assert.ThrowsAsync<DomainException>(() => business.CloseAsync(Guid.NewGuid()));

        Assert.Equal("job.not_found", ex.Code);
        Assert.Equal(StatusCodes.Status404NotFound, ex.StatusCode);
        Assert.Null(dataLayer.ClosedEvent);
    }

    [Fact]
    public async Task GetAsync_Missing_ReturnsNull()
    {
        var business = new JobBusiness(new FakeJobDataLayer { GetResult = null });

        Assert.Null(await business.GetAsync(Guid.NewGuid()));
    }
}
