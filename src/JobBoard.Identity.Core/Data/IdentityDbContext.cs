using JobBoard.Identity.Core.Managers.Models.Domain;
using JobBoard.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobBoard.Identity.Core.Data;

/// <summary>
/// EF Core context for identitydb. Derives from <see cref="BaseDbContext"/> to inherit the cross-cutting
/// Outbox/Inbox sets (unused by Identity, but kept for schema uniformity across services), and adds the
/// Identity domain (Account).
/// </summary>
public class IdentityDbContext : BaseDbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options)
    {
    }

    public DbSet<Account> Accounts => Set<Account>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
    }
}
