using JobBoard.Applications.Core.Managers.Models.ServiceModels;
using JobBoard.Applications.Core.Managers.Models.ViewModels;
using JobBoard.Contracts;

namespace JobBoard.Applications.Core.Business;

/// <summary>
/// Domain rules, translation, and the decision to emit an event. Reads map entity → service model;
/// submit translates the view model → domain; withdraw and advance apply the lifecycle rules, build the
/// <c>ApplicationStatusChanged</c> event, and hand it to the data layer. The <c>JobClosed</c> reaction
/// closes a job's active applications, emitting one event each. Depends only on
/// <see cref="Data.IApplicationDataLayer"/>.
/// </summary>
public interface IApplicationBusiness
{
    Task<IReadOnlyList<ApplicationSummaryServiceModel>> ListByCandidateAsync(Guid candidateId, CancellationToken cancellationToken = default);

    Task<ApplicationDetailServiceModel?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ApplicationDetailServiceModel> SubmitAsync(SubmitApplicationViewModel viewModel, CancellationToken cancellationToken = default);

    Task<ApplicationDetailServiceModel> WithdrawAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ApplicationDetailServiceModel> AdvanceAsync(Guid id, AdvanceApplicationStatusViewModel viewModel, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reacts to a job closing: moves every active application for the job to <c>Rejected</c> and publishes
    /// an <c>ApplicationStatusChanged</c> per application. Idempotency is handled downstream (the inbox).
    /// </summary>
    Task HandleJobClosedAsync(JobClosed @event, CancellationToken cancellationToken = default);
}
