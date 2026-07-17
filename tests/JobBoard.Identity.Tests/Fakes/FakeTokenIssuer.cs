using JobBoard.Identity.Core.Managers.Models.Domain;
using JobBoard.Identity.Core.Security;

namespace JobBoard.Identity.Tests.Fakes;

/// <summary>
/// Hand-rolled <see cref="ITokenIssuer"/> for business tests. Captures the account it was asked to issue
/// for and returns a configured <see cref="IssuedToken"/>, so a test can assert a token is issued only on
/// a successful authentication (and for the right account) without real signing.
/// </summary>
public sealed class FakeTokenIssuer : ITokenIssuer
{
    public Account? IssuedFor { get; private set; }

    public IssuedToken Result { get; set; } = new("test-access-token", DateTime.UtcNow.AddHours(1));

    public IssuedToken Issue(Account account)
    {
        IssuedFor = account;
        return Result;
    }
}
