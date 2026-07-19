---
paths:
  - src/JobBoard.Gateway/**
---
# Gateway rules — YARP reverse proxy

`JobBoard.Gateway` is the **only** public entry point. The Angular app talks to the gateway and
nothing else; individual services are never exposed to the browser. Keep the gateway declarative —
routing and cross-cutting edge concerns only, no business logic.

- **Route by Aspire resource name, not by address.** YARP `Clusters` name destinations by service
  discovery name (e.g. `http://jobs`, `http://applications`), which Aspire resolves. Never a literal
  host:port. This is the one sanctioned place a service name appears in config, because discovery
  reads it.
- **Every client-facing endpoint needs a route.** A service endpoint with no gateway route is
  internal-only by design (fine — e.g. Notifications has none). A client-facing endpoint without a
  route is unreachable; adding the route is the last step of shipping it (see the `add-endpoint`
  skill).
- **Auth at the edge.** JWTs issued by the Identity service are validated at the gateway; protected
  routes require a valid token before proxying. Keep signing keys/secrets out of source — wire via
  config/Aspire.
- **Edge cross-cutting only.** Rate limiting, CORS for the Angular origin, and request/trace
  propagation belong here; anything domain-specific belongs in a service. The gateway calls
  `AddServiceDefaults()` so its traces join the rest. Minting and forwarding the per-request
  `CorrelationId` (and projecting the caller's identity, ADR-0011) is part of this — see
  `.claude/rules/audit.md`.
- **Stable public contract.** The gateway's public paths are the contract the frontend depends on;
  the service boundaries behind them can move without the client noticing. Don't reshape a public
  path casually.

Verify YARP-with-Aspire wiring (transforms, service-discovery destination syntax, package names)
against https://aspire.dev and the YARP docs — these move between versions.
