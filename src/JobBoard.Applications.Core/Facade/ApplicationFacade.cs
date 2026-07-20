using FluentValidation;
using JobBoard.Applications.Core.Business;
using JobBoard.Applications.Core.Managers.Models.ServiceModels;
using JobBoard.Applications.Core.Managers.Models.ViewModels;
using JobBoard.Contracts;

namespace JobBoard.Applications.Core.Facade;

/// <summary>
/// Validates the write view models (the global handler maps a thrown <c>ValidationException</c> to a 400
/// with field detail), then delegates to the business layer. Reads and the <c>JobClosed</c> reaction pass
/// straight through.
/// </summary>
public sealed class ApplicationFacade : IApplicationFacade
{
    private readonly IApplicationBusiness _business;
    private readonly IValidator<SubmitApplicationViewModel> _submitValidator;
    private readonly IValidator<AdvanceApplicationStatusViewModel> _advanceValidator;

    public ApplicationFacade(
        IApplicationBusiness business,
        IValidator<SubmitApplicationViewModel> submitValidator,
        IValidator<AdvanceApplicationStatusViewModel> advanceValidator)
    {
        _business = business;
        _submitValidator = submitValidator;
        _advanceValidator = advanceValidator;
    }

    public Task<IReadOnlyList<ApplicationSummaryServiceModel>> ListByCandidateAsync(
        Guid candidateId,
        CancellationToken cancellationToken = default) =>
        _business.ListByCandidateAsync(candidateId, cancellationToken);

    public Task<ApplicationDetailServiceModel?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        _business.GetAsync(id, cancellationToken);

    public async Task<ApplicationDetailServiceModel> SubmitAsync(
        SubmitApplicationViewModel viewModel,
        CancellationToken cancellationToken = default)
    {
        await _submitValidator.ValidateAndThrowAsync(viewModel, cancellationToken);
        return await _business.SubmitAsync(viewModel, cancellationToken);
    }

    public Task<ApplicationDetailServiceModel> WithdrawAsync(Guid id, CancellationToken cancellationToken = default) =>
        _business.WithdrawAsync(id, cancellationToken);

    public async Task<ApplicationDetailServiceModel> AdvanceAsync(
        Guid id,
        AdvanceApplicationStatusViewModel viewModel,
        CancellationToken cancellationToken = default)
    {
        await _advanceValidator.ValidateAndThrowAsync(viewModel, cancellationToken);
        return await _business.AdvanceAsync(id, viewModel, cancellationToken);
    }

    public Task HandleJobClosedAsync(JobClosed @event, CancellationToken cancellationToken = default) =>
        _business.HandleJobClosedAsync(@event, cancellationToken);

    public Task HandleJobPostedAsync(JobPosted @event, CancellationToken cancellationToken = default) =>
        _business.HandleJobPostedAsync(@event, cancellationToken);

    public Task HandleEmployerProfileChangedAsync(EmployerProfileChanged @event, CancellationToken cancellationToken = default) =>
        _business.HandleEmployerProfileChangedAsync(@event, cancellationToken);

    public Task<IReadOnlyList<ApplicationHistoryServiceModel>> ListMineAsync(CancellationToken cancellationToken = default) =>
        _business.ListMineAsync(cancellationToken);
}
