using JobBoard.Applications.Core.Data;
using JobBoard.Applications.Core.Managers.Models.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobBoard.Applications.Core.Seeding;

/// <summary>
/// Development-only demo data for applicationsdb: a handful of applications for the seeded candidate, in a
/// spread of lifecycle states, so "My applications" has something to review. Idempotent — no-ops once any
/// application exists. The candidate and job ids are duplicated by literal from the Identity and Jobs
/// seeders (reference data, not cross-service FKs). Owns only applicationsdb.
/// </summary>
public static class ApplicationsSeedData
{
    /// <summary>Well-known candidate account id — matches Identity's seeded candidate.</summary>
    public static readonly Guid CandidateId = new("c0000000-0000-0000-0000-000000000001");

    /// <summary>Well-known job ids — match the Jobs seeder (n is 1–255, two hex digits; seed set uses 1–10).</summary>
    private static Guid JobId(int n) => new($"10000000-0000-0000-0000-0000000000{n:x2}");

    public static async Task SeedAsync(
        ApplicationsDbContext db,
        CancellationToken cancellationToken = default)
    {
        if (await db.Applications.AnyAsync(cancellationToken))
        {
            return;
        }

        var now = DateTime.UtcNow;

        Application application(int jobNumber, ApplicationStatus status, int appliedDaysAgo, int movedDaysAgo) => new()
        {
            Id = Guid.NewGuid(),
            CandidateId = CandidateId,
            JobId = JobId(jobNumber),
            Status = status,
            ResumeReference = null,
            SubmittedOnUtc = now.AddDays(-appliedDaysAgo),
            StatusChangedOnUtc = now.AddDays(-movedDaysAgo),
        };

        db.Applications.AddRange(
            application(1, ApplicationStatus.Reviewed, 6, 3),
            application(3, ApplicationStatus.Submitted, 4, 4),
            application(5, ApplicationStatus.Offered, 10, 1),
            application(9, ApplicationStatus.Rejected, 14, 8));

        await db.SaveChangesAsync(cancellationToken);
    }
}
