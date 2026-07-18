using JobBoard.Profiles.Core.Business;
using JobBoard.Profiles.Core.Managers.Models.ServiceModels;
using JobBoard.Profiles.Core.Managers.Models.ViewModels;

namespace JobBoard.Profiles.Tests.Fakes;

/// <summary>
/// Hand-rolled <see cref="ICandidateProfileBusiness"/> for facade tests. Records whether upsert was
/// reached (so a test can prove validation short-circuits before business) and returns a configured result.
/// </summary>
public sealed class FakeCandidateProfileBusiness : ICandidateProfileBusiness
{
    public CandidateProfileServiceModel UpsertResult { get; init; } = default!;

    public int UpsertCallCount { get; private set; }

    public Task<CandidateProfileServiceModel?> GetAsync(Guid candidateId, CancellationToken cancellationToken = default) =>
        Task.FromResult<CandidateProfileServiceModel?>(UpsertResult);

    public Task<CandidateProfileServiceModel> UpsertAsync(Guid candidateId, UpsertCandidateProfileViewModel viewModel, CancellationToken cancellationToken = default)
    {
        UpsertCallCount++;
        return Task.FromResult(UpsertResult);
    }
}
