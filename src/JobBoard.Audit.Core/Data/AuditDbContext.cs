using JobBoard.Audit.Core.Managers.Models.Domain;
using JobBoard.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobBoard.Audit.Core.Data;

/// <summary>
/// EF Core context for auditdb. Derives from <see cref="BaseDbContext"/> to inherit the cross-cutting
/// Outbox/Inbox sets — the <b>Inbox</b> gives the audit consumer its idempotency (SCRUB A5); the Outbox
/// comes along for schema uniformity but is unused, since Audit is consumer-only and publishes nothing —
/// and adds the append-only <see cref="AuditEntry"/> aggregate.
/// </summary>
public class AuditDbContext : BaseDbContext
{
    public AuditDbContext(DbContextOptions<AuditDbContext> options) : base(options)
    {
    }

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuditDbContext).Assembly);
    }
}
