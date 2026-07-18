using JobBoard.Profiles.Core.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace JobBoard.Profiles.Tests;

/// <summary>
/// A relational (SQLite in-memory) harness over the real <see cref="ProfilesDbContext"/> — the shape the
/// repository/data-layer tests need real transactions and the Skills value-converter round-trip for. One
/// open connection for the harness lifetime keeps committed data alive across contexts.
/// </summary>
public sealed class ProfilesSqliteHarness : IDisposable
{
    private readonly SqliteConnection _connection;

    public ProfilesSqliteHarness()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    /// <summary>A new context over the shared connection — each call sees the same committed data.</summary>
    public ProfilesDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ProfilesDbContext>()
            .UseSqlite(_connection)
            .Options;

        return new ProfilesDbContext(options);
    }

    public void Dispose() => _connection.Dispose();
}
