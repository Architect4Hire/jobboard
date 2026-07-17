using JobBoard.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobBoard.Jobs.Core.Data;

/// <summary>
/// EF Core context for jobsdb. Derives from <see cref="BaseDbContext"/> to inherit the
/// cross-cutting Outbox/Inbox sets; domain sets (Job, Category, Tag) arrive in later steps.
/// </summary>
public class JobsDbContext : BaseDbContext
{
    public JobsDbContext(DbContextOptions<JobsDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Domain configuration added with the Job model in a later step.
    }
}
