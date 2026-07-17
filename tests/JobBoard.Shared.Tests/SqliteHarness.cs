using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace JobBoard.Shared.Tests;

/// <summary>
/// A relational (SQLite in-memory) test harness. SQLite — unlike the EF in-memory provider — supports
/// real transactions and execution strategies, which the rollback test needs. The connection is held
/// open for the lifetime of the harness so the schema (and data) survive between contexts, letting a
/// fresh context assert against what actually committed.
/// </summary>
public sealed class SqliteHarness : IDisposable
{
    private readonly SqliteConnection _connection;

    public SqliteHarness()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    /// <summary>A new context over the shared connection — each call sees the same committed data.</summary>
    public TestDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(_connection)
            .Options;

        return new TestDbContext(options);
    }

    public void Dispose() => _connection.Dispose();
}
