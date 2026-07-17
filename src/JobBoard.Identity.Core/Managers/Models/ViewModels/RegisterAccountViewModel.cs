using JobBoard.Identity.Core.Managers.Models.Domain;

namespace JobBoard.Identity.Core.Managers.Models.ViewModels;

/// <summary>
/// Inbound request to register an account — the only shape the register controller binds. The business
/// layer hashes the password and translates this to an <see cref="Account"/>; the plaintext never
/// reaches the database.
/// </summary>
public sealed record RegisterAccountViewModel
{
    public string Email { get; init; } = default!;

    public string Password { get; init; } = default!;

    public AccountRole Role { get; init; }
}
