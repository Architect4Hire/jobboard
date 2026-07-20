using JobBoard.Contracts;
using JobBoard.Profiles.Core.Data;
using JobBoard.Profiles.Core.Managers.Models.Domain;

namespace JobBoard.Profiles.Tests.Fakes;

/// <summary>
/// Hand-rolled <see cref="ICandidateProfileDataLayer"/> for business-layer tests. Returns configured
/// values and captures the entity business handed down (owner id + translated fields) and the
/// <see cref="ProfileUpdated"/> event it built, so a test can assert the VM→domain translation and the
/// audit thread without a database.
/// </summary>
public sealed class FakeCandidateProfileDataLayer : ICandidateProfileDataLayer
{
    public CandidateProfile? GetResult { get; set; }

    public CandidateProfile? Upserted { get; private set; }

    public ProfileUpdated? UpdatedEvent { get; private set; }

    public Task<CandidateProfile?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(GetResult);

    public Task<CandidateProfile> UpsertAsync(CandidateProfile incoming, ProfileUpdated updated, CancellationToken cancellationToken = default)
    {
        Upserted = incoming;
        UpdatedEvent = updated;
        return Task.FromResult(incoming);
    }
}
