using JobBoard.Profiles.Core.Managers.Models.Domain;
using JobBoard.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace JobBoard.Profiles.Core.Data;

/// <summary>
/// EF Core implementation of <see cref="ICandidateProfileRepository"/> over <see cref="ProfilesDbContext"/>.
/// Inherits <c>ExecuteInTransactionAsync</c> from <see cref="BaseRepository{TContext}"/>.
/// </summary>
public sealed class CandidateProfileRepository : BaseRepository<ProfilesDbContext>, ICandidateProfileRepository
{
    public CandidateProfileRepository(ProfilesDbContext context) : base(context)
    {
    }

    public Task<CandidateProfile?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        Context.CandidateProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<CandidateProfile> UpsertAsync(CandidateProfile incoming, CancellationToken cancellationToken = default)
    {
        // Load the row tracked so an update flows through change tracking; insert when it doesn't exist.
        var existing = await Context.CandidateProfiles
            .FirstOrDefaultAsync(p => p.Id == incoming.Id, cancellationToken);

        if (existing is null)
        {
            await Context.CandidateProfiles.AddAsync(incoming, cancellationToken);
            return incoming;
        }

        // Copy the incoming scalar values (headline, summary, skills, résumé, timestamp) onto the tracked
        // entity; the Skills value comparer detects list changes so the update is persisted.
        Context.Entry(existing).CurrentValues.SetValues(incoming);
        return existing;
    }

    /// <summary>
    /// True when <paramref name="exception"/> is a primary-key unique violation — the narrow race where
    /// two concurrent first-time upserts for the same owner id both insert. The classifier lives here
    /// (the repository owns provider knowledge); the data layer, which owns the transaction, catches it.
    /// </summary>
    public static bool IsDuplicateKeyViolation(DbUpdateException exception) =>
        exception.InnerException switch
        {
            PostgresException pg => pg.SqlState == PostgresErrorCodes.UniqueViolation,
            { } inner => inner.Message.Contains("PRIMARY KEY", StringComparison.OrdinalIgnoreCase)
                || (inner.Message.Contains("unique", StringComparison.OrdinalIgnoreCase)
                    && inner.Message.Contains("constraint", StringComparison.OrdinalIgnoreCase)),
            _ => false,
        };
}
