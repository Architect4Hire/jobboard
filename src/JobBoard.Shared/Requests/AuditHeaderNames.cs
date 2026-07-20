namespace JobBoard.Shared.Requests;

/// <summary>
/// The trusted header names the gateway projects inward (ADR-0015) and the service side reads into the
/// <see cref="IRequestContext"/>. This is a wire contract between the edge and every service: the gateway
/// mints/strips/projects these, the services trust them (the gateway→service path is the trust boundary).
/// The gateway mirrors these same literals in its own edge helper rather than referencing Shared, so the
/// edge stays lean — keep the two in sync.
/// </summary>
public static class AuditHeaderNames
{
    /// <summary>The originating request's correlation id, minted fresh at the edge on every request.</summary>
    public const string CorrelationId = "X-Correlation-Id";

    /// <summary>The acting identity (the token's <c>sub</c>), projected from the validated JWT at the edge.</summary>
    public const string UserId = "X-User-Id";

    /// <summary>The acting identity's role (the token's <c>role</c>), projected at the edge.</summary>
    public const string UserRole = "X-User-Role";
}
