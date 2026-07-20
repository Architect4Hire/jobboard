using JobBoard.Identity.Core.Managers.Models.Domain;
using JobBoard.Identity.Core.Managers.Models.ViewModels;

namespace JobBoard.Identity.Tests;

/// <summary>Builders for the fixtures the Identity tests share, kept terse and override-friendly.</summary>
internal static class TestData
{
    public static Account Account(
        Guid? id = null,
        string email = "user@example.com",
        string passwordHash = "hashed-password",
        AccountRole role = AccountRole.Candidate) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Email = email,
        PasswordHash = passwordHash,
        Role = role,
        CreatedOnUtc = DateTime.UtcNow,
    };

    public static RegisterAccountViewModel RegisterViewModel(
        string email = "user@example.com",
        string password = "password123",
        AccountRole role = AccountRole.Candidate) => new()
    {
        Email = email,
        Password = password,
        Role = role,
    };

    public static LoginViewModel LoginViewModel(
        string email = "user@example.com",
        string password = "password123") => new()
    {
        Email = email,
        Password = password,
    };
}
