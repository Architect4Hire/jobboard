using JobBoard.Profiles.Core.Managers.Models.Domain;
using JobBoard.Shared.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace JobBoard.Profiles.Core.Data;

/// <inheritdoc cref="IEmployerProfileDataLayer"/>
public sealed class EmployerProfileDataLayer : IEmployerProfileDataLayer
{
    private readonly IEmployerProfileRepository _repository;

    public EmployerProfileDataLayer(IEmployerProfileRepository repository) => _repository = repository;

    public Task<EmployerProfile?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        _repository.GetAsync(id, cancellationToken);

    public async Task<EmployerProfile> UpsertAsync(EmployerProfile incoming, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _repository.ExecuteInTransactionAsync(
                token => _repository.UpsertAsync(incoming, token),
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
