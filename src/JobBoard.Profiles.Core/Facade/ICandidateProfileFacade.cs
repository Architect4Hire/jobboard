using JobBoard.Profiles.Core.Managers.Models.ServiceModels;
using JobBoard.Profiles.Core.Managers.Models.ViewModels;

namespace JobBoard.Profiles.Core.Facade;

/// <summary>
/// The boundary the candidate-profile controller calls: validates the upsert view model, then delegates
/// to <see cref="Business.ICandidateProfileBusiness"/>. No mapping, EF, or caching here.
/// </summary>
public interface ICandidateProfileFacade
{
    Task<CandidateProfileServiceModel?> GetAsync(Guid candidateId, CancellationToken cancellationToken = default);

    Task<CandidateProfileServiceModel> UpsertAsync(Guid candidateId, UpsertCandidateProfileViewModel viewModel, CancellationToken cancellationToken = default);
}
