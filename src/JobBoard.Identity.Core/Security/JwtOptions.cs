namespace JobBoard.Identity.Core.Security;

/// <summary>
/// JWT issuance settings, bound from the host's <c>Jwt</c> configuration section. The
/// <see cref="SigningKey"/> is a secret injected via the AppHost (env), never committed to source; the
/// rest are plain config shared with the gateway so it validates what this service signs.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>Symmetric HMAC-SHA256 key. Injected from Aspire config — kept out of source.</summary>
    public string SigningKey { get; set; } = default!;

    public string Issuer { get; set; } = default!;

    public string Audience { get; set; } = default!;

    public int TokenLifetimeMinutes { get; set; } = 60;
}
