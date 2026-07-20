namespace JobBoard.Shared.Requests;

/// <summary>
/// The ambient, per-request identity/correlation thread the publish path reads when it stamps an event
/// (ADR-0013/0015, SCRUB A3). Populated once at the edge of the service by
/// <see cref="RequestContextMiddleware"/> from the trusted headers the gateway projects
/// (<see cref="AuditHeaderNames"/>) — never from a request body. Read-only to consumers; only the
/// middleware populates it (via the concrete <see cref="AmbientRequestContext"/>). Registered on the
/// request scope.
/// </summary>
public interface IRequestContext
{
    /// <summary>The originating request's correlation id, minted at the edge. <see cref="System.Guid.Empty"/>
    /// when the request did not arrive through the gateway (e.g. an internal/health call).</summary>
    Guid CorrelationId { get; }

    /// <summary>The acting identity projected by the edge, or <c>null</c> for an anonymous request.</summary>
    Guid? ActorId { get; }

    /// <summary>The acting identity's role projected by the edge, or <c>null</c> when absent.</summary>
    string? ActorRole { get; }
}
