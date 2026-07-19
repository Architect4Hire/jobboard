using JobBoard.Shared.Requests;

namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// Pipeline activation for the ambient request context. The host registers the services with
/// <c>AddSharedRequestContext()</c> and activates the middleware here.
/// </summary>
public static class RequestContextApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the <see cref="RequestContextMiddleware"/> so the trusted edge headers are read into the
    /// scoped <c>IRequestContext</c> for every request. Place it early, before the endpoints that publish.
    /// </summary>
    public static IApplicationBuilder UseSharedRequestContext(this IApplicationBuilder app)
        => app.UseMiddleware<RequestContextMiddleware>();
}
