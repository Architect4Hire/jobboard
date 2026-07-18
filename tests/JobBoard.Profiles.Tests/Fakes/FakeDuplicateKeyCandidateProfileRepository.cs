using JobBoard.Profiles.Core.Data;
using JobBoard.Profiles.Core.Managers.Models.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobBoard.Profiles.Tests.Fakes;

/// <summary>
/// Hand-rolled <see cref="ICandidateProfileRepository"/> whose <see cref="UpsertAsync"/> throws a
/// primary-key <see cref="DbUpdateException"/> — simulating the concurrent-first-insert race — so a test
/// can prove the data layer maps it to a retryable 409 <c>DomainException</c>. Its
/// <c>ExecuteInTransactionAsync</c> simply runs the operation (no real transaction), matching how the
/// data layer invokes it.
/// </summary>
public sealed class FakeDuplicateKeyCandidateProfileRepository : ICandidateProfileRepository
{
    public Task<CandidateProfile?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult<CandidateProfile?>(null);

    public Task<CandidateProfile> UpsertAsync(CandidateProfile incoming, CancellationToken cancellationToken = default) =>
        throw new DbUpdateException("insert failed", new Exception("UNIQUE constraint failed: CandidateProfiles.Id"));

    public Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default) =>
        operation(cancellationToken);

    public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default) =>
        operation(cancellationToken);
}
