using JobBoard.Applications.Core.Data;
using JobBoard.Applications.Core.Managers.Models.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobBoard.Applications.Core.Seeding;

/// <summary>
/// Development-only demo data for applicationsdb: a handful of applications for the seeded candidate, in a
/// spread of lifecycle states, so "My applications" has something to review. Idempotent per application —
/// each candidate+job pair is seeded only if it's missing, so a developer can add a demo application and it
/// lands on the next start without wiping the volume. The candidate and job ids are duplicated by literal
/// from the Identity and Jobs seeders (reference data, not cross-service FKs). Owns only applicationsdb.
/// </summary>
public static class ApplicationsSeedData
{
    /// <summary>Well-known candidate account id — matches Identity's seeded candidate.</summary>
    public static readonly Guid CandidateId = new("c0000000-0000-0000-0000-000000000001");

    /// <summary>Well-known job ids — match the Jobs seeder (n is 1–255, two hex digits; seed set uses 1–10).</summary>
    private static Guid JobId(int n) => new($"10000000-0000-0000-0000-0000000000{n:x2}");

    /// <summary>
    /// Deterministic application ids so seeded rows are stable across restarts — derived from the job
    /// number, mirroring <see cref="JobId"/>'s two-hex-digit shape (n is 1–255; the seed set uses 1–10).
    /// </summary>
    private static Guid ApplicationId(int n) => new($"a0000000-0000-0000-0000-0000000000{n:x2}");

    public static async Task SeedAsync(
        ApplicationsDbContext db,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        Application application(int jobNumber, ApplicationStatus status, int appliedDaysAgo, int movedDaysAgo) => new()
        {
            Id = ApplicationId(jobNumber),
            CandidateId = CandidateId,
            JobId = JobId(jobNumber),
            Status = status,
            ResumeReference = null,
            SubmittedOnUtc = now.AddDays(-appliedDaysAgo),
            StatusChangedOnUtc = now.AddDays(-movedDaysAgo),
        };

        var seed = new[]
        {
            application(1, ApplicationStatus.Reviewed, 6, 3),
            application(3, ApplicationStatus.Submitted, 4, 4),
            application(5, ApplicationStatus.Offered, 10, 1),
            application(9, ApplicationStatus.Rejected, 14, 8),
        };

        // Per-application guard on the natural key (candidate applies to a job at most once): seed only
        // the pairs that aren't already present, so new demo applications land without disturbing existing
        // ones. Pre-load the seeded candidate's existing job ids in one query rather than probing per row.
        var existingJobIds = await db.Applications
            .Where(a => a.CandidateId == CandidateId)
            .Select(a => a.JobId)
            .ToListAsync(cancellationToken);
        var existing = existingJobIds.ToHashSet();

        db.Applications.AddRange(seed.Where(a => !existing.Contains(a.JobId)));

        await db.SaveChangesAsync(cancellationToken);
    }
}
