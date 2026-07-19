using JobBoard.Audit.Core.Managers.Models.Domain;
using JobBoard.Shared.Persistence;

namespace JobBoard.Audit.Core.Data;

/// <summary>
/// EF Core implementation of <see cref="IAuditRepository"/> over <see cref="AuditDbContext"/>. Inherits
/// <c>ExecuteInTransactionAsync</c> from <see cref="BaseRepository{TContext}"/>. Only appends — never
/// updates or deletes a row.
/// </summary>
public sealed class AuditRepository : BaseRepository<AuditDbContext>, IAuditRepository
{
    public AuditRepository(AuditDbContext context) : base(context)
    {
    }

    public async Task AddAsync(AuditEntry entry, CancellationToken cancellationToken = default) =>
        await Context.AuditEntries.AddAsync(entry, cancellationToken);
}
