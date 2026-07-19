using JobBoard.Contracts;

namespace JobBoard.Audit.Core.Business;

/// <summary>
/// The Audit domain: turn each consumed event into one immutable trail row and record it (idempotently,
/// via the data layer). One entry point for every event, since the row shape is uniform. Depends only on
/// <see cref="Data.IAuditDataLayer"/>.
/// </summary>
public interface IAuditBusiness
{
    Task RecordAsync(IIntegrationEvent @event, CancellationToken cancellationToken = default);
}
