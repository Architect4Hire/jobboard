using JobBoard.Audit.Core.Managers.Models.ServiceModels;
using JobBoard.Audit.Core.Managers.Models.ViewModels;
using JobBoard.Contracts;

namespace JobBoard.Audit.Core.Business;

/// <summary>
/// The Audit domain: turn each consumed event into one immutable trail row and record it (idempotently,
/// via the data layer), and read the trail back for the support-query surface. One entry point for every
/// event on the write side, since the row shape is uniform. Depends only on <see cref="Data.IAuditDataLayer"/>.
/// </summary>
public interface IAuditBusiness
{
    Task RecordAsync(IIntegrationEvent @event, CancellationToken cancellationToken = default);

    /// <summary>
    /// Translates the (already-validated) filter into the domain query, reads the matching trail rows, and
    /// maps them to outbound ServiceModels (SCRUB A6).
    /// </summary>
    Task<IReadOnlyList<AuditEntryServiceModel>> QueryAsync(
        AuditQueryViewModel query,
        CancellationToken cancellationToken = default);
}
