# API Gateway & Edge Concerns

*YARP as the single public door: routes name a service by its Aspire resource name, authorization is
enforced before proxying, and per-request cross-cutting (correlation, identity projection) happens once,
here, instead of in every service.*

## The problem this solves

If every service were reachable from the browser, "the gateway is the only public door" would be a
convention nobody could verify. Centralizing routing in one YARP instance makes that a fact: a service
endpoint with no gateway route is *unreachable*, not just discouraged. It also gives exactly one place
to enforce auth, mint a correlation id, and turn a validated token into something services can trust —
instead of every service re-implementing (and inevitably drifting on) the same edge logic.

## How it works here

### Routing by resource name, not address

[`appsettings.json`](../../../src/JobBoard.Gateway/appsettings.json) maps public paths to clusters, and
clusters to destinations named by **Aspire resource name**:

```json
"Routes": {
  "applications": { "ClusterId": "applications", "AuthorizationPolicy": "authenticated",
                     "Match": { "Path": "/applications/{**catch-all}" } }
},
"Clusters": {
  "applications": { "Destinations": { "applications": { "Address": "http://applications" } } }
}
```

`http://applications` is never resolved as a literal DNS name — [`Program.cs`](../../../src/JobBoard.Gateway/Program.cs)
wires `AddServiceDiscoveryDestinationResolver()` onto the reverse proxy, so Aspire's service discovery
resolves it to wherever that resource actually runs. This is the one sanctioned place a service name
appears in config, because discovery is what reads it — not a hardcoded `host:port`. Notice
Notifications has **no route at all** in that file: it has no public HTTP surface, so it's
internal-only by design, not an oversight.

A route also states its own authorization requirement inline — `profiles-employers-read` (GET, public)
and `profiles-employers-write` (PUT, `authenticated`) are two separate route entries over the *same*
path, split by HTTP method, so a read stays open while a write requires a token.

### Auth before proxying

[`Program.cs`](../../../src/JobBoard.Gateway/Program.cs) validates the JWTs Identity signs — same
issuer, audience, and symmetric signing key, injected via config/Aspire, never a source literal:

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;   // keep "sub"/"role" as Identity minted them
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = issuer,
            ValidateAudience = true, ValidAudience = audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ValidateLifetime = true,
        };
    });

builder.Services.AddAuthorization(options =>
    options.AddPolicy("authenticated", policy => policy.RequireAuthenticatedUser()));
```

`app.UseAuthentication()` / `UseAuthorization()` run **before** `app.MapReverseProxy()`, so a route's
`AuthorizationPolicy` is enforced before anything is forwarded — an unauthenticated call to a protected
route never reaches a service at all.

### Minting correlation and projecting identity — once, here

YARP transforms run after authentication, so `HttpContext.User` is already the validated principal (or
anonymous, on a public route). The request transform calls
[`EdgeIdentityHeaders.ApplyRequest`](../../../src/JobBoard.Gateway/EdgeIdentityHeaders.cs), which strips
any client-supplied `X-Correlation-Id`/`X-User-Id`/`X-User-Role`, mints a fresh correlation id, and — if
authenticated — projects the token's `sub`/`role` claims onto those same header names on the *outbound*
request to the service. The response transform echoes the minted correlation id back to the caller so
support can quote it later. This is the mechanism side of identity propagation; the full story —
including why the projection is trustworthy and what's still open — is
[Authentication & Identity Propagation](./authentication-and-identity-propagation.md).

## Why

[ADR-0006](../../adr/0006-single-api-gateway-yarp.md) is the decision to have exactly one public entry
point at all. The identity-projection mechanism inside it is [ADR-0015](../../adr/0015-gateway-identity-projection-header-mechanism.md).

## Pitfalls / rules to respect

- **Route by Aspire resource name, never a literal `host:port`.** The one exception (naming a resource
  in `Clusters`) is sanctioned because service discovery is what resolves it.
- **Every client-facing endpoint needs a route; adding it is the last step of shipping the endpoint.** A
  route-less client-facing endpoint is a bug you'll notice as a 404; a route-less internal endpoint
  (Notifications) is by design.
- **Auth at the edge, not duplicated in every service.** A protected route names its policy in
  `appsettings.json`; don't re-implement JWT validation inside a service.
- **Edge cross-cutting only.** Rate limiting, CORS for the Angular origin, and the correlation/identity
  transforms belong here; anything domain-specific belongs in a service.
- **The gateway's public paths are the contract the frontend depends on.** The services behind them can
  move; don't reshape a public path casually.

See `.claude/rules/gateway.md` for the full standing-rule list.

## Reference map

| Concern | Real file |
| --- | --- |
| Routes + clusters | [`appsettings.json`](../../../src/JobBoard.Gateway/appsettings.json) |
| JWT validation + transform wiring | [`Program.cs`](../../../src/JobBoard.Gateway/Program.cs) |
| Correlation mint/strip + identity projection | [`EdgeIdentityHeaders.cs`](../../../src/JobBoard.Gateway/EdgeIdentityHeaders.cs) |
| Standing rules | [`.claude/rules/gateway.md`](../../../.claude/rules/gateway.md) |
