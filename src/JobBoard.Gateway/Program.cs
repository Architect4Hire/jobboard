using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

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
    .AddServiceDiscoveryDestinationResolver();

var app = builder.Build();

// Auth runs before the proxy so a route's AuthorizationPolicy is enforced before anything is forwarded.
app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();
app.MapReverseProxy();

app.Run();
