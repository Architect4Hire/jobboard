using JobBoard.Jobs.Core.Managers.Models.Domain;
using JobBoard.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobBoard.Jobs.Core.Data;

/// <summary>
/// EF Core context for jobsdb. Derives from <see cref="BaseDbContext"/> to inherit the
/// cross-cutting Outbox/Inbox sets, and adds the Jobs domain (Job, Category, Tag).
/// </summary>
public class JobsDbContext : BaseDbContext
{
    public JobsDbContext(DbContextOptions<JobsDbContext> options) : base(options)
    {
    }

    public DbSet<Job> Jobs => Set<Job>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Tag> Tags => Set<Tag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(JobsDbContext).Assembly);
    }
}
