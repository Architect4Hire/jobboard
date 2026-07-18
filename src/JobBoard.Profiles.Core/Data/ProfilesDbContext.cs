using JobBoard.Profiles.Core.Managers.Models.Domain;
using JobBoard.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobBoard.Profiles.Core.Data;

/// <summary>
/// EF Core context for profilesdb. Derives from <see cref="BaseDbContext"/> to inherit the cross-cutting
/// Outbox/Inbox sets (unused by Profiles, but kept for schema uniformity across services), and adds the
/// two profile aggregates (CandidateProfile, EmployerProfile).
/// </summary>
public class ProfilesDbContext : BaseDbContext
{
    public ProfilesDbContext(DbContextOptions<ProfilesDbContext> options) : base(options)
    {
    }

    public DbSet<CandidateProfile> CandidateProfiles => Set<CandidateProfile>();

    public DbSet<EmployerProfile> EmployerProfiles => Set<EmployerProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProfilesDbContext).Assembly);
    }
}
