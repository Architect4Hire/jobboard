using JobBoard.Profiles.Core.Managers.Models.Domain;
using JobBoard.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace JobBoard.Profiles.Core.Data;

/// <summary>
/// EF Core implementation of <see cref="IEmployerProfileRepository"/> over <see cref="ProfilesDbContext"/>.
/// Inherits <c>ExecuteInTransactionAsync</c> from <see cref="BaseRepository{TContext}"/>.
/// </summary>
public sealed class EmployerProfileRepository : BaseRepository<ProfilesDbContext>, IEmployerProfileRepository
{
    public EmployerProfileRepository(ProfilesDbContext context) : base(context)
    {
    }

    public Task<EmployerProfile?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        Context.EmployerProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<EmployerProfile> UpsertAsync(EmployerProfile incoming, CancellationToken cancellationToken = default)
    {
        var existing = await Context.EmployerProfiles
            .FirstOrDefaultAsync(p => p.Id == incoming.Id, cancellationToken);

        if (existing is null)
        {
            await Context.EmployerProfiles.AddAsync(incoming, cancellationToken);
            return incoming;
        }

        Context.Entry(existing).CurrentValues.SetValues(incoming);
        return existing;
    }

    /// <summary>
    /// True when <paramref name="exception"/> is a primary-key unique violation — the race where two
    /// concurrent first-time upserts for the same owner id both insert.
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
