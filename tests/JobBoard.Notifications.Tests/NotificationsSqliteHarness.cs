using JobBoard.Notifications.Core.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace JobBoard.Notifications.Tests;

/// <summary>
/// A relational (SQLite in-memory) harness over the real <see cref="NotificationsDbContext"/> — the shape
/// the data-layer idempotency tests need real transactions and the inbox table for. One open connection
/// for the harness lifetime keeps committed data alive across contexts.
/// </summary>
public sealed class NotificationsSqliteHarness : IDisposable
{
    private readonly SqliteConnection _connection;

    public NotificationsSqliteHarness()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public NotificationsDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseSqlite(_connection)
            .Options;

        return new NotificationsDbContext(options);
    }

    public void Dispose() => _connection.Dispose();
}
