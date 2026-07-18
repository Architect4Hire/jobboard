using JobBoard.Applications.Core.Business;
using JobBoard.Applications.Core.Managers.Models.Domain;
using JobBoard.Applications.Tests.Fakes;
using JobBoard.Contracts;
using JobBoard.Shared.Errors;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace JobBoard.Applications.Tests;

public sealed class ApplicationBusinessTests
{
    [Fact]
    public async Task SubmitAsync_TranslatesViewModelToSubmittedApplication_AndBuildsEvent()
    {
        var dataLayer = new FakeApplicationDataLayer();
        var business = new ApplicationBusiness(dataLayer);
        var candidateId = Guid.NewGuid();
        var jobId = Guid.NewGuid();

        var vm = TestData.SubmitViewModel(candidateId: candidateId, jobId: jobId);

        var result = await business.SubmitAsync(vm);

        var added = dataLayer.SubmittedApplication;
        Assert.NotNull(added);
        Assert.NotEqual(Guid.Empty, added!.Id);
        Assert.Equal(ApplicationStatus.Submitted, added.Status);
        Assert.Equal(candidateId, added.CandidateId);
        Assert.Equal(jobId, added.JobId);

        var @event = dataLayer.SubmittedEvent;
        Assert.NotNull(@event);
        Assert.NotEqual(Guid.Empty, @event!.Id);
        Assert.Equal(added.Id, @event.ApplicationId);
        Assert.Equal(candidateId, @event.CandidateId);
        Assert.Equal(jobId, @event.JobId);

        Assert.Equal(added.Id, result.Id);
        Assert.Equal(ApplicationStatus.Submitted, result.Status);
    }

    [Theory]
    [InlineData(ApplicationStatus.Submitted)]
    [InlineData(ApplicationStatus.Reviewed)]
    [InlineData(ApplicationStatus.Offered)]
    public async Task WithdrawAsync_OnActiveApplication_SetsWithdrawn_AndBuildsEvent(ApplicationStatus current)
    {
        var application = TestData.Application(status: current);
        var dataLayer = new FakeApplicationDataLayer { GetResult = application };
        var business = new ApplicationBusiness(dataLayer);

        var result = await business.WithdrawAsync(application.Id);

        Assert.Equal(ApplicationStatus.Withdrawn, result.Status);

        var @event = dataLayer.WithdrawEvent;
        Assert.NotNull(@event);
        Assert.Equal(application.Id, @event!.ApplicationId);
        Assert.Equal(current.ToString(), @event.FromStatus);
        Assert.Equal(nameof(ApplicationStatus.Withdrawn), @event.ToStatus);
    }

    [Theory]
    [InlineData(ApplicationStatus.Rejected)]
    [InlineData(ApplicationStatus.Withdrawn)]
    public async Task WithdrawAsync_OnTerminalApplication_Throws_AndEmitsNoEvent(ApplicationStatus terminal)
    {
        var application = TestData.Application(status: terminal);
        var dataLayer = new FakeApplicationDataLayer { GetResult = application };
        var business = new ApplicationBusiness(dataLayer);

        var ex = await Assert.ThrowsAsync<DomainException>(() => business.WithdrawAsync(application.Id));

        Assert.Equal("application.not_active", ex.Code);
        Assert.Equal(StatusCodes.Status409Conflict, ex.StatusCode);
        Assert.Null(dataLayer.WithdrawEvent);   // fast path: the data layer was never asked to withdraw
    }

    [Fact]
    public async Task WithdrawAsync_WhenDataLayerReportsLostRace_Throws409()
    {
        var application = TestData.Application(status: ApplicationStatus.Submitted);
        var dataLayer = new FakeApplicationDataLayer { GetResult = application, WithdrawResult = false };
        var business = new ApplicationBusiness(dataLayer);

        var ex = await Assert.ThrowsAsync<DomainException>(() => business.WithdrawAsync(application.Id));

        Assert.Equal("application.not_active", ex.Code);
        Assert.Equal(StatusCodes.Status409Conflict, ex.StatusCode);
        Assert.NotNull(dataLayer.WithdrawEvent); // it attempted the withdraw (built the event)...
        // ...but nothing was published because the data layer enqueued nothing on a false result.
    }

    [Fact]
    public async Task WithdrawAsync_OnMissingApplication_Throws404()
    {
        var dataLayer = new FakeApplicationDataLayer { GetResult = null };
        var business = new ApplicationBusiness(dataLayer);

        var ex = await Assert.ThrowsAsync<DomainException>(() => business.WithdrawAsync(Guid.NewGuid()));

        Assert.Equal("application.not_found", ex.Code);
        Assert.Equal(StatusCodes.Status404NotFound, ex.StatusCode);
    }

