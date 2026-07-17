using JobBoard.Identity.Core.Managers.Models.Domain;

namespace JobBoard.Identity.Core.Data;

/// <summary>
/// Composes the repository into whole operations and owns the transaction boundary. The register insert
/// runs inside a transaction (so the staged row actually commits) and a duplicate-email collision is
/// mapped to a conflict; the lookup passes straight through. Depends only on
/// <see cref="IAccountRepository"/>; holds no <c>DbContext</c>. No <c>IOutbox</c> — Identity emits no events.
/// </summary>
public interface IAccountDataLayer
{
    Task<Account?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a new account. Throws a conflict <c>DomainException</c> if the email is already taken
    /// (including a concurrent registration that won the race).
    /// </summary>
    Task<Account> RegisterAsync(Account account, CancellationToken cancellationToken = default);
}
