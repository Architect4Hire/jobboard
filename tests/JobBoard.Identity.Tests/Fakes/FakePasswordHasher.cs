using JobBoard.Identity.Core.Security;

namespace JobBoard.Identity.Tests.Fakes;

/// <summary>
/// Hand-rolled <see cref="IPasswordHasher"/> for business tests — no real PBKDF2 work. Records the
/// plaintext it hashed and the arguments it verified, and returns a configurable
/// <see cref="VerifyResult"/> so a test can simulate a matching or a wrong password.
/// </summary>
public sealed class FakePasswordHasher : IPasswordHasher
{
    public const string HashPrefix = "hash::";

    public string? HashedPassword { get; private set; }

    public bool VerifyResult { get; set; } = true;

    public string? VerifiedHash { get; private set; }

    public string? VerifiedPassword { get; private set; }

    public string Hash(string password)
    {
        HashedPassword = password;
        return HashPrefix + password;
    }

    public bool Verify(string passwordHash, string providedPassword)
    {
        VerifiedHash = passwordHash;
        VerifiedPassword = providedPassword;
        return VerifyResult;
    }
}
