namespace JobBoard.Identity.Core.Managers.Models.ServiceModels;

/// <summary>
/// The bearer token returned by <c>POST /identity/login</c> — the signed JWT the client presents to the
/// gateway on protected routes, plus its type and expiry so the client knows when to re-authenticate.
/// </summary>
public sealed record AuthTokenServiceModel(string AccessToken, string TokenType, DateTime ExpiresAtUtc);
