using JobBoard.Applications.Core.Managers.Models.Domain;
using JobBoard.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobBoard.Applications.Core.Data;

/// <summary>
/// EF Core context for <c>applicationsdb</c>. Derives from <see cref="BaseDbContext"/>, which owns the
/// cross-cutting <c>OutboxMessages</c> / <c>InboxMessages</c> sets — so the outbox row (send side) and the
/// inbox row (receive side) land on this same scoped context and enlist in the domain transaction.
/// </summary>
public class ApplicationsDbContext : BaseDbContext
{
    public ApplicationsDbContext(DbContextOptions<ApplicationsDbContext> options) : base(options)
    {
    }

    public DbSet<Application> Applications => Set<Application>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationsDbContext).Assembly);
    }
}
