# Architecture Decision Records

This directory records the **load-bearing architectural decisions** for JobBoard — the ones that are expensive to reverse and that every contributor (human or agent) needs to understand and honor. Each ADR captures the *context* that forced a decision, the *decision* itself, its *consequences*, and the *alternatives* that were weighed and rejected.

ADRs are immutable once **Accepted**: to change a decision, add a new ADR that **supersedes** the old one (and mark the old one `Superseded by ADR-NNNN`). This keeps the history of *why* intact.

## Index

| #                                                            | Title                                                           | Status   |
| ------------------------------------------------------------ | --------------------------------------------------------------- | -------- |
| [0001](./0001-microservices-database-per-service.md)         | Microservices with database-per-service                         | Accepted |
| [0002](./0002-event-driven-integration-over-service-bus.md)  | Event-driven integration over Azure Service Bus                 | Accepted |
| [0003](./0003-hand-rolled-transactional-outbox.md)           | Hand-rolled transactional outbox                                | Accepted |
| [0004](./0004-idempotent-inbox-at-least-once-delivery.md)    | Idempotent inbox over at-least-once delivery                    | Accepted |
| [0005](./0005-thin-host-core-layered-library.md)             | Thin host + `.Core` layered library; one-way acyclic references | Accepted |
| [0006](./0006-single-api-gateway-yarp.md)                    | Single YARP gateway as the only public door                     | Accepted |
| [0007](./0007-identity-issued-symmetric-jwt.md)              | Identity-issued symmetric (HS256) JWT validated at the edge     | Accepted |
| [0008](./0008-aspire-local-first-servicebus-emulator.md)     | Aspire local-first topology + Service Bus emulator              | Accepted |
| [0009](./0009-read-through-cache-generation-invalidation.md) | Fail-open read-through cache with generation-token invalidation | Accepted |
| [0010](./0010-contracts-leaf-status-as-string.md)            | Contracts as a leaf library; status crosses as strings          | Accepted |
| [0011](./0011-token-derived-identity-propagation.md)         | Token-derived identity propagation at the gateway               | Proposed |
| [0012](./0012-cross-service-read-model-strategy.md)          | Cross-service read-model / query composition strategy           | Proposed |
| [0013](./0013-correlation-causation-identifiers-on-events.md) | Correlation & causation identifiers on integration events       | Proposed |
| [0014](./0014-audit-bounded-context-bus-fed-support-trail.md) | The Audit bounded context — a bus-fed support audit trail       | Proposed |

## Statuses

- **Proposed** — under discussion; not yet binding.
- **Accepted** — the current, binding decision.
- **Superseded** — replaced by a later ADR (named in the header).
- **Deprecated** — no longer relevant, not replaced.

## Template

New ADRs follow the shape used throughout this directory:

```markdown
# ADR-NNNN: <short decision title>

- **Status:** Proposed | Accepted | Superseded by ADR-XXXX | Deprecated
- **Date:** YYYY-MM-DD
- **Deciders:** <who>
- **Related:** ADR-XXXX, docs/high-level-design.md §N, CLAUDE.md

## Context
<The forces at play: requirements, constraints, the problem that demands a decision.>

## Decision
<The decision, stated plainly and actively: "We will …".>

## Consequences
**Positive** / **Negative** / **Neutral** — what becomes easier, what becomes harder, what we accept.

## Alternatives considered
<Each rejected option and *why* it lost.>
```

See also: [`docs/high-level-design.md`](../high-level-design.md) (design narrative), [`CLAUDE.md`](../../CLAUDE.md) (enforceable ruleset), [`docs/ongoing-architecture-plan.md`](../ongoing-architecture-plan.md) (review & 30/60/90 plan).
