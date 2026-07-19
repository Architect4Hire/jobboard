using JobBoard.Identity.Core.Business;
using JobBoard.Identity.Core.Managers.Models.Domain;
using JobBoard.Identity.Tests.Fakes;
using JobBoard.Shared.Errors;
using JobBoard.Shared.Requests;
using Xunit;

namespace JobBoard.Identity.Tests;

public sealed class AccountBusinessTests
{
    // A known request thread the business reads correlation from. The context actor is deliberately set to
    // a DIFFERENT id than any account, so a test can prove register self-attributes to the account's own id
    // rather than copying the ambient actor (registration is anonymous at the edge).
    private static readonly Guid CorrelationId = Guid.NewGuid();
    private static readonly Guid ContextActorId = Guid.NewGuid();
    private static readonly IRequestContext RequestContext = BuildContext();

    private static AmbientRequestContext BuildContext()
    {
        var context = new AmbientRequestContext();
        context.Populate(CorrelationId, ContextActorId, "candidate");
        return context;
    }

    private static AccountBusiness CreateBusiness(
        FakeAccountDataLayer dataLayer,
        FakePasswordHasher? hasher = null,
        FakeTokenIssuer? issuer = null) =>
        new(dataLayer, hasher ?? new FakePasswordHasher(), issuer ?? new FakeTokenIssuer(), RequestContext);

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
    public async Task RegisterAsync_BuildsAccountCreated_WithSelfOriginatedThread_AndNoSecrets()
    {
        var dataLayer = new FakeAccountDataLayer();
        var business = CreateBusiness(dataLayer);

        await business.RegisterAsync(
            TestData.RegisterViewModel(email: "New@Example.com", password: "s3cret-password", role: AccountRole.Employer));

        var account = dataLayer.RegisteredAccount!;
        var created = dataLayer.CreatedEvent;
        Assert.NotNull(created);

        // Fresh event id (its own outbox-row key), and the account it describes.
        Assert.NotEqual(Guid.Empty, created!.Id);
        Assert.NotEqual(account.Id, created.Id);
        Assert.Equal(account.Id, created.AccountId);

        // Non-secret denormalized fields only — normalized email + role name, never the password/hash.
        Assert.Equal("new@example.com", created.Email);
        Assert.Equal("Employer", created.Role);
        Assert.DoesNotContain("s3cret-password", created.ToString());

        // Root of its request thread: correlation from the context, causation is the request's own id, and
        // the actor is the account itself — NOT the ambient context actor (registration is anonymous).
        Assert.Equal(CorrelationId, created.CorrelationId);
        Assert.Equal(CorrelationId, created.CausationId);
        Assert.Equal(account.Id, created.ActorId);
        Assert.NotEqual(ContextActorId, created.ActorId);
    }

    [Fact]
    public async Task AuthenticateAsync_ValidCredentials_IssuesTokenForAccount_AndRecordsLoggedIn()
    {
        var account = TestData.Account(email: "user@example.com", passwordHash: "stored-hash", role: AccountRole.Employer);
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

        // A successful sign-in records LoggedIn, self-originated (actor == the account), never a LoginFailed.
        var loggedIn = dataLayer.LoggedInEvent;
        Assert.NotNull(loggedIn);
        Assert.NotEqual(Guid.Empty, loggedIn!.Id);
        Assert.Equal(account.Id, loggedIn.AccountId);
        Assert.Equal("user@example.com", loggedIn.Email);
        Assert.Equal("Employer", loggedIn.Role);
        Assert.Equal(CorrelationId, loggedIn.CorrelationId);
        Assert.Equal(CorrelationId, loggedIn.CausationId);
        Assert.Equal(account.Id, loggedIn.ActorId);
        Assert.Null(dataLayer.LoginFailedEvent);
    }

    [Fact]
    public async Task AuthenticateAsync_UnknownEmail_Throws401_IssuesNoToken_AndRecordsLoginFailed()
    {
        var dataLayer = new FakeAccountDataLayer { GetByEmailResult = null };
        var issuer = new FakeTokenIssuer();
        var business = CreateBusiness(dataLayer, issuer: issuer);

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => business.AuthenticateAsync(TestData.LoginViewModel(email: "Nobody@example.com")));

        Assert.Equal("account.invalid_credentials", ex.Code);
        Assert.Equal(401, ex.StatusCode);
        Assert.Null(issuer.IssuedFor);

        // The rejected attempt is recorded: normalized attempted email, uniform reason, no actor, no LoggedIn.
        var failed = dataLayer.LoginFailedEvent;
        Assert.NotNull(failed);
        Assert.Equal("nobody@example.com", failed!.Email);
        Assert.Equal("invalid_credentials", failed.Reason);
        Assert.Equal(CorrelationId, failed.CorrelationId);
        Assert.Null(failed.ActorId);
        Assert.Null(dataLayer.LoggedInEvent);
    }

    [Fact]
    public async Task AuthenticateAsync_WrongPassword_Throws401_IssuesNoToken_AndRecordsLoginFailed()
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

        // A bad password against a known account records the same uniform LoginFailed (no account id leaked).
        var failed = dataLayer.LoginFailedEvent;
        Assert.NotNull(failed);
        Assert.Equal("invalid_credentials", failed!.Reason);
        Assert.Null(failed.ActorId);
        Assert.Null(dataLayer.LoggedInEvent);
    }
}
