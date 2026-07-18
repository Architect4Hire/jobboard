using JobBoard.Profiles.Core.Managers.Models.Domain;
using JobBoard.Shared.Persistence;

namespace JobBoard.Profiles.Core.Data;

/// <summary>
/// Data-only seam for employer profiles. Extends <see cref="IRepository"/> so the data layer can run the
/// upsert inside a transaction via <see cref="IRepository.ExecuteInTransactionAsync{T}"/>.
/// </summary>
public interface IEmployerProfileRepository : IRepository
{
    /// <summary>Loads the profile for this employer id, untracked. Null if none exists.</summary>
    Task<EmployerProfile?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stages a create-or-replace for the profile keyed by <paramref name="incoming"/>.Id: inserts when
    /// absent, otherwise copies the incoming values onto the tracked row.
    /// </summary>
    Task<EmployerProfile> UpsertAsync(EmployerProfile incoming, CancellationToken cancellationToken = default);
}
