using JobBoard.Applications.Core.Business;
using JobBoard.Applications.Core.Managers.Models.Domain;
using JobBoard.Applications.Core.Managers.Models.ServiceModels;
using JobBoard.Applications.Tests.Fakes;
using JobBoard.Contracts;
using JobBoard.Shared.Errors;
using JobBoard.Shared.Requests;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace JobBoard.Applications.Tests;

public sealed class ApplicationBusinessTests
{
    // A known request thread the business reads from and stamps onto request-initiated events (ADR-0013).
    private static readonly Guid CorrelationId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly IRequestContext RequestContext = BuildContext();

    private static AmbientRequestContext BuildContext()
    {
        var context = new AmbientRequestContext();
        context.Populate(CorrelationId, ActorId, "candidate");
        return context;
    }

    [Fact]
    public async Task SubmitAsync_TranslatesViewModelToSubmittedApplication_AndBuildsEvent()
    {
        var dataLayer = new FakeApplicationDataLayer();
        var business = new ApplicationBusiness(dataLayer, RequestContext);
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

        // Root of its request thread: correlation and actor from the context, causation is the request's
        // own id (ADR-0013, SCRUB A3).
        Assert.Equal(CorrelationId, @event.CorrelationId);
        Assert.Equal(CorrelationId, @event.CausationId);
        Assert.Equal(ActorId, @event.ActorId);

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
        var business = new ApplicationBusiness(dataLayer, RequestContext);

        var result = await business.WithdrawAsync(application.Id);

        Assert.Equal(ApplicationStatus.Withdrawn, result.Status);

        var @event = dataLayer.WithdrawEvent;
        Assert.NotNull(@event);
        Assert.Equal(application.Id, @event!.ApplicationId);
        Assert.Equal(current.ToString(), @event.FromStatus);
        Assert.Equal(nameof(ApplicationStatus.Withdrawn), @event.ToStatus);

        // Request-initiated: carries the request thread (ADR-0013, SCRUB A3).
        Assert.Equal(CorrelationId, @event.CorrelationId);
        Assert.Equal(CorrelationId, @event.CausationId);
        Assert.Equal(ActorId, @event.ActorId);
    }

    [Theory]
    [InlineData(ApplicationStatus.Rejected)]
    [InlineData(ApplicationStatus.Withdrawn)]
    public async Task WithdrawAsync_OnTerminalApplication_Throws_AndEmitsNoEvent(ApplicationStatus terminal)
    {
        var application = TestData.Application(status: terminal);
        var dataLayer = new FakeApplicationDataLayer { GetResult = application };
        var business = new ApplicationBusiness(dataLayer, RequestContext);

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
        var business = new ApplicationBusiness(dataLayer, RequestContext);

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
        var business = new ApplicationBusiness(dataLayer, RequestContext);

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
        var business = new ApplicationBusiness(dataLayer, RequestContext);

        var result = await business.AdvanceAsync(application.Id, TestData.AdvanceViewModel(to));

        Assert.Equal(to, result.Status);
        Assert.Equal(from, dataLayer.AdvanceExpected);
        Assert.Equal(to, dataLayer.AdvanceTarget);

        var @event = dataLayer.AdvanceEvent;
        Assert.NotNull(@event);
        Assert.Equal(from.ToString(), @event!.FromStatus);
        Assert.Equal(to.ToString(), @event.ToStatus);

        // Request-initiated: carries the request thread (ADR-0013, SCRUB A3).
        Assert.Equal(CorrelationId, @event.CorrelationId);
        Assert.Equal(CorrelationId, @event.CausationId);
        Assert.Equal(ActorId, @event.ActorId);
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
        var business = new ApplicationBusiness(dataLayer, RequestContext);

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
        var business = new ApplicationBusiness(dataLayer, RequestContext);

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => business.AdvanceAsync(application.Id, TestData.AdvanceViewModel(ApplicationStatus.Reviewed)));

