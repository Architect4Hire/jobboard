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

    public Task AppendAsync(AuditEntry entry, Guid messageId, CancellationToken cancellationToken = default)
    {
        Appended = entry;
        MessageId = messageId;
        return Task.CompletedTask;
    }
}
