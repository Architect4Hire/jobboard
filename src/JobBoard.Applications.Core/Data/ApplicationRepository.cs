using JobBoard.Applications.Core.Managers.Models.Domain;
using JobBoard.Applications.Core.Managers.Models.ServiceModels;
using JobBoard.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace JobBoard.Applications.Core.Data;

/// <summary>
/// EF Core implementation of <see cref="IApplicationRepository"/> over <see cref="ApplicationsDbContext"/>.
/// Inherits <c>ExecuteInTransactionAsync</c> from <see cref="BaseRepository{TContext}"/>. State transitions
/// are conditional <c>UPDATE</c>s so a concurrent change can never be lost; the read-side status check is
/// only ever a fast path.
/// </summary>
public sealed class ApplicationRepository : BaseRepository<ApplicationsDbContext>, IApplicationRepository
{
    public ApplicationRepository(ApplicationsDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<ApplicationSummaryServiceModel>> ListByCandidateAsync(
        Guid candidateId,
        CancellationToken cancellationToken = default) =>
        await Context.Applications
            .AsNoTracking()
            .Where(a => a.CandidateId == candidateId)
            .OrderByDescending(a => a.SubmittedOnUtc)
            .Select(a => new ApplicationSummaryServiceModel(
                a.Id,
                a.JobId,
                a.Status,
                a.SubmittedOnUtc,
                a.StatusChangedOnUtc))
            .ToListAsync(cancellationToken);

    public Task<Application?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        Context.Applications
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Application>> GetActiveByJobAsync(
        Guid jobId,
        CancellationToken cancellationToken = default) =>
        await Context.Applications
            .AsNoTracking()
            .Where(a => a.JobId == jobId && (
                a.Status == ApplicationStatus.Submitted ||
                a.Status == ApplicationStatus.Reviewed ||
                a.Status == ApplicationStatus.Offered))
            .OrderBy(a => a.SubmittedOnUtc)
            .ToListAsync(cancellationToken);

    public async Task<Application> AddAsync(Application application, CancellationToken cancellationToken = default)
    {
        await Context.Applications.AddAsync(application, cancellationToken);
        return application;
    }

    public async Task<bool> WithdrawIfActiveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var affected = await Context.Applications
            .Where(a => a.Id == id && (
                a.Status == ApplicationStatus.Submitted ||
                a.Status == ApplicationStatus.Reviewed ||
                a.Status == ApplicationStatus.Offered))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(a => a.Status, ApplicationStatus.Withdrawn)
                .SetProperty(a => a.StatusChangedOnUtc, now), cancellationToken);

