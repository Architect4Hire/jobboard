namespace JobBoard.Contracts;

/// <summary>
/// Marker for every integration event published across the JobBoard bus.
/// The <see cref="Id"/> is the event's stable identity — used as the Service Bus
/// <c>MessageId</c> and by the inbox for idempotent, at-least-once delivery.
/// Every event also carries the <b>audit thread</b> — <see cref="CorrelationId"/>,
/// <see cref="CausationId"/>, and <see cref="ActorId"/> — so the support audit trail can
/// reconstruct any request cradle-to-grave and attribute it (ADR-0013).
/// Contracts is a leaf library: it holds event records only and references nothing.
/// </summary>
public interface IIntegrationEvent
{
    /// <summary>The event's stable identity — the outbox row key and Service Bus <c>MessageId</c>,
    /// and the inbox dedupe key.</summary>
    Guid Id { get; }

    /// <summary>The originating request's thread, constant across its entire fan-out. Minted at the
    /// gateway (ADR-0015) and stamped onto the event at the publish site (SCRUB A3);
    /// <c>Guid.Empty</c> until that step lands.</summary>
    Guid CorrelationId { get; }

    /// <summary>The <see cref="Id"/> of the event or command that <i>directly</i> caused this one —
    /// the request thread for a root event, the consumed event's id for a follow-on — giving the
    /// trail a causal tree, not just a flat timeline. Stamped at the publish site (SCRUB A3).</summary>
    Guid CausationId { get; }

    /// <summary>Who performed the action: the projected edge identity (ADR-0015), never a
    /// body-supplied id. <c>null</c> for anonymous cradle events (e.g. registration). Stamped at the
    /// publish site (SCRUB A3).</summary>
    Guid? ActorId { get; }
}
