using JobBoard.Identity.Core.Managers.Models.Domain;
using JobBoard.Shared.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace JobBoard.Identity.Core.Data;

/// <inheritdoc cref="IAccountDataLayer"/>
public sealed class AccountDataLayer : IAccountDataLayer
{
    private readonly IAccountRepository _repository;

    public AccountDataLayer(IAccountRepository repository) => _repository = repository;

    // A single self-contained read — straight pass-through, no transaction needed.
    public Task<Account?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        _repository.GetByEmailAsync(email, cancellationToken);

    public async Task<Account> RegisterAsync(Account account, CancellationToken cancellationToken = default)
    {
        try
        {
            // The repository only stages the insert; the transaction is what SaveChanges/commits it.
            return await _repository.ExecuteInTransactionAsync(
                token => _repository.AddAsync(account, token),
                cancellationToken);
        }
        catch (DbUpdateException ex) when (AccountRepository.IsDuplicateEmailViolation(ex))
        {
            // The unique index is the authoritative guard: a concurrent registration inserted this email
            // between our read and our insert. Surface a conflict rather than a 500.
            throw new DomainException(
                "account.email_taken",
                "An account with this email already exists.",
                StatusCodes.Status409Conflict);
        }
    }
}
