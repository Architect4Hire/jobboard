using JobBoard.Profiles.Core.Data;
using JobBoard.Profiles.Core.Managers.Models.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobBoard.Profiles.Core.Seeding;

/// <summary>
/// Development-only demo data for profilesdb: a candidate résumé profile and an employer company profile
/// for the seeded accounts, so the profile surfaces have realistic content. Idempotent — no-ops once any
/// profile exists. Ids are duplicated by literal from Identity's seeder (each profile's id <b>is</b> the
/// owning account id — reference data, not a cross-service FK). Owns only profilesdb.
/// </summary>
public static class ProfilesSeedData
{
    /// <summary>Well-known account ids — match Identity's seeded accounts.</summary>
    public static readonly Guid EmployerId = new("e0000000-0000-0000-0000-000000000001");
    public static readonly Guid CandidateId = new("c0000000-0000-0000-0000-000000000001");

    public static async Task SeedAsync(ProfilesDbContext db, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        if (!await db.CandidateProfiles.AnyAsync(cancellationToken))
        {
            db.CandidateProfiles.Add(new CandidateProfile
            {
                Id = CandidateId,
                Headline = "Full-Stack .NET + Angular Engineer",
                Summary =
                    "Senior engineer with 8+ years building distributed ASP.NET Core services and modern "
                    + "Angular front-ends on Azure. I care about clean boundaries, testable code, and "
                    + "shipping features end to end — from EF Core migrations to signals in the browser.",
                Skills =
                [
                    "C#", ".NET", "ASP.NET Core", "EF Core", "PostgreSQL", "Azure",
                    "Angular", "TypeScript", "RxJS", "Docker", "Microservices", "Azure Service Bus",
                ],
                ResumeUrl = "https://jobboard.dev/resumes/demo-candidate.pdf",
                UpdatedOnUtc = now,
            });
        }

        if (!await db.EmployerProfiles.AnyAsync(cancellationToken))
        {
            db.EmployerProfiles.Add(new EmployerProfile
            {
                Id = EmployerId,
                CompanyName = "TechNova Cloud",
                Website = "https://technova.dev",
                Description =
                    "TechNova Cloud builds developer platforms on Azure and .NET. We run a microservice "
                    + "architecture on .NET Aspire and Angular, and we're hiring engineers who love clean "
                    + "distributed systems and sharp front-end craft.",
                UpdatedOnUtc = now,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
