using JobBoard.Jobs.Core.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace JobBoard.Jobs.Tests;

/// <summary>
/// A relational (SQLite in-memory) harness over the real <see cref="JobsDbContext"/> — the same shape
/// the repository/data-layer tests need real transactions and the outbox table for. Mirrors the Shared
/// tests' harness: one open connection for the harness lifetime so committed data survives across
/// contexts, letting a fresh context assert what actually persisted.
/// </summary>
public sealed class JobsSqliteHarness : IDisposable
{
    private readonly SqliteConnection _connection;

    public JobsSqliteHarness()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    /// <summary>A new context over the shared connection — each call sees the same committed data.</summary>
    public JobsDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<JobsDbContext>()
            .UseSqlite(_connection)
            .Options;

        return new JobsDbContext(options);
    }

    public void Dispose() => _connection.Dispose();
}
