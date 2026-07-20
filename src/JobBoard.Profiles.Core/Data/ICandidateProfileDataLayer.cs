using JobBoard.Contracts;
using JobBoard.Profiles.Core.Managers.Models.Domain;

namespace JobBoard.Profiles.Core.Data;

/// <summary>
/// Composes the candidate-profile repository into whole operations and owns the transaction boundary. The
/// read passes straight through; the upsert commits inside a transaction — enqueuing the
/// <see cref="ProfileUpdated"/> outbox row in the same unit so the event ships iff the write commits — and
/// maps a concurrent-insert primary-key collision to a retryable conflict. Depends on
/// <see cref="ICandidateProfileRepository"/> and <c>IOutbox</c>; holds no <c>DbContext</c>.
/// </summary>
public interface ICandidateProfileDataLayer
{
    Task<CandidateProfile?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<CandidateProfile> UpsertAsync(CandidateProfile incoming, ProfileUpdated updated, CancellationToken cancellationToken = default);
}
