using JobBoard.Profiles.Core.Data;
using JobBoard.Profiles.Core.Managers.Mappers;
using JobBoard.Profiles.Core.Managers.Models.Domain;
using JobBoard.Shared.Messaging;
using JobBoard.Shared.Requests;
using Microsoft.EntityFrameworkCore;

namespace JobBoard.Profiles.Core.Seeding;

/// <summary>
/// Development-only demo data for profilesdb: a candidate résumé profile and an employer company profile
/// for the seeded accounts, so the profile surfaces have realistic content. Idempotent per profile —
/// each well-known id is seeded only if it's missing, so a developer can add a demo profile and it lands
/// on the next start without wiping the volume. Ids are duplicated by literal from Identity's seeder (each profile's id <b>is</b> the
/// owning account id — reference data, not a cross-service FK). Owns only profilesdb.
/// </summary>
public static class ProfilesSeedData
{
    /// <summary>Well-known account ids — match Identity's seeded accounts.</summary>
    public static readonly Guid EmployerId = new("e0000000-0000-0000-0000-000000000001");
    public static readonly Guid CandidateId = new("c0000000-0000-0000-0000-000000000001");

    public static async Task SeedAsync(ProfilesDbContext db, IOutbox outbox, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        if (!await db.CandidateProfiles.AnyAsync(p => p.Id == CandidateId, cancellationToken))
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
                FullName = "Alex Candidate",
                Location = "Austin, TX (Remote)",
                Phone = "+1 (512) 555-0142",
                LinkedInUrl = "https://www.linkedin.com/in/alex-candidate",
                GitHubUrl = "https://github.com/alex-candidate",
                PortfolioUrl = "https://alex.dev",
                YearsOfExperience = 8,
                DesiredRole = "Senior Full-Stack Engineer",
                Availability = CandidateAvailability.WithinTwoWeeks,
                // No résumé blob is seeded — the file is uploaded through the résumé endpoint at runtime.
                UpdatedOnUtc = now,
            });
        }

        if (!await db.EmployerProfiles.AnyAsync(p => p.Id == EmployerId, cancellationToken))
        {
            var employerProfile = new EmployerProfile
            {
                Id = EmployerId,
                CompanyName = "TechNova Cloud",
                Website = "https://technova.dev",
                Description =
                    "TechNova Cloud builds developer platforms on Azure and .NET. We run a microservice "
                    + "architecture on .NET Aspire and Angular, and we're hiring engineers who love clean "
                    + "distributed systems and sharp front-end craft.",
                UpdatedOnUtc = now,
            };
            db.EmployerProfiles.Add(employerProfile);

            // Publish EmployerProfileChanged so Applications' EmployerReference projection (ADR-0012) is
            // populated for the demo employer the same way it is for a real profile write. No HTTP request
            // exists at seed time, so this synthesizes its own root thread (causation == its own
            // correlation, no actor) rather than deriving one from IRequestContext.
            var correlationId = Guid.NewGuid();
            var thread = new AuditThread(correlationId, correlationId, null);
            await outbox.EnqueueAsync(employerProfile.ToEmployerProfileChanged(thread), cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
