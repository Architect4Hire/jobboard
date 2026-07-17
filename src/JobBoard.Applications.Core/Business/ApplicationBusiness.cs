using JobBoard.Applications.Core.Data;
using JobBoard.Applications.Core.Managers.Mappers;
using JobBoard.Applications.Core.Managers.Models.Domain;
using JobBoard.Applications.Core.Managers.Models.ServiceModels;
using JobBoard.Applications.Core.Managers.Models.ViewModels;
using JobBoard.Contracts;
using JobBoard.Shared.Errors;
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

    public ApplicationBusiness(IApplicationDataLayer dataLayer) => _dataLayer = dataLayer;

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
        var @event = application.ToApplicationSubmitted();

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

        var @event = application.ToStatusChanged(application.Status, ApplicationStatus.Withdrawn);

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

        var @event = application.ToStatusChanged(application.Status, target);

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

    public Task HandleJobClosedAsync(JobClosed @event, CancellationToken cancellationToken = default) =>
        _dataLayer.CloseOpenApplicationsForJobAsync(
            @event.JobId,
            @event.Id,
            ApplicationStatus.Rejected,
            // Each snapshot entity carries its pre-close status as the event's "from".
            application => application.ToStatusChanged(application.Status, ApplicationStatus.Rejected),
            cancellationToken);
}
