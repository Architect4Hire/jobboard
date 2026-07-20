using JobBoard.Identity.Core.Data;
using JobBoard.Identity.Core.Managers.Models.Domain;
using JobBoard.Identity.Core.Security;
using Microsoft.EntityFrameworkCore;

namespace JobBoard.Identity.Core.Seeding;

/// <summary>
/// Development-only demo data for identitydb: one employer and one candidate account with known
/// credentials, so a reviewer can sign in and exercise the app without registering. Idempotent per
/// account — each well-known id is seeded only if it's missing, so a developer can add a demo account
/// and it lands on the next start without wiping the volume. The well-known ids are duplicated (as plain Guids) in the other
/// services' seeders so the seeded jobs/applications/profiles line up; that's allowed reference-data
/// duplication, never a cross-service FK. This service owns only identitydb and touches nothing else.
/// </summary>
public static class IdentitySeedData
{
    /// <summary>Well-known account ids, shared by literal with the other services' seeders.</summary>
    public static readonly Guid EmployerId = new("e0000000-0000-0000-0000-000000000001");
    public static readonly Guid CandidateId = new("c0000000-0000-0000-0000-000000000001");

    /// <summary>The shared demo password for every seeded account (shown in the sign-in screen hint).</summary>
    public const string DemoPassword = "Passw0rd!";

    public const string EmployerEmail = "employer@jobboard.dev";
    public const string CandidateEmail = "candidate@jobboard.dev";

    public static async Task SeedAsync(
        IdentityDbContext db,
        IPasswordHasher hasher,
        CancellationToken cancellationToken = default)
    {
        var createdOn = DateTime.UtcNow;
        var hash = hasher.Hash(DemoPassword);

        // Per-id guards: seed each account only if its well-known id is absent. Adding a new demo
        // account is then just another block — it lands on the next start, existing rows untouched.
        if (!await db.Accounts.AnyAsync(a => a.Id == EmployerId, cancellationToken))
        {
            db.Accounts.Add(new Account
            {
                Id = EmployerId,
                Email = EmployerEmail,
                PasswordHash = hash,
                Role = AccountRole.Employer,
                CreatedOnUtc = createdOn,
            });
        }

        if (!await db.Accounts.AnyAsync(a => a.Id == CandidateId, cancellationToken))
        {
            db.Accounts.Add(new Account
            {
                Id = CandidateId,
                Email = CandidateEmail,
                PasswordHash = hash,
                Role = AccountRole.Candidate,
                CreatedOnUtc = createdOn,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
