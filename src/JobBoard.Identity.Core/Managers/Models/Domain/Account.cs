namespace JobBoard.Identity.Core.Managers.Models.Domain;

/// <summary>
/// A user account — the aggregate root of the Identity context. Holds the credential
/// (<see cref="PasswordHash"/>, never the plaintext) and the <see cref="Role"/> the issued JWT will
/// carry. <see cref="Email"/> is the natural login key and is uniquely indexed.
/// </summary>
public class Account
{
    public Guid Id { get; set; }

    public string Email { get; set; } = default!;

    /// <summary>The PBKDF2 hash produced by <c>IPasswordHasher</c>; the plaintext is never stored.</summary>
    public string PasswordHash { get; set; } = default!;

    public AccountRole Role { get; set; }

    public DateTime CreatedOnUtc { get; set; }
}
