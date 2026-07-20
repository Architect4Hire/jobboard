# ADR-0015: Gateway identity projection as trusted headers (the mechanism)

- **Status:** Accepted
- **Date:** 2026-07-19
- **Deciders:** Robert Felkins
- **Related:** ADR-0006 (gateway), ADR-0007 (JWT), ADR-0011 (BOLA/IDOR remediation — the broader, still-Proposed fix this carves the mechanism from), ADR-0013 (events carry the actor), ADR-0014 (audit trail needs the actor), `docs/design/high-level-design.md` §8.1

## Context

The support audit trail (ADR-0014) records *who* performed each action, and ADR-0013 puts that actor on every event. For the actor to be trustworthy it must come from the validated token, projected inward by the gateway — never from a client-supplied value.

ADR-0011 proposes exactly that projection, but *as one step of* a larger BOLA/IDOR remediation that also strips body-supplied ids from ViewModels and adds role-authorization policies. We need the **projection mechanism** now to give the trail a trustworthy actor; the wider remediation is a separate security effort on its own timeline (and its own write-up). This ADR carves the mechanism out so the audit work has an accepted, stable basis without waiting on — or pulling in — the full remediation.

## Decision

**We will make the gateway project validated identity inward as trusted headers, and have services read the acting identity from them. Scope is the mechanism only.**

- The gateway adds a YARP transform copying the validated `sub` (and `role`) claims into request headers — `X-User-Id`, `X-User-Role` — on proxied requests, and **strips any client-supplied copies** so they cannot be spoofed.
- Services read the actor from those headers into an ambient request context; publish sites stamp it onto events (ADR-0013).
- The mechanism is **header projection**, not per-hop JWT forwarding (the alternative weighed in ADR-0011). The trust assumption is the gateway→service network path, enforced in production by network policy / mTLS.

**Explicitly out of scope here — remains ADR-0011 (Proposed):** removing `EmployerId`/`CandidateId` from inbound ViewModels, adding employer/candidate role-authorization policies, and per-object ownership checks. Those are the BOLA/IDOR remediation and ship separately.

## Consequences

**Positive**
- The audit trail gets a **trustworthy actor now**, with a stable Accepted basis for the gateway work (SCRUB prompt A2) — without blocking on the security refactor.
- Identity becomes trusted in exactly one place, the gateway, consistent with ADR-0006.

**Negative**
- Header-based propagation **trusts the gateway→service path**; that trust must be enforced (network policy / shared secret / mTLS in production), or a caller reaching a service directly could forge the headers.
- Until ADR-0011 lands, write paths still derive *domain ownership* from body-supplied ids — so the actor is trustworthy for **attribution** while the **authorization** hardening remains outstanding. The trail records who *claims* the edge said they were; it does not yet stop a mis-scoped write. This gap is the explicit reason ADR-0011 exists and stays open.

**Neutral**
- Adopting header projection here doesn't preclude moving to JWT forwarding later — that would supersede this *mechanism*, not the audit contract, which only depends on "a trustworthy actor exists."

## Alternatives considered

- **Accept ADR-0011 in full now.** Rejected for this sequence: it balloons the audit work into a security refactor (ViewModel changes across every write path + role policies). Deliberately deferred as a separate fix.
- **Forward the JWT and re-validate per service.** Stronger cryptographic trust, no network-trust assumption, at the cost of every service holding validation config and re-validating each call. Viable, weighed in ADR-0011; deferred — header projection suffices for attribution.
- **Take the actor from the request body (status quo).** Rejected: spoofable — the very seam ADR-0011 exists to close. An audit "who" built on it would be untrustworthy.
