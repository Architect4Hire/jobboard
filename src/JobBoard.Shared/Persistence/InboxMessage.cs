namespace JobBoard.Shared.Persistence;

/// <summary>
/// A record that a delivered integration event has already been handled, written to a service's own
/// <c>InboxMessages</c> table in the same transaction as the consumer's side effect. Because Service
/// Bus delivery is at-least-once, a consumer checks for this row before acting and no-ops on a repeat.
/// </summary>
public sealed class InboxMessage
{
    /// <summary>The delivered event's identity (its Service Bus <c>MessageId</c>) — the dedupe key.</summary>
    public Guid MessageId { get; set; }

    /// <summary>When the message was handled.</summary>
    public DateTime ProcessedOnUtc { get; set; }
}
