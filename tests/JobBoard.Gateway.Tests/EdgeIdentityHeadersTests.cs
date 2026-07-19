using System.Net.Http;
using System.Security.Claims;
using JobBoard.Gateway;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace JobBoard.Gateway.Tests;

public sealed class EdgeIdentityHeadersTests
{
    private static ClaimsPrincipal Authenticated(params Claim[] claims)
        => new(new ClaimsIdentity(claims, authenticationType: "jwt"));

    private static string? Single(HttpRequestMessage request, string name)
        => request.Headers.TryGetValues(name, out var values) ? Assert.Single(values) : null;

    [Fact]
    public void Strips_A_Client_Supplied_CorrelationId_And_Mints_A_Fresh_One()
    {
        var request = new HttpRequestMessage();
        var spoofed = Guid.NewGuid().ToString();
        request.Headers.TryAddWithoutValidation(EdgeIdentityHeaders.CorrelationId, spoofed);

        var minted = EdgeIdentityHeaders.ApplyRequest(user: null, request);

        var forwarded = Single(request, EdgeIdentityHeaders.CorrelationId);
        Assert.Equal(minted.ToString(), forwarded);
        Assert.NotEqual(spoofed, forwarded);
        Assert.NotEqual(Guid.Empty, minted);
    }

    [Fact]
    public void Projects_Sub_And_Role_From_The_Validated_Principal()
    {
        var request = new HttpRequestMessage();
        var sub = Guid.NewGuid().ToString();
        var user = Authenticated(new Claim("sub", sub), new Claim("role", "Employer"));

        EdgeIdentityHeaders.ApplyRequest(user, request);

        Assert.Equal(sub, Single(request, EdgeIdentityHeaders.UserId));
        Assert.Equal("Employer", Single(request, EdgeIdentityHeaders.UserRole));
    }

    [Fact]
    public void Strips_Client_Supplied_Identity_Headers_And_Reprojects_From_The_Token()
    {
        var request = new HttpRequestMessage();
        request.Headers.TryAddWithoutValidation(EdgeIdentityHeaders.UserId, Guid.NewGuid().ToString());
        request.Headers.TryAddWithoutValidation(EdgeIdentityHeaders.UserRole, "Admin");

        var realSub = Guid.NewGuid().ToString();
        var user = Authenticated(new Claim("sub", realSub), new Claim("role", "Candidate"));

        EdgeIdentityHeaders.ApplyRequest(user, request);

        Assert.Equal(realSub, Single(request, EdgeIdentityHeaders.UserId));
        Assert.Equal("Candidate", Single(request, EdgeIdentityHeaders.UserRole));
    }

    [Fact]
    public void Anonymous_Request_Has_No_Identity_Headers_But_Still_Gets_A_Correlation()
    {
        var request = new HttpRequestMessage();
        request.Headers.TryAddWithoutValidation(EdgeIdentityHeaders.UserId, "spoofed");

        var minted = EdgeIdentityHeaders.ApplyRequest(user: null, request);

        Assert.False(request.Headers.Contains(EdgeIdentityHeaders.UserId));
        Assert.False(request.Headers.Contains(EdgeIdentityHeaders.UserRole));
        Assert.Equal(minted.ToString(), Single(request, EdgeIdentityHeaders.CorrelationId));
    }

    [Fact]
    public void Authenticated_Principal_Without_A_Sub_Projects_No_UserId()
    {
        var request = new HttpRequestMessage();
        var user = Authenticated(new Claim("role", "Employer"));

        EdgeIdentityHeaders.ApplyRequest(user, request);

        Assert.False(request.Headers.Contains(EdgeIdentityHeaders.UserId));
        Assert.Equal("Employer", Single(request, EdgeIdentityHeaders.UserRole));
    }

    [Fact]
    public void ApplyResponse_Echoes_The_Minted_Correlation_Id()
    {
        var response = new DefaultHttpContext().Response;
        var correlationId = Guid.NewGuid();

        EdgeIdentityHeaders.ApplyResponse(response, correlationId);

        Assert.Equal(correlationId.ToString(), response.Headers[EdgeIdentityHeaders.CorrelationId]);
    }
}
