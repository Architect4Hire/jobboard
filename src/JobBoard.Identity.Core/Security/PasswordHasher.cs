using JobBoard.Identity.Core.Managers.Models.Domain;
using Microsoft.AspNetCore.Identity;

namespace JobBoard.Identity.Core.Security;

/// <summary>
/// <see cref="IPasswordHasher"/> backed by ASP.NET Core's
/// <see cref="Microsoft.AspNetCore.Identity.PasswordHasher{TUser}"/> (PBKDF2, per-password salt,
/// versioned format). The <c>TUser</c> argument is only used by the framework for optional rehash
/// decisions, which this seam doesn't expose, so a throwaway <see cref="Account"/> is passed.
/// </summary>
public sealed class PasswordHasher : IPasswordHasher
{
    private static readonly Account Placeholder = new();
    private readonly PasswordHasher<Account> _inner = new();

    public string Hash(string password) => _inner.HashPassword(Placeholder, password);

    public bool Verify(string passwordHash, string providedPassword) =>
        _inner.VerifyHashedPassword(Placeholder, passwordHash, providedPassword)
            != PasswordVerificationResult.Failed;
}
