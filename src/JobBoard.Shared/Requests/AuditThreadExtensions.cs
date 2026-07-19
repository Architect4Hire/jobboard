using JobBoard.Contracts;

namespace JobBoard.Shared.Requests;

/// <summary>
/// Derives the <see cref="AuditThread"/> a publish site stamps onto its event. The two derivations differ
/// only in where the thread comes from — the ambient request context for a request-initiated event, or the
/// consumed event for a follow-on — so keeping them here is the single source of the causation rule.
/// </summary>
public static class AuditThreadExtensions
{
    /// <summary>
    /// The thread for a <b>request-initiated</b> event: correlation and actor come from the edge-populated
    /// context, and causation is the request's own id — the correlation id — since the request itself is the
    /// root cause (there is no parent event).
    /// </summary>
    public static AuditThread RootThread(this IRequestContext context) =>
        new(context.CorrelationId, context.CorrelationId, context.ActorId);

    /// <summary>
    /// The thread for a <b>self-originated cradle</b> event — one the edge can't attribute because it runs
    /// before an identity exists (account registration, a login that mints the first token). Correlation and
    /// causation come from the request (root cause), but the actor is the <paramref name="actorId"/> the
    /// action itself establishes — the created/authenticated account's own id, a server-derived value, never
    /// a spoofable client-supplied one. Use instead of <see cref="RootThread"/> when the context carries no
    /// actor yet but the action's own subject is the actor.
    /// </summary>
    public static AuditThread SelfOriginatedThread(this IRequestContext context, Guid actorId) =>
        new(context.CorrelationId, context.CorrelationId, actorId);

    /// <summary>
    /// The thread for a <b>follow-on</b> event built while consuming another: correlation and actor are
    /// inherited from the consumed event, and causation is that event's <see cref="IIntegrationEvent.Id"/> —
    /// making the parent the direct cause in the trail's causal tree.
    /// </summary>
    public static AuditThread FollowOnThread(this IIntegrationEvent cause) =>
        new(cause.CorrelationId, cause.Id, cause.ActorId);
}
