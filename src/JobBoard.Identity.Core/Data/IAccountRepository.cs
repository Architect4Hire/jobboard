using JobBoard.Identity.Core.Managers.Models.Domain;
using JobBoard.Shared.Persistence;

namespace JobBoard.Identity.Core.Data;

/// <summary>
/// Data-only seam for the Identity context. Extends <see cref="IRepository"/> so the data layer can run
/// the insert inside a transaction via <see cref="IRepository.ExecuteInTransactionAsync{T}"/>. No rules,
/// hashing, or token logic here — just queries and staging against the accounts store.
/// </summary>
public interface IAccountRepository : IRepository
{
    /// <summary>Loads the account with this (normalized) email, untracked. Null if none exists.</summary>
    Task<Account?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Stages a new account for insert. The caller runs it inside a transaction so it persists.</summary>
    Task<Account> AddAsync(Account account, CancellationToken cancellationToken = default);
}
