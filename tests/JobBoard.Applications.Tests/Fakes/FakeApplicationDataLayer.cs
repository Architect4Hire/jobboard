using JobBoard.Applications.Core.Data;
using JobBoard.Applications.Core.Managers.Models.Domain;
using JobBoard.Applications.Core.Managers.Models.ServiceModels;
using JobBoard.Contracts;

namespace JobBoard.Applications.Tests.Fakes;

/// <summary>
/// Hand-rolled <see cref="IApplicationDataLayer"/> for business-layer tests. Returns configured values and
/// captures what business handed down — the application it submitted, the events it built, the transition
/// arguments, and the close factory — so a test can assert translation, rule enforcement, and event
/// building without a database.
/// </summary>
public sealed class FakeApplicationDataLayer : IApplicationDataLayer
{
    public IReadOnlyList<ApplicationSummaryServiceModel> ListResult { get; init; } = [];

    public Application? GetResult { get; set; }

    public Application? SubmittedApplication { get; private set; }

    public ApplicationSubmitted? SubmittedEvent { get; private set; }

    public ApplicationStatusChanged? WithdrawEvent { get; private set; }

    /// <summary>What <see cref="WithdrawAsync"/> reports — set false to simulate losing a concurrent transition.</summary>
    public bool WithdrawResult { get; set; } = true;

    public ApplicationStatus? AdvanceExpected { get; private set; }

    public ApplicationStatus? AdvanceTarget { get; private set; }

    public ApplicationStatusChanged? AdvanceEvent { get; private set; }

    /// <summary>What <see cref="AdvanceAsync"/> reports — set false to simulate losing a concurrent transition.</summary>
    public bool AdvanceResult { get; set; } = true;

    public Guid? CloseJobId { get; private set; }

    public Guid? CloseMessageId { get; private set; }

    public ApplicationStatus? CloseTarget { get; private set; }

    public Func<Application, ApplicationStatusChanged>? CloseBuildEvent { get; private set; }

    public int CloseResult { get; set; }

    public Task<IReadOnlyList<ApplicationSummaryServiceModel>> ListByCandidateAsync(Guid candidateId, CancellationToken cancellationToken = default) =>
        Task.FromResult(ListResult);

    public Task<Application?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(GetResult);

    public Task<Application> SubmitAsync(Application application, ApplicationSubmitted @event, CancellationToken cancellationToken = default)
    {
        SubmittedApplication = application;
        SubmittedEvent = @event;
        return Task.FromResult(application);
    }

    public Task<bool> WithdrawAsync(Guid id, ApplicationStatusChanged @event, CancellationToken cancellationToken = default)
    {
        WithdrawEvent = @event;
        return Task.FromResult(WithdrawResult);
    }

    public Task<bool> AdvanceAsync(Guid id, ApplicationStatus expected, ApplicationStatus target, ApplicationStatusChanged @event, CancellationToken cancellationToken = default)
    {
        AdvanceExpected = expected;
        AdvanceTarget = target;
        AdvanceEvent = @event;
        return Task.FromResult(AdvanceResult);
    }

    public Task<int> CloseOpenApplicationsForJobAsync(
        Guid jobId,
        Guid messageId,
        ApplicationStatus target,
        Func<Application, ApplicationStatusChanged> buildEvent,
        CancellationToken cancellationToken = default)
    {
        CloseJobId = jobId;
        CloseMessageId = messageId;
        CloseTarget = target;
        CloseBuildEvent = buildEvent;
        return Task.FromResult(CloseResult);
    }
}
