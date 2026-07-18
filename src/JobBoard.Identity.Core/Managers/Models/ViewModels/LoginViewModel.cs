namespace JobBoard.Identity.Core.Managers.Models.ViewModels;

/// <summary>
/// Inbound credentials for login — the only shape the login controller binds. The business layer
/// verifies them against the stored hash and, on success, issues a JWT.
/// </summary>
public sealed record LoginViewModel
{
    public string Email { get; init; } = default!;

    public string Password { get; init; } = default!;
}
