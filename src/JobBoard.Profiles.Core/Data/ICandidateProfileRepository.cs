using JobBoard.Profiles.Core.Managers.Models.Domain;
using JobBoard.Shared.Persistence;

namespace JobBoard.Profiles.Core.Data;

/// <summary>
/// Data-only seam for candidate profiles. Extends <see cref="IRepository"/> so the data layer can run the
/// upsert inside a transaction via <see cref="IRepository.ExecuteInTransactionAsync{T}"/>. No rules,
/// cache, or validation here.
/// </summary>
public interface ICandidateProfileRepository : IRepository
{
    /// <summary>Loads the profile for this candidate id, untracked. Null if none exists.</summary>
    Task<CandidateProfile?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stages a create-or-replace for the profile keyed by <paramref name="incoming"/>.Id: inserts when
    /// absent, otherwise copies the incoming values onto the tracked row. One self-contained data
    /// operation; the caller runs it inside a transaction so it commits.
    /// </summary>
    Task<CandidateProfile> UpsertAsync(CandidateProfile incoming, CancellationToken cancellationToken = default);
}