        return affected > 0;
    }

    public async Task<bool> AdvanceIfInStatusAsync(
        Guid id,
        ApplicationStatus expected,
        ApplicationStatus target,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var affected = await Context.Applications
            .Where(a => a.Id == id && a.Status == expected)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(a => a.Status, target)
                .SetProperty(a => a.StatusChangedOnUtc, now), cancellationToken);

        return affected > 0;
    }

    public async Task<int> CloseActiveByJobAsync(
        Guid jobId,
        ApplicationStatus target,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return await Context.Applications
            .Where(a => a.JobId == jobId && (
                a.Status == ApplicationStatus.Submitted ||
                a.Status == ApplicationStatus.Reviewed ||
                a.Status == ApplicationStatus.Offered))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(a => a.Status, target)
                .SetProperty(a => a.StatusChangedOnUtc, now), cancellationToken);
    }

    public async Task<IReadOnlySet<Guid>> GetIdsInStatusAsync(
        IReadOnlyCollection<Guid> ids,
        ApplicationStatus status,
        CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var matched = await Context.Applications
            .AsNoTracking()
            .Where(a => ids.Contains(a.Id) && a.Status == status)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

        return matched.ToHashSet();
    }

    public async Task UpsertJobReferenceAsync(
        Guid jobId,
        string title,
        Guid employerId,
        CancellationToken cancellationToken = default)
    {
        var existing = await Context.JobReferences.FindAsync([jobId], cancellationToken);
        if (existing is null)
        {
            await Context.JobReferences.AddAsync(
                new JobReference { JobId = jobId, Title = title, EmployerId = employerId }, cancellationToken);
        }
        else
        {
            existing.Title = title;
            existing.EmployerId = employerId;
        }
    }

    public async Task UpsertEmployerReferenceAsync(
        Guid employerId,
        string companyName,
        CancellationToken cancellationToken = default)
    {
        var existing = await Context.EmployerReferences.FindAsync([employerId], cancellationToken);
        if (existing is null)
        {
            await Context.EmployerReferences.AddAsync(
                new EmployerReference { EmployerId = employerId, CompanyName = companyName }, cancellationToken);
        }
        else
        {
            existing.CompanyName = companyName;
        }
    }

    public async Task<IReadOnlyList<ApplicationHistoryServiceModel>> ListMineAsync(
        Guid candidateId,
        CancellationToken cancellationToken = default)
    {
        var applications = await Context.Applications
            .AsNoTracking()
            .Where(a => a.CandidateId == candidateId)
            .OrderByDescending(a => a.SubmittedOnUtc)
            .ToListAsync(cancellationToken);

        if (applications.Count == 0)
        {
            return [];
        }

        // Local joins, in-memory: two small follow-up reads against this same database, keyed by the
        // distinct ids the first read produced. No cross-service call at any point.
        var jobIds = applications.Select(a => a.JobId).Distinct().ToList();
        var jobs = await Context.JobReferences
            .AsNoTracking()
            .Where(j => jobIds.Contains(j.JobId))
            .ToDictionaryAsync(j => j.JobId, cancellationToken);

        var employerIds = jobs.Values.Select(j => j.EmployerId).Distinct().ToList();
        var employers = await Context.EmployerReferences
            .AsNoTracking()
            .Where(e => employerIds.Contains(e.EmployerId))
            .ToDictionaryAsync(e => e.EmployerId, cancellationToken);

        return applications
            .Select(a =>
            {
                jobs.TryGetValue(a.JobId, out var job);
                var employerId = job?.EmployerId ?? Guid.Empty;
                employers.TryGetValue(employerId, out var employer);

                return new ApplicationHistoryServiceModel(
                    a.Id,
                    a.JobId,
                    job?.Title ?? "Unknown job",
                    employerId,
                    employer?.CompanyName ?? "Unknown employer",
                    a.Status,
                    a.SubmittedOnUtc,
                    a.StatusChangedOnUtc);
            })
            .ToList();
    }

    /// <summary>
    /// True when <paramref name="exception"/> is the unique-index violation on <c>(CandidateId, JobId)</c> —
    /// the narrow race where two concurrent submits insert the <i>same</i> candidate/job pair in the window
    /// between the read-side check and the <c>INSERT</c>. The classifier lives here (the repository owns
    /// provider knowledge), but the exception surfaces from <c>SaveChanges</c> inside
    /// <see cref="BaseRepository{TContext}.ExecuteInTransactionAsync{T}"/>, so the data layer — which owns
    /// the transaction — is where it's caught and mapped to a retryable conflict.
    /// </summary>
    public static bool IsDuplicateApplicationViolation(DbUpdateException exception) =>
        exception.InnerException switch
        {
            // Npgsql (production): a unique_violation (23505) whose failing index covers CandidateId.
            PostgresException pg => pg.SqlState == PostgresErrorCodes.UniqueViolation
                && (pg.ConstraintName?.Contains("CandidateId", StringComparison.OrdinalIgnoreCase) ?? false),
            // Other providers (e.g. SQLite in tests) name the offending columns in the failure text.
            { } inner => inner.Message.Contains("CandidateId", StringComparison.OrdinalIgnoreCase)
                && inner.Message.Contains("unique", StringComparison.OrdinalIgnoreCase),
            _ => false,
        };
}
