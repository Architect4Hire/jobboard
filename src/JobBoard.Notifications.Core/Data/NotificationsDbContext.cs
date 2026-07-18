using JobBoard.Notifications.Core.Managers.Models.Domain;
using JobBoard.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobBoard.Notifications.Core.Data;

/// <summary>
/// EF Core context for notificationsdb. Derives from <see cref="BaseDbContext"/> to inherit the
/// cross-cutting Outbox/Inbox sets — the <b>Inbox</b> is used here for consumer idempotency (the Outbox
/// comes along for schema uniformity but is unused, since Notifications publishes nothing) — and adds the
/// NotificationLog aggregate.
/// </summary>
public class NotificationsDbContext : BaseDbContext
{
    public NotificationsDbContext(DbContextOptions<NotificationsDbContext> options) : base(options)
    {
    }

    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationsDbContext).Assembly);
    }
}
