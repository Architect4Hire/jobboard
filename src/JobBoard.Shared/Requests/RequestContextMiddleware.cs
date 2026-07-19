using Microsoft.AspNetCore.Http;

namespace JobBoard.Shared.Requests;

/// <summary>
/// Reads the trusted edge headers (<see cref="AuditHeaderNames"/>) into the scoped
/// <see cref="RequestContext"/> so the publish path can stamp the correlation/actor thread onto events
/// (SCRUB A3). Trust here rests on the gateway→service network path (ADR-0015): the gateway has already
/// stripped any client-supplied copies and re-projected these from the validated token, so this side just
/// reads. Malformed or absent headers degrade to <see cref="Guid.Empty"/>/<c>null</c> rather than throwing —
/// a request that never traversed the gateway simply carries no thread.
/// </summary>
public sealed class RequestContextMiddleware
{
    private readonly RequestDelegate _next;

    public RequestContextMiddleware(RequestDelegate next) => _next = next;

    // The context is scoped, so it's injected per-request into Invoke rather than the singleton ctor.
    public async Task InvokeAsync(HttpContext context, AmbientRequestContext requestContext)
    {
        var headers = context.Request.Headers;

        _ = Guid.TryParse(headers[AuditHeaderNames.CorrelationId], out var correlationId);

        Guid? actorId = Guid.TryParse(headers[AuditHeaderNames.UserId], out var parsedActor)
            ? parsedActor
            : null;

        var roleValue = headers[AuditHeaderNames.UserRole].ToString();
        var actorRole = string.IsNullOrWhiteSpace(roleValue) ? null : roleValue;

        requestContext.Populate(correlationId, actorId, actorRole);

        await _next(context);
    }
}
