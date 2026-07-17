using JobBoard.Identity.Core.Data;
using JobBoard.Identity.Core.Managers.Models.Domain;

namespace JobBoard.Identity.Tests.Fakes;

/// <summary>
/// Hand-rolled <see cref="IAccountDataLayer"/> for business-layer tests. Returns configured values and
/// captures what business handed down — the email it looked up and the account it tried to register — so
/// a test can assert the VM→domain translation, email normalization, and credential flow without a database.
/// </summary>
public sealed class FakeAccountDataLayer : IAccountDataLayer
{
    public Account? GetByEmailResult { get; set; }

    public string? LookedUpEmail { get; private set; }

    public Account? RegisteredAccount { get; private set; }

    public Task<Account?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        LookedUpEmail = email;
        return Task.FromResult(GetByEmailResult);
    }

    public Task<Account> RegisterAsync(Account account, CancellationToken cancellationToken = default)
    {
        RegisteredAccount = account;
        return Task.FromResult(account);
    }
}
