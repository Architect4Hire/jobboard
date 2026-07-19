using JobBoard.Audit.Core.Managers.Models.Domain;

namespace JobBoard.Audit.Core.Data;

/// <summary>
/// Appends one audit row idempotently. Depends on <see cref="IAuditRepository"/> and the shared
/// <c>IInbox</c>; holds no <c>IOutbox</c> — Audit consumes and records, it publishes nothing.
/// </summary>
public interface IAuditDataLayer
{
    /// <summary>
    /// Appends <paramref name="entry"/> and records <paramref name="messageId"/> in the inbox, in one
    /// transaction — so a redelivery of the same message (same id) finds its inbox row and no-ops,
    /// writing no duplicate row.
    /// </summary>
    Task AppendAsync(AuditEntry entry, Guid messageId, CancellationToken cancellationToken = default);
}
