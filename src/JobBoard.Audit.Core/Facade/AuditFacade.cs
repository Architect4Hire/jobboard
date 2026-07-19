using JobBoard.Audit.Core.Business;
using JobBoard.Contracts;

namespace JobBoard.Audit.Core.Facade;

/// <inheritdoc cref="IAuditFacade"/>
public sealed class AuditFacade : IAuditFacade
{
    private readonly IAuditBusiness _business;

    public AuditFacade(IAuditBusiness business) => _business = business;

    public Task RecordAsync(IIntegrationEvent @event, CancellationToken cancellationToken = default) =>
        _business.RecordAsync(@event, cancellationToken);
}
