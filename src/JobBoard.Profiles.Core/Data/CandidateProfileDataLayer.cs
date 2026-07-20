using JobBoard.Contracts;
using JobBoard.Profiles.Core.Managers.Models.Domain;
using JobBoard.Shared.Errors;
using JobBoard.Shared.Messaging;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace JobBoard.Profiles.Core.Data;

/// <inheritdoc cref="ICandidateProfileDataLayer"/>
public sealed class CandidateProfileDataLayer : ICandidateProfileDataLayer
{
    private readonly ICandidateProfileRepository _repository;
    private readonly IOutbox _outbox;

    public CandidateProfileDataLayer(ICandidateProfileRepository repository, IOutbox outbox)
    {
        _repository = repository;
        _outbox = outbox;
    }

    // A single self-contained read — straight pass-through, no transaction needed.
    public Task<CandidateProfile?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        _repository.GetAsync(id, cancellationToken);

    public async Task<CandidateProfile> UpsertAsync(CandidateProfile incoming, ProfileUpdated updated, CancellationToken cancellationToken = default)
    {
        try
        {
            // The repository stages the insert-or-update and the ProfileUpdated outbox row commits in the
            // same transaction, so the event ships iff the write commits.
            return await _repository.ExecuteInTransactionAsync(
                async token =>
                {
                    var saved = await _repository.UpsertAsync(incoming, token);
                    await _outbox.EnqueueAsync(updated, token);
                    return saved;
                },
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
