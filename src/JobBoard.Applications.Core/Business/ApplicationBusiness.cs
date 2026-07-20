using JobBoard.Applications.Core.Data;
using JobBoard.Applications.Core.Managers.Mappers;
using JobBoard.Applications.Core.Managers.Models.Domain;
using JobBoard.Applications.Core.Managers.Models.ServiceModels;
using JobBoard.Applications.Core.Managers.Models.ViewModels;
using JobBoard.Contracts;
using JobBoard.Shared.Errors;
using JobBoard.Shared.Requests;
using Microsoft.AspNetCore.Http;

namespace JobBoard.Applications.Core.Business;

/// <summary>
/// Applies the application lifecycle rules, translates between the boundary shapes, and builds the
/// integration events. It decides <i>whether</i> a transition is legal and builds the event; the data
/// layer enforces it as a conditional write and ships the event atomically.
/// </summary>
public sealed class ApplicationBusiness : IApplicationBusiness
{
    // Legal advance transitions. Withdrawal is a separate flow; job-close (the consumer) rejects directly.
    private static readonly HashSet<(ApplicationStatus From, ApplicationStatus To)> AllowedAdvances =
    [
        (ApplicationStatus.Submitted, ApplicationStatus.Reviewed),
        (ApplicationStatus.Reviewed, ApplicationStatus.Offered),
        (ApplicationStatus.Reviewed, ApplicationStatus.Rejected),
        (ApplicationStatus.Offered, ApplicationStatus.Rejected),
    ];

    private static readonly ApplicationStatus[] ActiveStatuses =
        [ApplicationStatus.Submitted, ApplicationStatus.Reviewed, ApplicationStatus.Offered];

    private readonly IApplicationDataLayer _dataLayer;

    // Only valid on request-initiated paths (Submit/Withdraw/Advance) via RootThread(). Consumer-initiated
    // paths (e.g. HandleJobClosedAsync) run with no HTTP request, so this is unpopulated there — inherit the
    // thread from the consumed event with FollowOnThread() instead, or you stamp an empty thread (SCRUB A3).
    private readonly IRequestContext _requestContext;

    public ApplicationBusiness(IApplicationDataLayer dataLayer, IRequestContext requestContext)
    {
        _dataLayer = dataLayer;
        _requestContext = requestContext;
    }

    public Task<IReadOnlyList<ApplicationSummaryServiceModel>> ListByCandidateAsync(
        Guid candidateId,
        CancellationToken cancellationToken = default) =>
        _dataLayer.ListByCandidateAsync(candidateId, cancellationToken);

    public async Task<ApplicationDetailServiceModel?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var application = await _dataLayer.GetAsync(id, cancellationToken);
        return application?.ToDetailServiceModel();
    }

    public async Task<ApplicationDetailServiceModel> SubmitAsync(
        SubmitApplicationViewModel viewModel,
        CancellationToken cancellationToken = default)
    {
        var application = viewModel.ToEntity();
        var @event = application.ToApplicationSubmitted(_requestContext.RootThread());

        var saved = await _dataLayer.SubmitAsync(application, @event, cancellationToken);
        return saved.ToDetailServiceModel();
    }

    public async Task<ApplicationDetailServiceModel> WithdrawAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var application = await _dataLayer.GetAsync(id, cancellationToken)
            ?? throw new DomainException("application.not_found", $"Application '{id}' was not found.", StatusCodes.Status404NotFound);

        if (!ActiveStatuses.Contains(application.Status))
        {
            throw new DomainException("application.not_active", $"Application '{id}' is not active and cannot be withdrawn.");
        }

        var @event = application.ToStatusChanged(application.Status, ApplicationStatus.Withdrawn, _requestContext.RootThread());

        var withdrawn = await _dataLayer.WithdrawAsync(id, @event, cancellationToken);
        if (!withdrawn)
        {
            throw new DomainException("application.not_active", $"Application '{id}' changed before it could be withdrawn.");
        }

        application.Status = ApplicationStatus.Withdrawn;
        application.StatusChangedOnUtc = DateTime.UtcNow;
        return application.ToDetailServiceModel();
    }

    public async Task<ApplicationDetailServiceModel> AdvanceAsync(
        Guid id,
        AdvanceApplicationStatusViewModel viewModel,
        CancellationToken cancellationToken = default)
    {
        var application = await _dataLayer.GetAsync(id, cancellationToken)
            ?? throw new DomainException("application.not_found", $"Application '{id}' was not found.", StatusCodes.Status404NotFound);

        var target = viewModel.TargetStatus;
        if (!AllowedAdvances.Contains((application.Status, target)))
        {
            throw new DomainException(
                "application.invalid_transition",
                $"An application in '{application.Status}' cannot advance to '{target}'.");
        }

        var @event = application.ToStatusChanged(application.Status, target, _requestContext.RootThread());

        var advanced = await _dataLayer.AdvanceAsync(id, application.Status, target, @event, cancellationToken);
        if (!advanced)
        {
            throw new DomainException(
                "application.invalid_transition",
                $"Application '{id}' changed before it could advance to '{target}'.");
        }

        application.Status = target;
        application.StatusChangedOnUtc = DateTime.UtcNow;
        return application.ToDetailServiceModel();
    }

    public Task HandleJobClosedAsync(JobClosed @event, CancellationToken cancellationToken = default)
    {
        // Consumer-initiated: there is no HTTP request here, so the thread is inherited from the consumed
        // event — correlation and actor carry over, and JobClosed is the direct cause of each rejection.
        var thread = @event.FollowOnThread();
        return _dataLayer.CloseOpenApplicationsForJobAsync(
            @event.JobId,
            @event.Id,
            ApplicationStatus.Rejected,
            // Each snapshot entity carries its pre-close status as the event's "from".
            application => application.ToStatusChanged(application.Status, ApplicationStatus.Rejected, thread),
            cancellationToken);
    }

    public Task HandleJobPostedAsync(JobPosted @event, CancellationToken cancellationToken = default) =>
        _dataLayer.UpsertJobReferenceAsync(@event.JobId, @event.Id, @event.Title, @event.EmployerId, cancellationToken);

    public Task HandleEmployerProfileChangedAsync(EmployerProfileChanged @event, CancellationToken cancellationToken = default) =>
        _dataLayer.UpsertEmployerReferenceAsync(@event.EmployerId, @event.Id, @event.CompanyName, cancellationToken);

    public Task<IReadOnlyList<ApplicationHistoryServiceModel>> ListMineAsync(CancellationToken cancellationToken = default)
    {
        // The gateway's "authenticated" policy already guards every /applications/** route (gateway.md), so
        // a missing actor here means the request never traversed the gateway — defense in depth, not the
        // real enforcement.
        var candidateId = _requestContext.ActorId
            ?? throw new DomainException(
                "application.unauthenticated", "No authenticated candidate on the request.", StatusCodes.Status401Unauthorized);

        return _dataLayer.ListMineAsync(candidateId, cancellationToken);
    }
}
