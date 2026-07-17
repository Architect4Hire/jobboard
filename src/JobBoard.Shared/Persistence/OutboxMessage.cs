namespace JobBoard.Shared.Persistence;

/// <summary>
/// A single integration event captured in a service's own <c>OutboxMessages</c> table, written in the
/// same transaction as the domain change that produced it. The outbox dispatcher (added later, in the
/// messaging phase) relays each unprocessed row to Service Bus and stamps <see cref="ProcessedOnUtc"/>.
/// Nothing here talks to a broker — this is the durable, transactional record only.
/// </summary>
public sealed class OutboxMessage
{
    /// <summary>The event's own identity, reused as the row key and (later) the Service Bus <c>MessageId</c>.
    /// Because it is deterministic, a retried write cannot enqueue the same event twice.</summary>
    public Guid Id { get; set; }

    /// <summary>The event's runtime type name — the future Service Bus <c>Subject</c>.</summary>
    public string Type { get; set; } = default!;

    /// <summary>The topic the event should be published to.</summary>
    public string Destination { get; set; } = default!;

    /// <summary>The event serialized as JSON (by its runtime type, so derived fields are captured).</summary>
    public string Payload { get; set; } = default!;

    /// <summary>When the row was enqueued; the dispatcher relays rows oldest-first.</summary>
    public DateTime OccurredOnUtc { get; set; }

    /// <summary><c>null</c> until the dispatcher has relayed the event; stamped once sent.</summary>
    public DateTime? ProcessedOnUtc { get; set; }
}