    [Theory]
    [InlineData(ApplicationStatus.Submitted, ApplicationStatus.Reviewed)]
    [InlineData(ApplicationStatus.Reviewed, ApplicationStatus.Offered)]
    [InlineData(ApplicationStatus.Reviewed, ApplicationStatus.Rejected)]
    [InlineData(ApplicationStatus.Offered, ApplicationStatus.Rejected)]
    public async Task AdvanceAsync_OnLegalTransition_SetsTarget_AndBuildsEvent(ApplicationStatus from, ApplicationStatus to)
    {
        var application = TestData.Application(status: from);
        var dataLayer = new FakeApplicationDataLayer { GetResult = application };
        var business = new ApplicationBusiness(dataLayer);

        var result = await business.AdvanceAsync(application.Id, TestData.AdvanceViewModel(to));

        Assert.Equal(to, result.Status);
        Assert.Equal(from, dataLayer.AdvanceExpected);
        Assert.Equal(to, dataLayer.AdvanceTarget);

        var @event = dataLayer.AdvanceEvent;
        Assert.NotNull(@event);
        Assert.Equal(from.ToString(), @event!.FromStatus);
        Assert.Equal(to.ToString(), @event.ToStatus);
    }

    [Theory]
    [InlineData(ApplicationStatus.Submitted, ApplicationStatus.Offered)]
    [InlineData(ApplicationStatus.Submitted, ApplicationStatus.Withdrawn)]
    [InlineData(ApplicationStatus.Offered, ApplicationStatus.Reviewed)]
    [InlineData(ApplicationStatus.Rejected, ApplicationStatus.Offered)]
    public async Task AdvanceAsync_OnIllegalTransition_Throws_AndNeverReachesDataLayer(ApplicationStatus from, ApplicationStatus to)
    {
        var application = TestData.Application(status: from);
        var dataLayer = new FakeApplicationDataLayer { GetResult = application };
        var business = new ApplicationBusiness(dataLayer);

        var ex = await Assert.ThrowsAsync<DomainException>(() => business.AdvanceAsync(application.Id, TestData.AdvanceViewModel(to)));

        Assert.Equal("application.invalid_transition", ex.Code);
        Assert.Equal(StatusCodes.Status409Conflict, ex.StatusCode);
        Assert.Null(dataLayer.AdvanceEvent);
    }

    [Fact]
    public async Task AdvanceAsync_WhenDataLayerReportsLostRace_Throws409()
    {
        var application = TestData.Application(status: ApplicationStatus.Submitted);
        var dataLayer = new FakeApplicationDataLayer { GetResult = application, AdvanceResult = false };
        var business = new ApplicationBusiness(dataLayer);

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => business.AdvanceAsync(application.Id, TestData.AdvanceViewModel(ApplicationStatus.Reviewed)));

        Assert.Equal("application.invalid_transition", ex.Code);
        Assert.Equal(StatusCodes.Status409Conflict, ex.StatusCode);
    }

    [Fact]
    public async Task AdvanceAsync_OnMissingApplication_Throws404()
    {
        var dataLayer = new FakeApplicationDataLayer { GetResult = null };
        var business = new ApplicationBusiness(dataLayer);

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => business.AdvanceAsync(Guid.NewGuid(), TestData.AdvanceViewModel(ApplicationStatus.Reviewed)));

        Assert.Equal("application.not_found", ex.Code);
        Assert.Equal(StatusCodes.Status404NotFound, ex.StatusCode);
    }

    [Fact]
    public async Task HandleJobClosedAsync_ClosesToRejected_AndBuildsAStatusChangedPerApplication()
    {
        var jobId = Guid.NewGuid();
        var dataLayer = new FakeApplicationDataLayer { CloseResult = 3 };
        var business = new ApplicationBusiness(dataLayer);

        var jobClosed = new JobClosed(Guid.NewGuid(), jobId, Guid.NewGuid(), DateTime.UtcNow);

        await business.HandleJobClosedAsync(jobClosed);

        Assert.Equal(jobId, dataLayer.CloseJobId);
        Assert.Equal(jobClosed.Id, dataLayer.CloseMessageId);          // inbox dedupes on the event id
        Assert.Equal(ApplicationStatus.Rejected, dataLayer.CloseTarget);

        // The factory business handed down builds a Rejected event from a snapshot entity's prior status.
        var snapshot = TestData.Application(status: ApplicationStatus.Reviewed);
        var built = dataLayer.CloseBuildEvent!(snapshot);
        Assert.Equal(snapshot.Id, built.ApplicationId);
        Assert.Equal(nameof(ApplicationStatus.Reviewed), built.FromStatus);
        Assert.Equal(nameof(ApplicationStatus.Rejected), built.ToStatus);
    }

    [Fact]
    public async Task GetAsync_Missing_ReturnsNull()
    {
        var business = new ApplicationBusiness(new FakeApplicationDataLayer { GetResult = null });

        Assert.Null(await business.GetAsync(Guid.NewGuid()));
    }
}
