using JobBoard.Profiles.Core.Business;
using JobBoard.Profiles.Core.Managers.Models.ServiceModels;
using JobBoard.Profiles.Core.Managers.Models.ViewModels;
using JobBoard.Profiles.Core.Storage;

namespace JobBoard.Profiles.Tests.Fakes;

/// <summary>
/// Hand-rolled <see cref="ICandidateProfileBusiness"/> for facade tests. Records whether upsert / résumé
/// upload were reached (so a test can prove validation short-circuits before business) and returns a
/// configured result.
/// </summary>
public sealed class FakeCandidateProfileBusiness : ICandidateProfileBusiness
{
    public CandidateProfileServiceModel UpsertResult { get; init; } = default!;

    public int UpsertCallCount { get; private set; }

    public int UploadResumeCallCount { get; private set; }

    public Task<CandidateProfileServiceModel?> GetAsync(Guid candidateId, CancellationToken cancellationToken = default) =>
        Task.FromResult<CandidateProfileServiceModel?>(UpsertResult);

    public Task<CandidateProfileServiceModel> UpsertAsync(Guid candidateId, UpsertCandidateProfileViewModel viewModel, CancellationToken cancellationToken = default)
    {
        UpsertCallCount++;
        return Task.FromResult(UpsertResult);
    }

    public Task<CandidateProfileServiceModel> UploadResumeAsync(Guid candidateId, Stream content, string contentType, string fileName, CancellationToken cancellationToken = default)
    {
        UploadResumeCallCount++;
        return Task.FromResult(UpsertResult);
    }

    public Task<CandidateResumeFile?> GetResumeAsync(Guid candidateId, CancellationToken cancellationToken = default) =>
        Task.FromResult<CandidateResumeFile?>(null);

    public Task<CandidateProfileServiceModel?> DeleteResumeAsync(Guid candidateId, CancellationToken cancellationToken = default) =>
        Task.FromResult<CandidateProfileServiceModel?>(UpsertResult);
}
