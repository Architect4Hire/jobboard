using JobBoard.Contracts;
using JobBoard.Profiles.Core.Managers.Models.Domain;
using JobBoard.Shared.Errors;
using JobBoard.Shared.Messaging;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace JobBoard.Profiles.Core.Data;

/// <inheritdoc cref="IEmployerProfileDataLayer"/>
public sealed class EmployerProfileDataLayer : IEmployerProfileDataLayer
{
    private readonly IEmployerProfileRepository _repository;
    private readonly IOutbox _outbox;

    public EmployerProfileDataLayer(IEmployerProfileRepository repository, IOutbox outbox)
    {
        _repository = repository;
        _outbox = outbox;
    }

    public Task<EmployerProfile?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        _repository.GetAsync(id, cancellationToken);

    public async Task<EmployerProfile> UpsertAsync(EmployerProfile incoming, ProfileUpdated updated, CancellationToken cancellationToken = default)
    {
        try
        {
            // The upsert and the ProfileUpdated outbox row commit in one transaction.
            return await _repository.ExecuteInTransactionAsync(
                async token =>
                {
                    var saved = await _repository.UpsertAsync(incoming, token);
                    await _outbox.EnqueueAsync(updated, token);
                    return saved;
                },
                cancellationToken);
        }
        catch (DbUpdateException ex) when (EmployerProfileRepository.IsDuplicateKeyViolation(ex))
        {
            throw new DomainException(
                "employer_profile.conflict",
                "The profile was just created by another request. Please retry.",
                StatusCodes.Status409Conflict);
        }
    }
}
