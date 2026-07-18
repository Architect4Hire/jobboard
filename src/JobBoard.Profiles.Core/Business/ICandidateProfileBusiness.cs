using JobBoard.Profiles.Core.Managers.Models.ServiceModels;
using JobBoard.Profiles.Core.Managers.Models.ViewModels;
using JobBoard.Profiles.Core.Storage;

namespace JobBoard.Profiles.Core.Business;

/// <summary>
/// Translation and orchestration for candidate profiles: read maps entity → service model; upsert
/// translates the view model (plus the route-supplied owner id) → domain entity, preserving the uploaded
/// résumé, and maps the persisted entity → service model. The résumé operations move bytes through
/// <see cref="Storage.IResumeStorage"/> and keep the profile row's pointers in step. No events.
/// </summary>
public interface ICandidateProfileBusiness
{
    Task<CandidateProfileServiceModel?> GetAsync(Guid candidateId, CancellationToken cancellationToken = default);

    Task<CandidateProfileServiceModel> UpsertAsync(Guid candidateId, UpsertCandidateProfileViewModel viewModel, CancellationToken cancellationToken = default);

    Task<CandidateProfileServiceModel> UploadResumeAsync(
        Guid candidateId,
        Stream content,
        string contentType,
        string fileName,
        CancellationToken cancellationToken = default);

    Task<CandidateResumeFile?> GetResumeAsync(Guid candidateId, CancellationToken cancellationToken = default);

    Task<CandidateProfileServiceModel?> DeleteResumeAsync(Guid candidateId, CancellationToken cancellationToken = default);
}
