using JobBoard.Audit.Core.Data;
using JobBoard.Audit.Core.Managers.Mappers;
using JobBoard.Audit.Core.Managers.Models.ServiceModels;
using JobBoard.Audit.Core.Managers.Models.ViewModels;
using JobBoard.Contracts;

namespace JobBoard.Audit.Core.Business;

/// <inheritdoc cref="IAuditBusiness"/>
/// <remarks>
/// Maps the event to an <see cref="Managers.Models.Domain.AuditEntry"/> and hands it to the data layer
/// keyed by the event's <c>Id</c> — the same id the outbox stamped as the Service Bus <c>MessageId</c>,
/// so the inbox dedupes an at-least-once redelivery.
/// </remarks>
public sealed class AuditBusiness : IAuditBusiness
{
    private readonly IAuditDataLayer _dataLayer;

    public AuditBusiness(IAuditDataLayer dataLayer) => _dataLayer = dataLayer;

    public Task RecordAsync(IIntegrationEvent @event, CancellationToken cancellationToken = default) =>
        _dataLayer.AppendAsync(@event.ToAuditEntry(), @event.Id, cancellationToken);

    public async Task<IReadOnlyList<AuditEntryServiceModel>> QueryAsync(
        AuditQueryViewModel query,
        CancellationToken cancellationToken = default)
    {
        var entries = await _dataLayer.QueryAsync(query.ToAuditQuery(), cancellationToken);
        return entries.Select(entry => entry.ToServiceModel()).ToList();
    }
}
