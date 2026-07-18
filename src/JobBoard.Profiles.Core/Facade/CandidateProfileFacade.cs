using FluentValidation;
using JobBoard.Profiles.Core.Business;
using JobBoard.Profiles.Core.Managers.Models.ServiceModels;
using JobBoard.Profiles.Core.Managers.Models.ViewModels;

namespace JobBoard.Profiles.Core.Facade;

/// <inheritdoc cref="ICandidateProfileFacade"/>
/// <remarks>No caching: profiles are read straight through and written on upsert; the facade owns the
/// validation seam and delegates.</remarks>
public sealed class CandidateProfileFacade : ICandidateProfileFacade
{
    private readonly ICandidateProfileBusiness _business;
    private readonly IValidator<UpsertCandidateProfileViewModel> _upsertValidator;

    public CandidateProfileFacade(
        ICandidateProfileBusiness business,
        IValidator<UpsertCandidateProfileViewModel> upsertValidator)
    {
        _business = business;
        _upsertValidator = upsertValidator;
    }

    public Task<CandidateProfileServiceModel?> GetAsync(Guid candidateId, CancellationToken cancellationToken = default) =>
        _business.GetAsync(candidateId, cancellationToken);

    public async Task<CandidateProfileServiceModel> UpsertAsync(Guid candidateId, UpsertCandidateProfileViewModel viewModel, CancellationToken cancellationToken = default)
    {
        // The global exception handler maps the thrown ValidationException to a 400 with field detail.
        await _upsertValidator.ValidateAndThrowAsync(viewModel, cancellationToken);
        return await _business.UpsertAsync(candidateId, viewModel, cancellationToken);
    }
}
