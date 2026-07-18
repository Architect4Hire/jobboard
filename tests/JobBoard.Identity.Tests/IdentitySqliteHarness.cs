using JobBoard.Identity.Core.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace JobBoard.Identity.Tests;

/// <summary>
/// A relational (SQLite in-memory) harness over the real <see cref="IdentityDbContext"/> — the shape the
/// repository/data-layer tests need real transactions and the unique-email index for. One open
/// connection for the harness lifetime keeps committed data alive across contexts, so a fresh context can
/// assert what actually persisted.
/// </summary>
public sealed class IdentitySqliteHarness : IDisposable
{
    private readonly SqliteConnection _connection;

    public IdentitySqliteHarness()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    /// <summary>A new context over the shared connection — each call sees the same committed data.</summary>
    public IdentityDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlite(_connection)
            .Options;

        return new IdentityDbContext(options);
    }

    public void Dispose() => _connection.Dispose();
}
