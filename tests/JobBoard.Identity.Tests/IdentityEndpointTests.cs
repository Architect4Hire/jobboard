using System.Net;
using System.Net.Http.Json;
using JobBoard.Identity.Core.Managers.Models.Domain;
using JobBoard.Identity.Core.Managers.Models.ServiceModels;
using Xunit;

namespace JobBoard.Identity.Tests;

/// <summary>
/// End-to-end over the real pipeline: only view models go in, only service models come out. Covers the
/// register happy path + a duplicate + an invalid body, and login returning a token vs. rejecting bad
/// credentials. Each test hosts a fresh factory (its own in-memory database) for isolation.
/// </summary>
public sealed class IdentityEndpointTests
{
    [Fact]
    public async Task Register_ReturnsCreatedAccount()
    {
        using var factory = new IdentityApiFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/identity/register",
            TestData.RegisterViewModel(email: "new@example.com", role: AccountRole.Employer));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var account = await response.Content.ReadFromJsonAsync<AccountServiceModel>();
        Assert.NotNull(account);
        Assert.Equal("new@example.com", account!.Email);
        Assert.Equal(AccountRole.Employer, account.Role);
        Assert.NotEqual(Guid.Empty, account.Id);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        using var factory = new IdentityApiFactory();
        var client = factory.CreateClient();
        var register = TestData.RegisterViewModel(email: "dupe@example.com");

        var first = await client.PostAsJsonAsync("/identity/register", register);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync("/identity/register", register);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Register_InvalidBody_Returns400()
    {
        using var factory = new IdentityApiFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/identity/register",
            TestData.RegisterViewModel(email: "not-an-email", password: "short"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsBearerToken()
    {
        using var factory = new IdentityApiFactory();
        var client = factory.CreateClient();
        await client.PostAsJsonAsync(
            "/identity/register",
            TestData.RegisterViewModel(email: "login@example.com", password: "password123"));

        var response = await client.PostAsJsonAsync(
            "/identity/login",
            TestData.LoginViewModel(email: "login@example.com", password: "password123"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = await response.Content.ReadFromJsonAsync<AuthTokenServiceModel>();
        Assert.NotNull(token);
        Assert.False(string.IsNullOrWhiteSpace(token!.AccessToken));
        Assert.Equal("Bearer", token.TokenType);
        Assert.True(token.ExpiresAtUtc > DateTime.UtcNow);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        using var factory = new IdentityApiFactory();
        var client = factory.CreateClient();
        await client.PostAsJsonAsync(
            "/identity/register",
            TestData.RegisterViewModel(email: "user@example.com", password: "password123"));

        var response = await client.PostAsJsonAsync(
            "/identity/login",
            TestData.LoginViewModel(email: "user@example.com", password: "wrong-password"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_UnknownEmail_Returns401()
    {
        using var factory = new IdentityApiFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/identity/login",
            TestData.LoginViewModel(email: "nobody@example.com", password: "password123"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
