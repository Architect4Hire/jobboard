using JobBoard.Profiles.Core.Managers.Models.ServiceModels;
using JobBoard.Profiles.Core.Managers.Models.ViewModels;
using JobBoard.Profiles.Core.Storage;

namespace JobBoard.Profiles.Core.Facade;

/// <summary>
/// The boundary the candidate-profile controller calls: validates the upsert view model and the uploaded
/// résumé (size/type), then delegates to <see cref="Business.ICandidateProfileBusiness"/>. No mapping, EF,
/// blob I/O, or caching here — just the validation seam.
/// </summary>
public interface ICandidateProfileFacade
{
    Task<CandidateProfileServiceModel?> GetAsync(Guid candidateId, CancellationToken cancellationToken = default);

    Task<CandidateProfileServiceModel> UpsertAsync(Guid candidateId, UpsertCandidateProfileViewModel viewModel, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates and stores <paramref name="content"/> as the candidate's résumé (replacing any existing
    /// one), returning the updated profile. Throws a 400 <c>DomainException</c> for an oversized or
    /// unsupported file, or a 404 when the candidate has no profile to attach it to.
    /// </summary>
    Task<CandidateProfileServiceModel> UploadResumeAsync(
        Guid candidateId,
        Stream content,
        long length,
        string contentType,
        string fileName,
        CancellationToken cancellationToken = default);

    /// <summary>The candidate's résumé ready to stream, or <c>null</c> when none is on file.</summary>
    Task<CandidateResumeFile?> GetResumeAsync(Guid candidateId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the candidate's résumé (blob + pointers) and returns the updated profile, or <c>null</c>
    /// when the candidate has no profile.
    /// </summary>
    Task<CandidateProfileServiceModel?> DeleteResumeAsync(Guid candidateId, CancellationToken cancellationToken = default);
}
