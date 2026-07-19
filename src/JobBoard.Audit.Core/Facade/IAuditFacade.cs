using JobBoard.Audit.Core.Managers.Models.ServiceModels;
using JobBoard.Audit.Core.Managers.Models.ViewModels;
using JobBoard.Contracts;

namespace JobBoard.Audit.Core.Facade;

/// <summary>
/// The seam the audit consumers and the support-query controller call. On the write side it delegates
/// straight to <see cref="Business.IAuditBusiness"/> — consumed events are facts already accepted by their
/// owning service, so there is nothing to validate or cache. On the read side it validates the inbound
/// query filter (the one edge seam the layers below must not own) before delegating.
/// </summary>
public interface IAuditFacade
{
    Task RecordAsync(IIntegrationEvent @event, CancellationToken cancellationToken = default);

    /// <summary>Validates the support-query filter, then returns the matching trail rows (SCRUB A6).</summary>
    Task<IReadOnlyList<AuditEntryServiceModel>> QueryAsync(
        AuditQueryViewModel query,
        CancellationToken cancellationToken = default);
}
