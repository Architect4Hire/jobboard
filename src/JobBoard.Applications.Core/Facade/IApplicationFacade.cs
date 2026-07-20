using JobBoard.Applications.Core.Managers.Models.ServiceModels;
using JobBoard.Applications.Core.Managers.Models.ViewModels;
using JobBoard.Contracts;

namespace JobBoard.Applications.Core.Facade;

/// <summary>
/// The boundary the controller and the <c>JobClosed</c> consumer call. Validates write view models, then
/// delegates to the business layer; reads and the event reaction pass straight through. No mapping, EF, or
/// bus here (and no cache — this service caches nothing today).
/// </summary>
public interface IApplicationFacade
{
    Task<IReadOnlyList<ApplicationSummaryServiceModel>> ListByCandidateAsync(Guid candidateId, CancellationToken cancellationToken = default);

    Task<ApplicationDetailServiceModel?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ApplicationDetailServiceModel> SubmitAsync(SubmitApplicationViewModel viewModel, CancellationToken cancellationToken = default);

    Task<ApplicationDetailServiceModel> WithdrawAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ApplicationDetailServiceModel> AdvanceAsync(Guid id, AdvanceApplicationStatusViewModel viewModel, CancellationToken cancellationToken = default);

    /// <summary>Entry point for the <c>JobClosed</c> consumer — an integration event, not a view model, so no validation.</summary>
    Task HandleJobClosedAsync(JobClosed @event, CancellationToken cancellationToken = default);

    /// <summary>Entry point for the <c>JobPosted</c> consumer — an integration event, not a view model, so no validation.</summary>
    Task HandleJobPostedAsync(JobPosted @event, CancellationToken cancellationToken = default);

    /// <summary>Entry point for the <c>EmployerProfileChanged</c> consumer — an integration event, not a view model, so no validation.</summary>
    Task HandleEmployerProfileChangedAsync(EmployerProfileChanged @event, CancellationToken cancellationToken = default);

    /// <summary>The authenticated caller's own applications, enriched with job title and employer name.</summary>
    Task<IReadOnlyList<ApplicationHistoryServiceModel>> ListMineAsync(CancellationToken cancellationToken = default);
}
