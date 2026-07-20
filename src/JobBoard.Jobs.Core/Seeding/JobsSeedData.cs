using JobBoard.Jobs.Core.Data;
using JobBoard.Jobs.Core.Managers.Mappers;
using JobBoard.Jobs.Core.Managers.Models.Domain;
using JobBoard.Shared.Messaging;
using JobBoard.Shared.Requests;
using Microsoft.EntityFrameworkCore;

namespace JobBoard.Jobs.Core.Seeding;

/// <summary>
/// Development-only demo data for jobsdb: a set of prototypical ASP.NET Core / Angular / Azure roles so
/// the board has realistic content to review. Idempotent per job — each well-known <see cref="JobId"/> is
/// seeded only if it's missing, so a developer can add a demo posting and it lands on the next start
/// without wiping the volume. Categories and tags are de-duplicated by slug and shared across postings —
/// existing rows are pre-loaded so a newly-added job reuses them instead of duplicating, mirroring how the
/// repository reconciles classifications. <see cref="EmployerId"/> is the well-known employer account id (duplicated by literal
/// from Identity's seeder — reference data, not a cross-service FK). Owns only jobsdb.
/// </summary>
public static class JobsSeedData
{
    /// <summary>Well-known employer account id — matches Identity's seeded employer.</summary>
    public static readonly Guid EmployerId = new("e0000000-0000-0000-0000-000000000001");

    /// <summary>
    /// Well-known job ids, shared by literal with the Applications seeder. <paramref name="n"/> must be
    /// 1–255 so the two hex digits (<c>{n:x2}</c>) stay two wide and the guid keeps its 32-digit shape;
    /// the seed set uses 1–10. Widen the format if you ever need more than 255 seeded jobs.
    /// </summary>
    public static Guid JobId(int n) => new($"10000000-0000-0000-0000-0000000000{n:x2}");

