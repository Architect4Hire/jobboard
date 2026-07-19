using JobBoard.Contracts;

namespace JobBoard.Audit.Core.Facade;

/// <summary>
/// The seam the audit consumers call. Thin: it delegates to <see cref="Business.IAuditBusiness"/> — there
/// is nothing to validate or cache, since the events are facts already accepted by their owning service.
/// </summary>
public interface IAuditFacade
{
    Task RecordAsync(IIntegrationEvent @event, CancellationToken cancellationToken = default);
}
