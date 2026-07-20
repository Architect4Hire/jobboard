# ADR-0012: Cross-service read-model / query composition strategy

- **Status:** Proposed
- **Date:** 2026-07-18
- **Deciders:** Robert Felkins
- **Related:** ADR-0001 (database-per-service), ADR-0002 (events), `docs/design/high-level-design.md` §10–11, `docs/design/ongoing-architecture-plan.md` §1, §9 (90-day item #15)

## Context

Database-per-service (ADR-0001) means no cross-service JOIN. The moment a screen needs data spanning services — the canonical example is **"my applications, with the job title and the employer name"** — that data lives in three services (Applications, Jobs, Profiles). Today the frontend stitches it client-side because the events already carry denormalized ids/fields. That's acceptable at the current size, but there is **no articulated position** on how cross-service reads are composed, and that decision shapes the next several months of feature work. Left unnamed, it will be made ad hoc, differently, on each screen — the expensive kind of drift.

## Decision (proposed)

**We will explicitly choose and document one cross-service read strategy before the first screen forces the hand — and we will *not* pre-build a heavy CQRS/event-sourcing rig to solve a problem we don't yet have.**

The realistic options, to be decided:

- **(A) API composition at the gateway/BFF.** A composition endpoint fans out to the owning services and assembles the response. Simplest; no new stores; read latency and availability are the sum of the callees.
- **(B) Materialized read models (CQRS-style projections).** A read-optimized store per query need, kept current by subscribing to the same integration events (ADR-0002). Fast, resilient reads; adds a projection to build, own, and keep consistent.
- **(C) Event-carried state transfer (status quo, formalized).** Consumers keep local denormalized copies of what they need (as `JobClosed`/`JobPosted` already do), and reads stay within one service. Works well when the needed data is small and naturally travels on events.

The decision names **one default** and the trigger conditions under which a specific screen may use another.

## Consequences

**Positive**
- One consistent, intentional approach to cross-service reads instead of per-screen improvisation.
- The decision is made *before* it's forced, when it's cheap, rather than under feature pressure.

**Negative**
- Each option carries a cost: (A) couples read availability to multiple services; (B) adds projection infrastructure and eventual-consistency lag to reason about; (C) spreads denormalized copies that can go stale and must be maintained as events evolve.
- Deferring the *decision* too long risks the ad-hoc drift this ADR exists to prevent — so this ADR should be resolved on the 90-day horizon, not indefinitely.

**Neutral**
- The choice is not all-or-nothing: a sensible end-state may be "(A) by default, (B) for the few hot cross-service reads, (C) where the data is small enough to ride events." The ADR should still name the default and the escalation rule.

## Alternatives considered

- **Build full CQRS + event sourcing now.** Rejected (explicitly, per the review's "what *not* to do"): solving a read-model problem you don't yet have with a heavy rig is over-engineering. *Decide* the strategy; don't pre-build it.
- **Keep composing client-side forever.** Rejected as a *strategy*: pushing cross-service composition into the SPA leaks the service topology to the client, duplicates assembly logic per screen, and makes authorization/consistency harder to reason about. Fine as the current tactic; not a decision.
- **Reach across databases for reads.** Rejected outright: violates ADR-0001.

## Notes

This ADR is **Proposed** and corresponds to 90-day plan item #15 in `docs/design/ongoing-architecture-plan.md`. Promote to **Accepted** once the default strategy and escalation rule are chosen and applied to the first real cross-service screen.