    public static async Task SeedAsync(JobsDbContext db, IOutbox outbox, CancellationToken cancellationToken = default)
    {
        // De-duplicated classification pools, keyed by slug so a slug maps to exactly one row. Pre-loaded
        // with what's already in the db so a newly-added job reuses existing Category/Tag rows by slug
        // rather than inserting duplicates (the seeder writes directly, bypassing the repository's own
        // slug reconciliation).
        var categories = await db.Categories.ToDictionaryAsync(c => c.Slug, cancellationToken);
        var tags = await db.Tags.ToDictionaryAsync(t => t.Slug, cancellationToken);

        Category category(string name, string slug) =>
            categories.TryGetValue(slug, out var existing)
                ? existing
                : categories[slug] = new Category { Id = Guid.NewGuid(), Name = name, Slug = slug };

        Tag tag(string slug) =>
            tags.TryGetValue(slug, out var existing)
                ? existing
                : tags[slug] = new Tag { Id = Guid.NewGuid(), Name = slug, Slug = slug };

        var now = DateTime.UtcNow;

        Job job(
            int n,
            string title,
            string location,
            decimal min,
            decimal max,
            int postedDaysAgo,
            JobStatus status,
            string description,
            (string name, string slug)[] cats,
            string[] skills) => new()
        {
            Id = JobId(n),
            Title = title,
            Description = description,
            Location = location,
            Salary = new SalaryBand { Min = min, Max = max, Currency = "USD" },
            Status = status,
            EmployerId = EmployerId,
            CreatedOnUtc = now.AddDays(-postedDaysAgo),
            Categories = cats.Select(c => category(c.name, c.slug)).ToList(),
            Tags = skills.Select(tag).ToList(),
        };

        var backend = ("Backend", "backend");
        var frontend = ("Frontend", "frontend");
        var fullstack = ("Full-Stack", "full-stack");
        var cloud = ("Cloud", "cloud");
        var devops = ("DevOps", "devops");
        var architecture = ("Architecture", "architecture");

        var jobs = new List<Job>
        {
            job(1, "Senior .NET Backend Engineer", "Remote (US)", 140_000, 175_000, 1, JobStatus.Open,
                "Own core services in a distributed ASP.NET Core platform on Azure. You'll design clean, "
                + "testable APIs over EF Core and PostgreSQL, publish integration events across a Service "
                + "Bus, and keep our latency budgets honest.",
                new[] { backend }, new[] { "c-sharp", "dotnet", "aspnet-core", "ef-core", "postgresql", "azure" }),

            job(2, "Angular Front-End Developer", "Austin, TX · Hybrid", 115_000, 145_000, 2, JobStatus.Open,
                "Build fast, accessible UIs with modern Angular — standalone components, signals, and the "
                + "async pipe. You care about type-safe models, tidy RxJS, and pixel-tight design "
                + "implementation.",
                new[] { frontend }, new[] { "angular", "typescript", "rxjs", "signals", "html-css" }),

            job(3, "Full-Stack Engineer (.NET + Angular)", "Remote (US)", 130_000, 165_000, 3, JobStatus.Open,
                "Ship features end to end: a C# / ASP.NET Core API behind a YARP gateway and an Angular "
                + "client that consumes it. Comfortable moving across the stack and owning a slice from "
                + "database to pixels.",
                new[] { fullstack }, new[] { "c-sharp", "aspnet-core", "angular", "typescript", "azure" }),

            job(4, "Azure Cloud Solutions Architect", "Seattle, WA", 160_000, 205_000, 4, JobStatus.Open,
                "Define our Azure landing zones and reference architectures. You'll shape microservice "
                + "boundaries, infra-as-code with Bicep/Terraform, and guardrails that let teams ship "
                + "safely at scale.",
                new[] { cloud, architecture },
                new[] { "azure", "bicep", "terraform", "kubernetes", "microservices" }),

            job(5, "Microservices Platform Engineer (.NET Aspire)", "Remote (US)", 145_000, 185_000, 5, JobStatus.Open,
                "Build the paved road for our services: .NET Aspire orchestration, a transactional outbox "
                + "over Azure Service Bus, health and telemetry by default. You make the right thing the "
                + "easy thing for product teams.",
                new[] { backend, cloud },
                new[] { "dotnet", "aspire", "service-bus", "docker", "microservices" }),

            job(6, "DevOps Engineer — Azure DevOps & Kubernetes", "Denver, CO · Hybrid", 125_000, 160_000, 6, JobStatus.Open,
                "Own CI/CD and runtime for a container platform on AKS. Pipelines in Azure DevOps, "
                + "infra in Terraform, observability that catches problems before customers do.",
                new[] { devops, cloud },
                new[] { "azure", "kubernetes", "docker", "ci-cd", "terraform" }),

            job(7, "Lead Angular Engineer", "New York, NY", 150_000, 185_000, 7, JobStatus.Open,
                "Lead the front-end guild for a large Angular app. Set patterns for state, testing, and "
                + "performance, mentor engineers, and keep the codebase strict and maintainable as it grows.",
                new[] { frontend }, new[] { "angular", "typescript", "rxjs", "ngrx", "testing" }),

            job(8, ".NET API Developer — EF Core & PostgreSQL", "Remote (US)", 110_000, 140_000, 9, JobStatus.Open,
                "Design and evolve REST APIs on ASP.NET Core with EF Core over PostgreSQL. Solid "
                + "fundamentals, clean migrations, and a bias for well-tested, boring-in-a-good-way code.",
                new[] { backend }, new[] { "c-sharp", "aspnet-core", "ef-core", "postgresql", "rest" }),

            job(9, "Principal Software Engineer — C# / Azure", "Boston, MA", 185_000, 230_000, 12, JobStatus.Open,
                "Set technical direction across event-driven services on Azure. Domain-driven design, "
                + "resilient messaging, and the judgment to know which trade-off fits which problem.",
                new[] { architecture, cloud },
                new[] { "c-sharp", "azure", "microservices", "ddd", "event-driven" }),

            job(10, "Staff Frontend Engineer — Design Systems (Angular)", "Remote (US)", 155_000, 195_000, 20, JobStatus.Closed,
                "Owned our Angular design system: accessible components, theming, and Storybook docs used "
                + "by every product team. (This posting has been filled — kept here to show a closed role.)",
                new[] { frontend }, new[] { "angular", "typescript", "storybook", "accessibility" }),
        };

        // Per-id guard: seed only the postings whose well-known id isn't already present. Categories and
        // tags reachable only from skipped jobs stay untracked (never added); those reachable from a new
        // job are added transitively, reusing any pre-loaded existing row for the same slug.
        var seededJobIds = jobs.Select(j => j.Id).ToList();
        var existingJobIds = await db.Jobs
            .Where(j => seededJobIds.Contains(j.Id))
            .Select(j => j.Id)
            .ToListAsync(cancellationToken);
        var existing = existingJobIds.ToHashSet();

        var newJobs = jobs.Where(j => !existing.Contains(j.Id)).ToList();
        db.Jobs.AddRange(newJobs);

        // Publish JobPosted for each newly-seeded job, exactly like JobBusiness.PostAsync, so Applications'
        // JobReference projection (ADR-0012) is populated for demo jobs the same way it is for real posts.
        // No HTTP request exists at seed time, so there's no IRequestContext to derive a thread from — each
        // event synthesizes its own root thread (causation == its own correlation, no actor).
        foreach (var newJob in newJobs)
        {
            var correlationId = Guid.NewGuid();
            var thread = new AuditThread(correlationId, correlationId, null);
            await outbox.EnqueueAsync(newJob.ToJobPosted(thread), cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
