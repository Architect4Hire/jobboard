namespace JobBoard.Identity.Core.Security;

/// <summary>
/// A freshly signed access token and when it expires. The <see cref="ITokenIssuer"/> produces this; the
/// business layer maps it to the outbound <c>AuthTokenServiceModel</c>.
/// </summary>
public sealed record IssuedToken(string AccessToken, DateTime ExpiresAtUtc);
