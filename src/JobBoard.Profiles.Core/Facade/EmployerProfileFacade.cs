using FluentValidation;
using JobBoard.Profiles.Core.Business;
using JobBoard.Profiles.Core.Managers.Models.ServiceModels;
using JobBoard.Profiles.Core.Managers.Models.ViewModels;

namespace JobBoard.Profiles.Core.Facade;

/// <inheritdoc cref="IEmployerProfileFacade"/>
public sealed class EmployerProfileFacade : IEmployerProfileFacade
{
    private readonly IEmployerProfileBusiness _business;
    private readonly IValidator<UpsertEmployerProfileViewModel> _upsertValidator;

    public EmployerProfileFacade(
        IEmployerProfileBusiness business,
        IValidator<UpsertEmployerProfileViewModel> upsertValidator)
    {
        _business = business;
        _upsertValidator = upsertValidator;
    }

    public Task<EmployerProfileServiceModel?> GetAsync(Guid employerId, CancellationToken cancellationToken = default) =>
        _business.GetAsync(employerId, cancellationToken);

    public async Task<EmployerProfileServiceModel> UpsertAsync(Guid employerId, UpsertEmployerProfileViewModel viewModel, CancellationToken cancellationToken = default)
    {
        await _upsertValidator.ValidateAndThrowAsync(viewModel, cancellationToken);
        return await _business.UpsertAsync(employerId, viewModel, cancellationToken);
    }
}
