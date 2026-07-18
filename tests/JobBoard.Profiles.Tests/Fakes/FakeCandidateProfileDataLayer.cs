using JobBoard.Profiles.Core.Data;
using JobBoard.Profiles.Core.Managers.Models.Domain;

namespace JobBoard.Profiles.Tests.Fakes;

/// <summary>
/// Hand-rolled <see cref="ICandidateProfileDataLayer"/> for business-layer tests. Returns configured
/// values and captures the entity business handed down (owner id + translated fields), so a test can
/// assert the VM→domain translation without a database.
/// </summary>
public sealed class FakeCandidateProfileDataLayer : ICandidateProfileDataLayer
{
    public CandidateProfile? GetResult { get; set; }

    public CandidateProfile? Upserted { get; private set; }

    public Task<CandidateProfile?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(GetResult);

    public Task<CandidateProfile> UpsertAsync(CandidateProfile incoming, CancellationToken cancellationToken = default)
    {
        Upserted = incoming;
        return Task.FromResult(incoming);
    }
}
