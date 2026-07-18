using JobBoard.Identity.Core.Business;
using JobBoard.Identity.Core.Managers.Models.Domain;
using JobBoard.Identity.Tests.Fakes;
using JobBoard.Shared.Errors;
using Xunit;

namespace JobBoard.Identity.Tests;

public sealed class AccountBusinessTests
{
    private static AccountBusiness CreateBusiness(
        FakeAccountDataLayer dataLayer,
        FakePasswordHasher? hasher = null,
        FakeTokenIssuer? issuer = null) =>
        new(dataLayer, hasher ?? new FakePasswordHasher(), issuer ?? new FakeTokenIssuer());

    [Fact]
    public async Task RegisterAsync_HashesPassword_NormalizesEmail_AndMapsToServiceModel()
    {
        var dataLayer = new FakeAccountDataLayer();
        var hasher = new FakePasswordHasher();
        var business = CreateBusiness(dataLayer, hasher);

        var result = await business.RegisterAsync(
            TestData.RegisterViewModel(email: "  User@Example.COM ", password: "s3cret-password", role: AccountRole.Employer));

        // Password was hashed (plaintext never persisted); email normalized to lower/trimmed.
        Assert.Equal("s3cret-password", hasher.HashedPassword);
        Assert.NotNull(dataLayer.RegisteredAccount);
        Assert.Equal("user@example.com", dataLayer.RegisteredAccount!.Email);
        Assert.Equal(FakePasswordHasher.HashPrefix + "s3cret-password", dataLayer.RegisteredAccount.PasswordHash);
        Assert.Equal(AccountRole.Employer, dataLayer.RegisteredAccount.Role);

        // The response mirrors the persisted account (never the hash).
        Assert.Equal("user@example.com", result.Email);
        Assert.Equal(AccountRole.Employer, result.Role);
        Assert.Equal(dataLayer.RegisteredAccount.Id, result.Id);
    }

    [Fact]
    public async Task AuthenticateAsync_ValidCredentials_IssuesTokenForAccount()
    {
        var account = TestData.Account(email: "user@example.com", passwordHash: "stored-hash");
        var dataLayer = new FakeAccountDataLayer { GetByEmailResult = account };
        var hasher = new FakePasswordHasher { VerifyResult = true };
        var issuer = new FakeTokenIssuer { Result = new("signed.jwt.token", DateTime.UtcNow.AddMinutes(30)) };
        var business = CreateBusiness(dataLayer, hasher, issuer);

        var token = await business.AuthenticateAsync(TestData.LoginViewModel(email: "USER@example.com", password: "pw"));

        Assert.Equal("user@example.com", dataLayer.LookedUpEmail); // normalized lookup
        Assert.Equal("stored-hash", hasher.VerifiedHash);
        Assert.Equal("pw", hasher.VerifiedPassword);
        Assert.Same(account, issuer.IssuedFor);
        Assert.Equal("signed.jwt.token", token.AccessToken);
        Assert.Equal("Bearer", token.TokenType);
    }

    [Fact]
    public async Task AuthenticateAsync_UnknownEmail_Throws401_AndIssuesNoToken()
    {
        var dataLayer = new FakeAccountDataLayer { GetByEmailResult = null };
        var issuer = new FakeTokenIssuer();
        var business = CreateBusiness(dataLayer, issuer: issuer);

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => business.AuthenticateAsync(TestData.LoginViewModel(email: "nobody@example.com")));

        Assert.Equal("account.invalid_credentials", ex.Code);
        Assert.Equal(401, ex.StatusCode);
        Assert.Null(issuer.IssuedFor);
    }

    [Fact]
    public async Task AuthenticateAsync_WrongPassword_Throws401_AndIssuesNoToken()
    {
        var account = TestData.Account(email: "user@example.com");
        var dataLayer = new FakeAccountDataLayer { GetByEmailResult = account };
        var hasher = new FakePasswordHasher { VerifyResult = false };
        var issuer = new FakeTokenIssuer();
        var business = CreateBusiness(dataLayer, hasher, issuer);

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => business.AuthenticateAsync(TestData.LoginViewModel(password: "wrong")));

        Assert.Equal("account.invalid_credentials", ex.Code);
        Assert.Equal(401, ex.StatusCode);
        Assert.Null(issuer.IssuedFor);
    }
}
