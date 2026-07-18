using System.Security.Claims;
using System.Text;
using JobBoard.Identity.Core.Managers.Models.Domain;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace JobBoard.Identity.Core.Security;

/// <summary>
/// <see cref="ITokenIssuer"/> that signs an HMAC-SHA256 JWT for the account, carrying its id
/// (<c>sub</c>), email, and role. Issuer, audience, lifetime, and the signing key come from
/// <see cref="JwtOptions"/> — the same issuer/audience/key the gateway validates against.
/// </summary>
public sealed class JwtTokenIssuer : ITokenIssuer
{
    private readonly JwtOptions _options;

    public JwtTokenIssuer(IOptions<JwtOptions> options) => _options = options.Value;

    public IssuedToken Issue(Account account)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(_options.TokenLifetimeMinutes);

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, account.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, account.Email),
                new Claim("role", account.Role.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            ]),
            SigningCredentials = credentials,
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return new IssuedToken(token, expires);
    }
}
