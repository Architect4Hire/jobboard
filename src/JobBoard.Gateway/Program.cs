using System.Text;
using JobBoard.Gateway;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

// Cross-cutting Aspire defaults: telemetry, health, resilience, service discovery.
builder.AddServiceDefaults();

// Edge auth: validate the JWTs the Identity service signs before proxying a protected route. Issuer and
// audience come from config; the HMAC signing key is injected via env (Jwt__SigningKey) — the same key
// Identity signs with — and never lives in source. Fail fast if any is missing.
var jwt = builder.Configuration.GetSection("Jwt");
var issuer = jwt["Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer is not configured.");
var audience = jwt["Audience"] ?? throw new InvalidOperationException("Jwt:Audience is not configured.");
var signingKey = jwt["SigningKey"] ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Keep the token's claim types as issued ("sub", "role") instead of remapping them to the long
        // legacy ClaimTypes URIs, so the identity-projection transform reads the same names Identity mints.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ValidateLifetime = true,
        };
    });

// The "authenticated" policy is what a protected YARP route names to require a valid token.
builder.Services.AddAuthorization(options =>
    options.AddPolicy("authenticated", policy => policy.RequireAuthenticatedUser()));

// The gateway is the only public door. Routes/clusters are loaded from configuration; the
// service-discovery destination resolver lets YARP resolve cluster destinations named by Aspire
// resource ("http://jobs") through discovery instead of a literal DNS lookup.
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver()
    // Edge cross-cutting (ADR-0013/0015): mint the correlation thread and project the validated identity
    // inward as trusted headers, stripping client-supplied copies so neither can be spoofed. Runs after
    // UseAuthentication, so HttpContext.User is the validated principal (or anonymous on public routes).
    .AddTransforms(context =>
    {
        context.AddRequestTransform(transformContext =>
        {
            var correlationId = EdgeIdentityHeaders.ApplyRequest(
                transformContext.HttpContext.User, transformContext.ProxyRequest);
            transformContext.HttpContext.Items[EdgeIdentityHeaders.CorrelationItemKey] = correlationId;
            return ValueTask.CompletedTask;
        });

        context.AddResponseTransform(transformContext =>
        {
            if (transformContext.HttpContext.Items[EdgeIdentityHeaders.CorrelationItemKey] is Guid correlationId)
            {
                EdgeIdentityHeaders.ApplyResponse(transformContext.HttpContext.Response, correlationId);
            }

            return ValueTask.CompletedTask;
        });
    });

var app = builder.Build();

// Auth runs before the proxy so a route's AuthorizationPolicy is enforced before anything is forwarded.
app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();
app.MapReverseProxy();

app.Run();
