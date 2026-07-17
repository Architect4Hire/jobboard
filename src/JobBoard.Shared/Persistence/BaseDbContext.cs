using JobBoard.Shared.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace JobBoard.Shared.Persistence;

/// <summary>
/// The base <see cref="DbContext"/> every service's <c>&lt;Service&gt;DbContext</c> derives from. It owns
/// the cross-cutting outbox/inbox tables so the transactional-outbox mechanism lives in exactly one
/// place; a service context adds only its own domain sets and calls <c>base.OnModelCreating</c>.
/// </summary>
public abstract class BaseDbContext : DbContext
{
    protected BaseDbContext(DbContextOptions options) : base(options)
    {
    }

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new InboxMessageConfiguration());
    }
}
