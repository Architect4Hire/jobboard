# Authentication & Identity Propagation

*A JWT issued by Identity, validated once at the gateway, and projected inward as trusted headers so a
service can learn "who is calling" without validating a token itself — plus the honest state of the one
piece of this that's still open: whether services actually trust that projection over the request body.*

## The problem this solves

Every protected write needs to know two things: is this caller who they claim to be, and are they
allowed to do this specific thing. Doing full JWT validation in every service duplicates the signing key
and validation logic five times over; trusting whatever the client puts in the request body is worse —
it's not authentication at all. The chosen shape validates the token exactly once (at the edge, where
the only public entry point already sits) and re-expresses the result as something the network path
guarantees came from the gateway, not the browser.

## How it works here

### Issue — Identity signs an HMAC JWT

[`JwtTokenIssuer.Issue`](../../../src/JobBoard.Identity.Core/Security/JwtTokenIssuer.cs) signs an
HS256 token carrying the account's id, email, and role:

```csharp
Subject = new ClaimsIdentity([
    new Claim(JwtRegisteredClaimNames.Sub, account.Id.ToString()),
    new Claim(JwtRegisteredClaimNames.Email, account.Email),
    new Claim("role", account.Role.ToString()),
    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
]),
SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256),
```

Issuer, audience, lifetime, and the signing key all come from `JwtOptions` — the *same* signing key the
gateway is handed via `Jwt__SigningKey` in [`AppHost.cs`](../../../src/JobBoard.AppHost/AppHost.cs)
(generated per-run if not supplied via config, so `aspire run` stays one command without a literal in
source). Passwords never touch this — they're hashed separately by
[`PasswordHasher`](../../../src/JobBoard.Identity.Core/Security/PasswordHasher.cs), a thin wrapper over
ASP.NET Core's own PBKDF2 `PasswordHasher<TUser>`.

