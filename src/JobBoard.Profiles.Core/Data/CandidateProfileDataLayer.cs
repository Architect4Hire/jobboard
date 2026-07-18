using JobBoard.Profiles.Core.Managers.Models.Domain;
using JobBoard.Shared.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace JobBoard.Profiles.Core.Data;

/// <inheritdoc cref="ICandidateProfileDataLayer"/>
public sealed class CandidateProfileDataLayer : ICandidateProfileDataLayer
{
    private readonly ICandidateProfileRepository _repository;

    public CandidateProfileDataLayer(ICandidateProfileRepository repository) => _repository = repository;

    // A single self-contained read — straight pass-through, no transaction needed.
    public Task<CandidateProfile?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        _repository.GetAsync(id, cancellationToken);

    public async Task<CandidateProfile> UpsertAsync(CandidateProfile incoming, CancellationToken cancellationToken = default)
    {
        try
        {
            // The repository stages the insert-or-update; the transaction is what SaveChanges/commits it.
            return await _repository.ExecuteInTransactionAsync(
                token => _repository.UpsertAsync(incoming, token),
                cancellationToken);
        }
        catch (DbUpdateException ex) when (CandidateProfileRepository.IsDuplicateKeyViolation(ex))
        {
            // Two concurrent first-time upserts for this owner both inserted; surface a retryable
            // conflict so a retry finds the now-committed row and updates it.
            throw new DomainException(
                "candidate_profile.conflict",
                "The profile was just created by another request. Please retry.",
                StatusCodes.Status409Conflict);
        }
    }
}
