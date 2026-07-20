namespace JobBoard.Audit.Core.Managers.Models.Domain;

/// <summary>
/// One immutable row of the support audit trail — the aggregate of the Audit context. Each consumed
/// integration event appends exactly one entry (append-only: never updated or deleted, a correction is
/// a new row — ADR-0014). It records the event's identity, the request thread that produced it, who
/// acted, and the full event as a <c>jsonb</c> payload so support can reconstruct any request
/// cradle-to-grave and attribute it (ADR-0013).
/// </summary>
public class AuditEntry
{
    /// <summary>
    /// The source event's own <c>Id</c> — reused as this row's key so the append is naturally
    /// idempotent (a redelivery collides on the primary key) and lines up with the inbox dedupe key.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>The event-type name (e.g. <c>JobPosted</c>) — the trail's discriminator.</summary>
    public string EventType { get; set; } = default!;

    /// <summary>The originating request's thread, constant across its whole fan-out (ADR-0013).</summary>
    public Guid CorrelationId { get; set; }

    /// <summary>The <c>Id</c> of the event that directly caused this one — the causal tree (ADR-0013).</summary>
    public Guid CausationId { get; set; }

    /// <summary>Who performed the action: the projected edge identity (ADR-0015), never a body-supplied
    /// id. <c>null</c> for anonymous cradle events (e.g. registration).</summary>
    public Guid? ActorId { get; set; }

    /// <summary>The principal entity this row is about (the job, application, etc.), duplicated from the
    /// event for "an entity's life" queries. Populated by the audit consumer (SCRUB A5); nullable so the
    /// schema is ready before that lands.</summary>
    public Guid? SubjectId { get; set; }

    /// <summary>When the recorded event occurred (from the event), not when the row was written.</summary>
    public DateTime OccurredOnUtc { get; set; }

    /// <summary>The full event serialized as JSON, stored in a <c>jsonb</c> column so heterogeneous
    /// event shapes need no per-event migration. Keep secrets and needless PII out of it.</summary>
    public string Payload { get; set; } = default!;
}