[`AccountBusiness`](../../../src/JobBoard.Identity.Core/Business/AccountBusiness.cs) is worth reading
end to end for how login and registration handle the *audit* thread specially — see
[Correlation, Causation & the Audit Trail](./correlation-causation-and-audit-trail.md#self-originated-events)
for why (in short: there's no token yet to derive an actor from at the moment an account is created or
a login succeeds).

### Validate — the gateway, and only the gateway

[`Program.cs`](../../../src/JobBoard.Gateway/Program.cs) validates every claim that matters — issuer,
audience, signing key, lifetime — before a protected route is proxied. See
[API Gateway & Edge Concerns](./api-gateway-edge.md) for the surrounding routing/auth-policy mechanics.
No other service in this codebase re-validates the token; they trust what the gateway already decided.

### Project — trusted headers, not the token itself

Rather than forward the raw JWT inward, the gateway re-expresses the validated claims as headers on the
proxied request — [`EdgeIdentityHeaders.ApplyRequest`](../../../src/JobBoard.Gateway/EdgeIdentityHeaders.cs):

```csharp
proxyRequest.Headers.Remove(UserId);
proxyRequest.Headers.Remove(UserRole);   // strip any client-supplied copy, unconditionally

if (user?.Identity?.IsAuthenticated == true)
{
    var sub = user.FindFirst(SubClaim)?.Value;
    if (!string.IsNullOrEmpty(sub)) proxyRequest.Headers.TryAddWithoutValidation(UserId, sub);
    var role = user.FindFirst(RoleClaim)?.Value;
    if (!string.IsNullOrEmpty(role)) proxyRequest.Headers.TryAddWithoutValidation(UserRole, role);
}
```

A service reads those headers into its own `IRequestContext` at the edge of *its* pipeline —
[`RequestContextMiddleware`](../../../src/JobBoard.Shared/Requests/RequestContextMiddleware.cs) parses
`X-Correlation-Id`/`X-User-Id`/`X-User-Role` into an `AmbientRequestContext`, degrading to
`Guid.Empty`/`null` on anything malformed or absent rather than throwing — a request that never
traversed the gateway (a health check, an internal call) simply carries no thread. The header names are
a wire contract deliberately duplicated in two places —
[`AuditHeaderNames`](../../../src/JobBoard.Shared/Requests/AuditHeaderNames.cs) in `Shared`, and the
literals inside `EdgeIdentityHeaders` in the gateway — so the gateway's lean edge project doesn't have
to reference `Shared`'s persistence/messaging stack just to agree on three string constants.

**Why this is trustworthy at all:** the gateway unconditionally strips any client-supplied copy of these
three headers before re-projecting them from the validated token. A browser can send whatever
`X-User-Id` it likes; it never survives the hop through the gateway. The trust boundary is the
gateway→service network path itself — in a real deployment that means the services must be unreachable
except through the gateway (network policy, or a shared secret/mTLS), a caveat [ADR-0011](../../adr/0011-token-derived-identity-propagation.md)
calls out explicitly for this being a local Aspire topology, not a hardened one.

### The gap this doesn't close yet

Having a trustworthy `IRequestContext.ActorId` doesn't automatically mean every service *uses* it. As of
this writing, [`PostJobViewModel`](../../../src/JobBoard.Jobs.Core/Managers/Models/ViewModels/PostJobViewModel.cs)
still carries a body-supplied `EmployerId` — the business layer doesn't derive the poster from the
projected identity, it trusts whatever the client sent. That's a live BOLA/IDOR gap, not a documentation
oversight: [ADR-0011](../../adr/0011-token-derived-identity-propagation.md) is **Proposed**, not
Accepted. Its scope was split on 2026-07-19 — the header-*projection mechanism* above shipped as
[ADR-0015](../../adr/0015-gateway-identity-projection-header-mechanism.md) (Accepted) because the audit
trail needed a trustworthy actor regardless; the *remediation* — removing body-supplied
`EmployerId`/`CandidateId` from ViewModels, adding `employer`/`candidate` role policies at the edge, and
per-object ownership checks in business — is still open work. If you're adding a new write endpoint,
don't add a fourth body-supplied identity field to the pile; ask whether this is the endpoint that
finally schedules ADR-0011.

## Why

[ADR-0007](../../adr/0007-identity-issued-symmetric-jwt.md) is the token-issuance decision.
[ADR-0015](../../adr/0015-gateway-identity-projection-header-mechanism.md) is the projection mechanism.
[ADR-0011](../../adr/0011-token-derived-identity-propagation.md) is the still-open remediation that
would make services actually *use* the projected identity instead of the request body.

## Pitfalls / rules to respect

- **Never trust a body-supplied actor id over the projected one** for anything security-sensitive you're
  newly adding — even though older endpoints still do, per the gap above.
- **A service never re-validates the JWT.** If you find yourself wiring `AddJwtBearer` into a service
  host, stop — that's the gateway's job.
- **The header names are a wire contract.** If you ever change `AuditHeaderNames`, change
  `EdgeIdentityHeaders`'s literals in the same change — they must stay identical.
- **Signing keys and secrets never live in source.** They come from Aspire-injected config/env
  (`Jwt__SigningKey`), generated per-run in dev when not supplied.
- **Same error for a wrong password and an unknown email.** `AccountBusiness.AuthenticateAsync` never
  discloses which one failed — don't add a code path that does.

## Reference map

| Concern | Real file |
| --- | --- |
| Token issuance | [`JwtTokenIssuer.cs`](../../../src/JobBoard.Identity.Core/Security/JwtTokenIssuer.cs) |
| Password hashing | [`PasswordHasher.cs`](../../../src/JobBoard.Identity.Core/Security/PasswordHasher.cs) |
| Login/registration business rules | [`AccountBusiness.cs`](../../../src/JobBoard.Identity.Core/Business/AccountBusiness.cs) |
| Gateway JWT validation | [`Program.cs`](../../../src/JobBoard.Gateway/Program.cs) |
| Header projection | [`EdgeIdentityHeaders.cs`](../../../src/JobBoard.Gateway/EdgeIdentityHeaders.cs) |
| Service-side context | [`RequestContextMiddleware.cs`](../../../src/JobBoard.Shared/Requests/RequestContextMiddleware.cs) · [`IRequestContext.cs`](../../../src/JobBoard.Shared/Requests/IRequestContext.cs) |
| The open BOLA/IDOR gap | [ADR-0011](../../adr/0011-token-derived-identity-propagation.md) |