        Assert.Equal("application.invalid_transition", ex.Code);
        Assert.Equal(StatusCodes.Status409Conflict, ex.StatusCode);
    }

    [Fact]
    public async Task AdvanceAsync_OnMissingApplication_Throws404()
    {
        var dataLayer = new FakeApplicationDataLayer { GetResult = null };
        var business = new ApplicationBusiness(dataLayer, RequestContext);

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
        var business = new ApplicationBusiness(dataLayer, RequestContext);

        // The consumed event carries its own audit thread; the follow-on must inherit it.
        var incomingCorrelation = Guid.NewGuid();
        var incomingActor = Guid.NewGuid();
        var jobClosed = new JobClosed(Guid.NewGuid(), jobId, Guid.NewGuid(), DateTime.UtcNow)
        {
            CorrelationId = incomingCorrelation,
            CausationId = Guid.NewGuid(),
            ActorId = incomingActor,
        };

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

        // Follow-on thread: correlation and actor inherited from JobClosed, causation is JobClosed's id
        // (its direct cause) — not the request context, which is empty on the consumer path (SCRUB A3).
        Assert.Equal(incomingCorrelation, built.CorrelationId);
        Assert.Equal(jobClosed.Id, built.CausationId);
        Assert.Equal(incomingActor, built.ActorId);
    }

    [Fact]
    public async Task HandleJobPostedAsync_PassesEventFieldsToTheDataLayer()
    {
        var dataLayer = new FakeApplicationDataLayer();
        var business = new ApplicationBusiness(dataLayer, RequestContext);
        var jobId = Guid.NewGuid();
        var employerId = Guid.NewGuid();

        var jobPosted = new JobPosted(Guid.NewGuid(), jobId, employerId, "Senior Engineer", "Remote", DateTime.UtcNow);

        await business.HandleJobPostedAsync(jobPosted);

        Assert.Equal((jobId, jobPosted.Id, "Senior Engineer", employerId), dataLayer.UpsertedJobReference);
    }

    [Fact]
    public async Task HandleEmployerProfileChangedAsync_PassesEventFieldsToTheDataLayer()
    {
        var dataLayer = new FakeApplicationDataLayer();
        var business = new ApplicationBusiness(dataLayer, RequestContext);
        var employerId = Guid.NewGuid();

        var changed = new EmployerProfileChanged(Guid.NewGuid(), employerId, "Globex Corp", DateTime.UtcNow);

        await business.HandleEmployerProfileChangedAsync(changed);

        Assert.Equal((employerId, changed.Id, "Globex Corp"), dataLayer.UpsertedEmployerReference);
    }

    [Fact]
    public async Task ListMineAsync_UsesTheAmbientActorId_NeverAClientSuppliedOne()
    {
        var expected = new List<ApplicationHistoryServiceModel>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), "Title", Guid.NewGuid(), "Employer", ApplicationStatus.Submitted, DateTime.UtcNow, DateTime.UtcNow),
        };
        var dataLayer = new FakeApplicationDataLayer { MineResult = expected };
        var business = new ApplicationBusiness(dataLayer, RequestContext);

        var result = await business.ListMineAsync();

        Assert.Equal(ActorId, dataLayer.MineCandidateId);
        Assert.Same(expected, result);
    }

    [Fact]
    public async Task ListMineAsync_WithNoAuthenticatedActor_Throws401()
    {
        var anonymous = new AmbientRequestContext();
        anonymous.Populate(Guid.NewGuid(), null, null);
        var business = new ApplicationBusiness(new FakeApplicationDataLayer(), anonymous);

        var ex = await Assert.ThrowsAsync<DomainException>(() => business.ListMineAsync());

        Assert.Equal("application.unauthenticated", ex.Code);
        Assert.Equal(StatusCodes.Status401Unauthorized, ex.StatusCode);
    }

    [Fact]
    public async Task GetAsync_Missing_ReturnsNull()
    {
        var business = new ApplicationBusiness(new FakeApplicationDataLayer { GetResult = null }, RequestContext);

        Assert.Null(await business.GetAsync(Guid.NewGuid()));
    }
}
