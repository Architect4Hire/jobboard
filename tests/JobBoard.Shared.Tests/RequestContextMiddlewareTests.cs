using JobBoard.Shared.Requests;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace JobBoard.Shared.Tests;

public sealed class RequestContextMiddlewareTests
{
    private static async Task<AmbientRequestContext> RunAsync(Action<HttpContext> arrange)
    {
        var context = new DefaultHttpContext();
        arrange(context);

        var requestContext = new AmbientRequestContext();
        var middleware = new RequestContextMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(context, requestContext);

        return requestContext;
    }

    [Fact]
    public async Task Populates_The_Thread_From_The_Trusted_Headers()
    {
        var correlationId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        var result = await RunAsync(ctx =>
        {
            ctx.Request.Headers[AuditHeaderNames.CorrelationId] = correlationId.ToString();
            ctx.Request.Headers[AuditHeaderNames.UserId] = actorId.ToString();
            ctx.Request.Headers[AuditHeaderNames.UserRole] = "Employer";
        });

        Assert.Equal(correlationId, result.CorrelationId);
        Assert.Equal(actorId, result.ActorId);
        Assert.Equal("Employer", result.ActorRole);
    }

    [Fact]
    public async Task Missing_Headers_Degrade_To_Empty_And_Null()
    {
        var result = await RunAsync(_ => { });

        Assert.Equal(Guid.Empty, result.CorrelationId);
        Assert.Null(result.ActorId);
        Assert.Null(result.ActorRole);
    }

    [Fact]
    public async Task Anonymous_Request_Carries_Correlation_But_No_Actor()
    {
        var correlationId = Guid.NewGuid();

        var result = await RunAsync(ctx =>
            ctx.Request.Headers[AuditHeaderNames.CorrelationId] = correlationId.ToString());

        Assert.Equal(correlationId, result.CorrelationId);
        Assert.Null(result.ActorId);
        Assert.Null(result.ActorRole);
    }

    [Fact]
    public async Task Malformed_Ids_Degrade_Rather_Than_Throw()
    {
        var result = await RunAsync(ctx =>
        {
            ctx.Request.Headers[AuditHeaderNames.CorrelationId] = "not-a-guid";
            ctx.Request.Headers[AuditHeaderNames.UserId] = "also-not-a-guid";
        });

        Assert.Equal(Guid.Empty, result.CorrelationId);
        Assert.Null(result.ActorId);
    }
}
