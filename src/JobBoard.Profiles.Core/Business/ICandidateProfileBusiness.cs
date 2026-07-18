using JobBoard.Profiles.Core.Managers.Models.ServiceModels;
using JobBoard.Profiles.Core.Managers.Models.ViewModels;

namespace JobBoard.Profiles.Core.Business;

/// <summary>
/// Translation for candidate profiles: read maps entity → service model; upsert translates the view
/// model (plus the route-supplied owner id) → domain entity and maps the persisted entity → service
/// model. No events. Depends only on <see cref="Data.ICandidateProfileDataLayer"/>.
/// </summary>
public interface ICandidateProfileBusiness
{
    Task<CandidateProfileServiceModel?> GetAsync(Guid candidateId, CancellationToken cancellationToken = default);

    Task<CandidateProfileServiceModel> UpsertAsync(Guid candidateId, UpsertCandidateProfileViewModel viewModel, CancellationToken cancellationToken = default);
}
