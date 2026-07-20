namespace JobBoard.Identity.Core.Security;

/// <summary>
/// Hashes and verifies account passwords. A seam over the framework hasher so the business layer can be
/// tested without the real PBKDF2 work, and so the algorithm is swappable in one place.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>Produces a salted, versioned hash of <paramref name="password"/> for storage.</summary>
    string Hash(string password);

    /// <summary>True when <paramref name="providedPassword"/> matches <paramref name="passwordHash"/>.</summary>
    bool Verify(string passwordHash, string providedPassword);
}
