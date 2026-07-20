using System.Security.Claims;

namespace JobBoard.Gateway;

/// <summary>
/// The edge cross-cutting mechanism (ADR-0013/0015): mint the request thread and project the validated
/// identity inward as trusted headers, stripping any client-supplied copies so neither can be spoofed. The
/// gateway is the only public door, so a client-supplied correlation id or identity header is never
/// trustworthy — this always strips and re-mints/re-projects. Pure header manipulation, no business logic.
///
/// <para>The header names mirror <c>JobBoard.Shared.Requests.AuditHeaderNames</c> — duplicated here on
/// purpose so the edge stays lean and doesn't reference the persistence/messaging Shared library; keep the
/// two in sync (they are a wire contract).</para>
/// </summary>
public static class EdgeIdentityHeaders
{
    public const string CorrelationId = "X-Correlation-Id";
    public const string UserId = "X-User-Id";
    public const string UserRole = "X-User-Role";

    // The claim types as the Identity service mints them (JwtRegisteredClaimNames.Sub + "role"); read by
    // these literals because the gateway's JwtBearer sets MapInboundClaims = false, so claims aren't remapped.
    private const string SubClaim = "sub";
    private const string RoleClaim = "role";

    // Where the minted correlation id is stashed on the request so the response transform can echo it.
    internal const string CorrelationItemKey = "Edge.CorrelationId";

    /// <summary>
    /// Applies the request-side edge transform to the outbound proxy request: strips any client-supplied
    /// correlation/identity headers, mints a fresh <see cref="CorrelationId"/>, and projects the validated
    /// <c>sub</c>/<c>role</c> claims (when authenticated) as <see cref="UserId"/>/<see cref="UserRole"/>.
    /// Returns the minted correlation id so the caller can echo it on the response.
    /// </summary>
    public static Guid ApplyRequest(ClaimsPrincipal? user, System.Net.Http.HttpRequestMessage proxyRequest)
    {
        // Correlation: never honor an inbound copy — strip and mint fresh at the public edge.
        proxyRequest.Headers.Remove(CorrelationId);
        var correlationId = Guid.NewGuid();
        proxyRequest.Headers.TryAddWithoutValidation(CorrelationId, correlationId.ToString());

        // Identity: strip any client-supplied copies unconditionally, then project only from the token.
        proxyRequest.Headers.Remove(UserId);
        proxyRequest.Headers.Remove(UserRole);

        if (user?.Identity?.IsAuthenticated == true)
        {
            var sub = user.FindFirst(SubClaim)?.Value;
            if (!string.IsNullOrEmpty(sub))
            {
                proxyRequest.Headers.TryAddWithoutValidation(UserId, sub);
            }

            var role = user.FindFirst(RoleClaim)?.Value;
            if (!string.IsNullOrEmpty(role))
            {
                proxyRequest.Headers.TryAddWithoutValidation(UserRole, role);
            }
        }

        return correlationId;
    }

    /// <summary>Echoes the minted correlation id back to the client so support can quote it.</summary>
    public static void ApplyResponse(HttpResponse response, Guid correlationId)
        => response.Headers[CorrelationId] = correlationId.ToString();
}
