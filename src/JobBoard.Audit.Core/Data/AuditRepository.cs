using JobBoard.Audit.Core.Managers.Models.Domain;
using JobBoard.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobBoard.Audit.Core.Data;

/// <summary>
/// EF Core implementation of <see cref="IAuditRepository"/> over <see cref="AuditDbContext"/>. Inherits
/// <c>ExecuteInTransactionAsync</c> from <see cref="BaseRepository{TContext}"/>. Only appends — never
/// updates or deletes a row.
/// </summary>
public sealed class AuditRepository : BaseRepository<AuditDbContext>, IAuditRepository
{
    // Bounds a broad support query (e.g. a wide time window) so it can't drag the whole trail back; the
    // filters that matter for support (correlation, entity) are far narrower than this in practice.
    private const int MaxResults = 500;

    public AuditRepository(AuditDbContext context) : base(context)
    {
    }

    public async Task AddAsync(AuditEntry entry, CancellationToken cancellationToken = default) =>
        await Context.AuditEntries.AddAsync(entry, cancellationToken);

    public async Task<IReadOnlyList<AuditEntry>> QueryAsync(
        AuditQuery query,
        CancellationToken cancellationToken = default)
    {
        // Read-only projection: no tracking, filters AND-combined, only the supplied ones applied. The
        // CorrelationId/SubjectId axes ride the indexes declared in AuditEntryConfiguration.
        var entries = Context.AuditEntries.AsNoTracking();

        if (query.CorrelationId is { } correlationId)
        {
            entries = entries.Where(entry => entry.CorrelationId == correlationId);
        }

        if (query.SubjectId is { } subjectId)
        {
            entries = entries.Where(entry => entry.SubjectId == subjectId);
        }

        if (query.ActorId is { } actorId)
        {
            entries = entries.Where(entry => entry.ActorId == actorId);
        }

        if (query.FromUtc is { } fromUtc)
        {
            entries = entries.Where(entry => entry.OccurredOnUtc >= fromUtc);
        }

        if (query.ToUtc is { } toUtc)
        {
            entries = entries.Where(entry => entry.OccurredOnUtc <= toUtc);
        }

        // Oldest-first so the caller reads a timeline; Id as a stable tie-breaker for same-instant events.
        return await entries
            .OrderBy(entry => entry.OccurredOnUtc)
            .ThenBy(entry => entry.Id)
            .Take(MaxResults)
            .ToListAsync(cancellationToken);
    }
}
