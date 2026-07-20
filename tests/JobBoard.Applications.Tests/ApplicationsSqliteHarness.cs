using JobBoard.Applications.Core.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace JobBoard.Applications.Tests;

/// <summary>
/// A relational (SQLite in-memory) harness over the real <see cref="ApplicationsDbContext"/> — the shape
/// the repository/data-layer/consumer tests need real transactions plus the outbox/inbox tables for. One
/// open connection for the harness lifetime so committed data survives across contexts, letting a fresh
/// context assert what actually persisted.
/// </summary>
public sealed class ApplicationsSqliteHarness : IDisposable
{
    private readonly SqliteConnection _connection;

    public ApplicationsSqliteHarness()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    /// <summary>A new context over the shared connection — each call sees the same committed data.</summary>
    public ApplicationsDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationsDbContext>()
            .UseSqlite(_connection)
            .Options;

        return new ApplicationsDbContext(options);
    }

    public void Dispose() => _connection.Dispose();
}
