using JobBoard.Audit.Core.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace JobBoard.Audit.Tests;

/// <summary>
/// A relational (SQLite in-memory) harness over the real <see cref="AuditDbContext"/> — the shape the
/// data-layer idempotency tests need real transactions and the inbox table for. One open connection for
/// the harness lifetime keeps committed data alive across contexts. (The <c>jsonb</c> payload column maps
/// to SQLite's flexible type affinity, so <see cref="Microsoft.EntityFrameworkCore.DatabaseFacade.EnsureCreated"/> is happy.)
/// </summary>
public sealed class AuditSqliteHarness : IDisposable
{
    private readonly SqliteConnection _connection;

    public AuditSqliteHarness()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public AuditDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseSqlite(_connection)
            .Options;

        return new AuditDbContext(options);
    }

    public void Dispose() => _connection.Dispose();
}
