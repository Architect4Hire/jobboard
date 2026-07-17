using System.Text;
using JobBoard.Identity.Core.Managers.Models.Domain;
using JobBoard.Identity.Core.Security;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace JobBoard.Identity.Tests;

/// <summary>
/// Proves the issuer signs a token the gateway can trust: the JWT carries the account's id, email, and
/// role, validates under the same issuer/audience/key, and is rejected under a different key.
/// </summary>
public sealed class JwtTokenIssuerTests
{
    private const string SigningKey =
        "dGVzdC1zaWduaW5nLWtleS1mb3Itam9iYm9hcmQtaWRlbnRpdHktdW5pdC10ZXN0cy0wMTIzNDU2Nzg5";

    private const string OtherKey =
        "YW5vdGhlci1rZXktdGhhdC1kb2VzLW5vdC1tYXRjaC10aGUtaXNzdWVyLXNpZ25pbmcta2V5LTEyMzQ1Ng==";

    private static JwtOptions CreateOptions() => new()
    {
        SigningKey = SigningKey,
        Issuer = "jobboard-identity",
        Audience = "jobboard",
        TokenLifetimeMinutes = 60,
    };

    [Fact]
    public async Task Issue_ProducesTokenWithExpectedClaims_ValidUnderSameKey()
    {
        var options = CreateOptions();
        var issuer = new JwtTokenIssuer(Options.Create(options));
        var account = TestData.Account(email: "user@example.com", role: AccountRole.Employer);

        var issued = issuer.Issue(account);

        Assert.False(string.IsNullOrWhiteSpace(issued.AccessToken));
        Assert.True(issued.ExpiresAtUtc > DateTime.UtcNow);

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(
            issued.AccessToken,
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = options.Issuer,
                ValidateAudience = true,
                ValidAudience = options.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
                ValidateLifetime = true,
            });

        Assert.True(result.IsValid);
        Assert.Equal(account.Id.ToString(), result.Claims["sub"]?.ToString());
        Assert.Equal("user@example.com", result.Claims["email"]?.ToString());
        Assert.Equal("Employer", result.Claims["role"]?.ToString());
    }

    [Fact]
    public async Task Issue_TokenIsRejected_UnderDifferentSigningKey()
    {
        var issuer = new JwtTokenIssuer(Options.Create(CreateOptions()));
        var issued = issuer.Issue(TestData.Account());

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(
            issued.AccessToken,
            new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(OtherKey)),
                ValidateLifetime = false,
            });

        Assert.False(result.IsValid);
    }
}
