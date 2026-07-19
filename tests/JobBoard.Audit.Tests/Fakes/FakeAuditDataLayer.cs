using JobBoard.Audit.Core.Data;
using JobBoard.Audit.Core.Managers.Models.Domain;

namespace JobBoard.Audit.Tests.Fakes;

/// <summary>
/// Hand-rolled <see cref="IAuditDataLayer"/> for business tests. Captures the entry business built and the
/// message id it keyed on, so a test can assert the event→entry mapping and that the dedupe key is the
/// event's id — without a database.
/// </summary>
public sealed class FakeAuditDataLayer : IAuditDataLayer
{
    public AuditEntry? Appended { get; private set; }

    public Guid? MessageId { get; private set; }

    /// <summary>The query the read path passed down — asserted by the business read tests.</summary>
    public AuditQuery? Queried { get; private set; }

    /// <summary>Rows to return from <see cref="QueryAsync"/>; a test seeds these before querying.</summary>
    public IReadOnlyList<AuditEntry> QueryResult { get; set; } = [];

    public Task AppendAsync(AuditEntry entry, Guid messageId, CancellationToken cancellationToken = default)
    {
        Appended = entry;
        MessageId = messageId;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AuditEntry>> QueryAsync(AuditQuery query, CancellationToken cancellationToken = default)
    {
        Queried = query;
        return Task.FromResult(QueryResult);
    }
}
