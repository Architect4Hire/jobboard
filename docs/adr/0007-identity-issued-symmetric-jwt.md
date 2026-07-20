# ADR-0007: Identity-issued symmetric (HS256) JWT validated at the edge

- **Status:** Accepted
- **Date:** 2026-07-18
- **Deciders:** Robert Felkins
- **Related:** ADR-0006 (gateway), ADR-0008 (Aspire secret wiring), ADR-0011 (identity propagation — proposed), `docs/design/high-level-design.md` §8.1
- **Implements:** `JobBoard.Identity.Core/Security/JwtTokenIssuer.cs`, `JobBoard.Gateway/Program.cs`, `AppHost.cs`

## Context

The system needs authentication: candidates and employers log in, and protected routes must reject anonymous callers. In a microservice system, *who issues* tokens and *who validates* them, and *how the key is shared*, are architectural choices. The design must keep the token-issuing authority in one service (Identity) and let the edge (the gateway, ADR-0006) validate without every service re-implementing auth.

## Decision

**We will have the Identity service issue HMAC-SHA256 (HS256) JWTs, and the gateway validate them at the edge, sharing a single signing key injected by Aspire.**

- **Issuer.** `JwtTokenIssuer` signs a JWT carrying `sub` (account id), `email`, `role`, and `jti`, with issuer/audience/lifetime from `JwtOptions`.
- **Validator.** The gateway configures JWT bearer validation against the **same** issuer, audience, and signing key, and only then proxies a protected route (ADR-0006).
- **Key handling.** The shared HMAC signing key is injected to Identity and the gateway via Aspire env (`Jwt__SigningKey`) — supplied from config/user-secrets when present, otherwise a per-run generated dev key so `aspire run` stays one command. **Never a literal in source.**
- **Passwords** are hashed with ASP.NET Core's PBKDF2 `PasswordHasher` (per-password salt, versioned format).

## Consequences

**Positive**
- Stateless validation at the edge: the gateway verifies a token's signature and claims without a call back to Identity.
- One issuing authority; one validation point; the rest of the system trusts the gateway's gate.
- The key never lives in source, and local dev needs no secret setup.

**Negative**
- **Symmetric (HS256) means the validator holds a *signing-capable* key.** Anyone who can validate can also mint tokens — acceptable while Identity and the gateway are the only holders, but it doesn't scale to many independent validators. Moving to **asymmetric RS256** (Identity signs with a private key; validators hold only the public key) is a 60-day hardening item.
- **Short-lived access tokens only, no refresh/rotation yet** — a re-login is required at expiry; refresh tokens are a 60-day item.
- **Token transport/storage is a frontend concern** currently weak: the SPA stores the JWT in `localStorage` (latent XSS token-theft risk) — a 60-day item.
- **Edge authorization is coarse today:** a valid token passes; per-role and per-object checks are not fully enforced (see ADR-0011 and the review's risks #1–2).

**Neutral**
- Using the framework's JWT bearer + `PasswordHasher` keeps the crypto primitives standard and well-reviewed rather than hand-rolled.

## Alternatives considered

- **Asymmetric RS256 from day one.** The better end-state (validators can't mint), and the intended migration — deferred only to keep the initial wiring a single shared secret; captured as a hardening item rather than pre-built.
- **An external IdP / OIDC provider (Entra ID, Auth0, Duende IdentityServer).** Appropriate for a real product, but heavier than the demonstration needs and would obscure the token flow the project is showing; the boundary (Identity issues, gateway validates) is designed so an external IdP could slot in later.
- **Session cookies + server-side session store.** Rejected: reintroduces shared state and doesn't fit the stateless, gateway-validated posture; an httpOnly cookie *transport* for the JWT is still on the table as an XSS mitigation (60-day item) independent of this decision.
