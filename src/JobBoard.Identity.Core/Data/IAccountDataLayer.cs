using JobBoard.Contracts;
using JobBoard.Identity.Core.Managers.Models.Domain;

namespace JobBoard.Identity.Core.Data;

/// <summary>
/// Composes the repository into whole operations and owns the transaction boundary. The register insert
/// runs inside a transaction — enqueuing the <see cref="AccountCreated"/> outbox row in the same unit so
/// the event ships iff the account commits — and a duplicate-email collision is mapped to a conflict; the
/// lookup passes straight through. Depends on <see cref="IAccountRepository"/> and <c>IOutbox</c>; holds no
/// <c>DbContext</c>.
/// </summary>
public interface IAccountDataLayer
{
    Task<Account?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a new account and enqueues its <see cref="AccountCreated"/> event to the outbox in the same
    /// transaction. Throws a conflict <c>DomainException</c> if the email is already taken (including a
    /// concurrent registration that won the race) — in which case neither the account nor the event commits.
    /// </summary>
    Task<Account> RegisterAsync(Account account, AccountCreated created, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a successful sign-in by enqueuing the <see cref="LoggedIn"/> event to the outbox in a
    /// transaction. Login mutates no domain state, so the event is the only write.
    /// </summary>
    Task RecordLoginAsync(LoggedIn loggedIn, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a rejected login by enqueuing the <see cref="LoginFailed"/> event to the outbox in a
    /// transaction — persisted before the caller surfaces the 401.
    /// </summary>
    Task RecordLoginFailedAsync(LoginFailed loginFailed, CancellationToken cancellationToken = default);
}
