using JobBoard.Contracts;
using JobBoard.Identity.Core.Data;
using JobBoard.Identity.Core.Managers.Models.Domain;

namespace JobBoard.Identity.Tests.Fakes;

/// <summary>
/// Hand-rolled <see cref="IAccountDataLayer"/> for business-layer tests. Returns configured values and
/// captures what business handed down — the email it looked up, the account it tried to register, and the
/// <see cref="AccountCreated"/> event it built — so a test can assert the VM→domain translation, email
/// normalization, credential flow, and the audit thread without a database.
/// </summary>
public sealed class FakeAccountDataLayer : IAccountDataLayer
{
    public Account? GetByEmailResult { get; set; }

    public string? LookedUpEmail { get; private set; }

    public Account? RegisteredAccount { get; private set; }

    public AccountCreated? CreatedEvent { get; private set; }

    public LoggedIn? LoggedInEvent { get; private set; }

    public LoginFailed? LoginFailedEvent { get; private set; }

    public Task<Account?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        LookedUpEmail = email;
        return Task.FromResult(GetByEmailResult);
    }

    public Task<Account> RegisterAsync(Account account, AccountCreated created, CancellationToken cancellationToken = default)
    {
        RegisteredAccount = account;
        CreatedEvent = created;
        return Task.FromResult(account);
    }

    public Task RecordLoginAsync(LoggedIn loggedIn, CancellationToken cancellationToken = default)
    {
        LoggedInEvent = loggedIn;
        return Task.CompletedTask;
    }

    public Task RecordLoginFailedAsync(LoginFailed loginFailed, CancellationToken cancellationToken = default)
    {
        LoginFailedEvent = loginFailed;
        return Task.CompletedTask;
    }
}
