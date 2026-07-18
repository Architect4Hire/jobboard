using JobBoard.Profiles.Core.Managers.Models.Domain;

namespace JobBoard.Profiles.Core.Data;

/// <summary>
/// Composes the candidate-profile repository into whole operations and owns the transaction boundary. The
/// read passes straight through; the upsert commits inside a transaction and maps a concurrent-insert
/// primary-key collision to a retryable conflict. Depends only on
/// <see cref="ICandidateProfileRepository"/>; holds no <c>DbContext</c>. No <c>IOutbox</c> — Profiles
/// emits no events.
/// </summary>
public interface ICandidateProfileDataLayer
{
    Task<CandidateProfile?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<CandidateProfile> UpsertAsync(CandidateProfile incoming, CancellationToken cancellationToken = default);
}
