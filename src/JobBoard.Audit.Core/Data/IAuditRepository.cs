using JobBoard.Audit.Core.Managers.Models.Domain;
using JobBoard.Shared.Persistence;

namespace JobBoard.Audit.Core.Data;

/// <summary>
/// Data-only seam for the Audit context. Extends <see cref="IRepository"/> so the data layer can run the
/// inbox check + the append inside one transaction via <see cref="IRepository.ExecuteInTransactionAsync"/>.
/// Append-only — there is no update or delete member by design (ADR-0014).
/// </summary>
public interface IAuditRepository : IRepository
{
    /// <summary>Stages one <see cref="AuditEntry"/> for insert. The caller runs it inside a transaction.</summary>
    Task AddAsync(AuditEntry entry, CancellationToken cancellationToken = default);
}
