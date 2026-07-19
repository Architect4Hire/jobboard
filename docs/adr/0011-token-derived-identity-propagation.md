# ADR-0011: Token-derived identity propagation at the gateway

- **Status:** Proposed
- **Date:** 2026-07-18
- **Deciders:** Robert Felkins
- **Related:** ADR-0006 (gateway), ADR-0007 (JWT), ADR-0015 (the projection *mechanism*, carved out and Accepted for audit attribution), ADR-0013 / ADR-0014 (the audit trail that depends on the actor), `docs/high-level-design.md` §8.1, `docs/ongoing-architecture-plan.md` §5.1 (risks #1–2)

## Context

The current build has a **broken object-level authorization seam** (the top-ranked risk in the architecture review). Services derive the acting user from **body-supplied** values (`employerId` / `candidateId` in inbound ViewModels) rather than from the authenticated token. That means an authenticated caller can act *as someone else* by supplying a different id — a classic **BOLA/IDOR** vulnerability. Separately, roles are issued in the JWT (ADR-0007) but **not enforced** on write paths (e.g. `POST /jobs` is effectively unauthenticated at the policy level). The gateway validates the token but does not currently *propagate the caller's identity inward* in a trustworthy way.

This is an architectural decision because it defines **where identity becomes trusted** and **how it flows** from the edge to the services — a seam every write path depends on.

## Decision (proposed)

**We will make the gateway the single point that projects validated identity inward, and forbid services from trusting client-supplied identity.**

1. **Project token claims to trusted headers at the gateway.** Add a YARP transform that copies the validated `sub` and `role` into request headers (e.g. `X-User-Id`, `X-User-Role`) on proxied requests, and **strips any client-supplied copies** of those headers so they can't be spoofed.
2. **Services derive the actor from that trusted header,** not from the body. Remove `EmployerId` / `CandidateId` from all inbound ViewModels; the owning id comes from the propagated identity.
3. **Enforce role authorization at the edge.** Add `employer` and `candidate` policies keyed on the `role` claim; put `POST /jobs` behind `employer`, `/applications` writes behind `candidate`, etc. Add the matching Angular `roleGuard`.
4. **Defense in depth:** services still verify that the actor owns the resource they're mutating (per-object check), not only that they hold the right role.

## Consequences

**Positive**
- Closes the BOLA/IDOR seam: a caller can only act as themselves, and the system can prove it.
- Identity becomes trusted in exactly one place (the gateway), consistent with ADR-0006.
- Role enforcement moves from "issued but ignored" to actually gating writes.

**Negative**
- Touches every write path and the gateway at once — a coordinated change (and, per `CLAUDE.md`, a design conversation before code since it spans services).
- Header-based propagation trusts the network path between gateway and services; that trust must be enforced (services must reject these headers from any non-gateway origin, e.g. via network policy or a shared secret/mTLS in production).

**Neutral**
- An alternative propagation mechanism (forwarding the validated JWT itself and having services re-validate) is compatible with the same "derive actor from token, not body" principle; the header-projection choice trades a re-validation per hop for a network-trust assumption.

## Alternatives considered

- **Forward the JWT to services and re-validate per service.** Stronger cryptographic trust (no network-trust assumption), at the cost of every service holding validation config and re-validating on every call. Viable; pairs naturally with RS256 (ADR-0007's hardening path). To be weighed against header projection when this ADR is decided.
- **Leave authorization coarse at the edge only.** Rejected: edge role checks don't stop a correctly-roled user from acting on *another* user's object — per-object ownership must be checked with a trustworthy actor id.
- **Status quo (trust body-supplied ids).** Rejected: this is the vulnerability.

## Notes

This ADR is **Proposed** and corresponds to the 30-day, ship-blocking items in `docs/ongoing-architecture-plan.md` §9.

**Scope split (2026-07-19):** the audit-trail work needed a trustworthy actor, so the **projection mechanism** — step 1 above (gateway projects `sub`/`role` into `X-User-Id`/`X-User-Role`, strips client copies) — has been carved out and **Accepted** as ADR-0015, on the header-projection option. This ADR is now the **BOLA/IDOR remediation** that still remains: steps 2–4 (remove `EmployerId`/`CandidateId` from ViewModels, enforce role policies at the edge, per-object ownership checks). It stays Proposed and ships as a separate security effort; promote it to Accepted when that work is scheduled.
