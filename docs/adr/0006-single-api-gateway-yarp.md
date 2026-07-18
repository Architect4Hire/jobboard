# ADR-0006: Single YARP gateway as the only public door

- **Status:** Accepted
- **Date:** 2026-07-18
- **Deciders:** Robert Felkins
- **Related:** ADR-0007 (JWT at the edge), ADR-0008 (Aspire wiring), ADR-0011 (identity propagation — proposed), `docs/high-level-design.md` §4, §8.1, `.claude/rules/gateway.md`
- **Implements:** `JobBoard.Gateway/Program.cs`, `JobBoard.Gateway/appsettings.json`

## Context

With five services and an Angular SPA, the front end needs *one* stable place to talk to, and the system needs *one* place to enforce edge concerns (authentication, and later CORS, rate limiting, correlation). Exposing every service directly to the browser would spread auth and cross-cutting policy across five hosts, leak the internal topology to clients, and make the service boundaries part of the public contract (so they couldn't move without breaking the SPA).

## Decision

**We will place a single YARP reverse-proxy gateway in front of the services as the only public entry point. The Angular app talks to nothing else.**

- **Only public door.** Services are not exposed to the browser. New public routes are added at the gateway; a service endpoint with no gateway route is unreachable by design (fine for internal-only endpoints).
- **Routing by resource.** Routes/clusters are loaded from configuration and matched by path (`/jobs/**`, `/applications/**`, …). Destinations are named by **Aspire resource** (`http://jobs`) and resolved through service discovery (ADR-0008) — using the model, not hardcoding an address.
- **Auth at the edge.** The gateway validates JWTs (ADR-0007) *before* proxying a protected route; a route names an `AuthorizationPolicy` (e.g. `authenticated`) to require a valid token.
- **The gateway stays declarative** — no business logic; it authenticates, authorizes, and routes.
- **The SPA↔gateway contract is the stable one;** the service boundaries behind it can move.

## Consequences

**Positive**
- One place to enforce edge policy; one stable base URL for the client.
- Internal topology is hidden; services can be split/merged/renamed without touching the SPA, as long as the gateway's public routes hold.
- Auth is enforced once, at the boundary, instead of five times.

**Negative**
- The gateway is a **single logical entry point** — it must be made highly available and is a natural bottleneck to watch (standard for the pattern; mitigated by running multiple replicas in production).
- Edge cross-cutting that isn't there yet (CORS, rate limiting) is now *the gateway's* job to add (30-day plan items).
- Coarse-grained routing at the edge is not a substitute for per-object authorization inside services (see ADR-0011).

**Neutral**
- YARP specifically (over Ocelot/Envoy/nginx) is chosen because it's the first-party .NET reverse proxy, integrates with Aspire service discovery, and keeps the gateway an ASP.NET Core app like everything else.

## Alternatives considered

- **Expose services directly (no gateway).** Rejected: scatters auth/CORS/rate-limiting across services, leaks topology, and freezes service boundaries into the public contract.
- **A third-party/edge gateway (nginx, Envoy, Ocelot, cloud API gateway).** Reasonable in production, but YARP keeps the edge in-process with the same stack and Aspire discovery, which fits the local-first demonstration; a managed edge can front YARP later without changing services.
- **Backend-for-frontend (BFF) per client.** Overkill at one client; the single gateway already gives the SPA a stable surface. A BFF can be introduced later if a second, differently-shaped client appears.
