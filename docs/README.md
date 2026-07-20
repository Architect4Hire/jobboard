# JobBoard Documentation

The design, decisions, and prompts behind JobBoard — an event-driven microservice job board built on **Aspire + ASP.NET Core + Angular (.NET 10)**, driven with Claude Code under the SCRUB framework.

Everything the docs reference — the constitution ([`../CLAUDE.md`](../CLAUDE.md)) and the toolkit ([`../.claude/`](../.claude/): rules, skills, subagents, hooks) — already lives in the repo.

## Contents

### Design & architecture ([`design/`](./design/))

| Document | What's inside |
| --- | --- |
| [High-Level Design](./design/high-level-design.md) | The *target* architecture the repo builds — services, seams, boundaries, and the design decisions that hold it together. Living document. |
| [Ongoing Architecture Plan](./design/ongoing-architecture-plan.md) | A grounded review of the current spike (physical, logical, conceptual — service by service), benchmarked against industry practice, with a ranked risk table and a 30/60/90-day plan. |
| [Product Completeness](./design/product-completeness.md) | The complementary *product* roadmap: capability-by-capability, what a candidate and employer can do today vs. what a fuller job board needs — the applicant-review funnel, search, account lifecycle, real notifications — each named to its service, with a value-tiered build order. |

### Decisions ([`adr/`](./adr/))

| Document | What's inside |
| --- | --- |
| [Architecture Decision Records](./adr/README.md) | The load-bearing decisions — expensive to reverse, binding on every contributor. Each ADR captures context, decision, consequences, and rejected alternatives. See the [index](./adr/README.md#index) below. |

<details>
<summary><b>ADR index</b> (15 records)</summary>

| # | Title | Status |
| --- | --- | --- |
| [0001](./adr/0001-microservices-database-per-service.md) | Microservices with database-per-service | Accepted |
| [0002](./adr/0002-event-driven-integration-over-service-bus.md) | Event-driven integration over Azure Service Bus | Accepted |
| [0003](./adr/0003-hand-rolled-transactional-outbox.md) | Hand-rolled transactional outbox | Accepted |
| [0004](./adr/0004-idempotent-inbox-at-least-once-delivery.md) | Idempotent inbox over at-least-once delivery | Accepted |
| [0005](./adr/0005-thin-host-core-layered-library.md) | Thin host + `.Core` layered library; one-way acyclic references | Accepted |
| [0006](./adr/0006-single-api-gateway-yarp.md) | Single YARP gateway as the only public door | Accepted |
| [0007](./adr/0007-identity-issued-symmetric-jwt.md) | Identity-issued symmetric (HS256) JWT validated at the edge | Accepted |
| [0008](./adr/0008-aspire-local-first-servicebus-emulator.md) | Aspire local-first topology + Service Bus emulator | Accepted |
| [0009](./adr/0009-read-through-cache-generation-invalidation.md) | Fail-open read-through cache with generation-token invalidation | Accepted |
| [0010](./adr/0010-contracts-leaf-status-as-string.md) | Contracts as a leaf library; status crosses as strings | Accepted |
| [0011](./adr/0011-token-derived-identity-propagation.md) | Token-derived identity propagation (BOLA/IDOR remediation) | Proposed |
| [0012](./adr/0012-cross-service-read-model-strategy.md) | Cross-service read-model / query composition strategy | Proposed |
| [0013](./adr/0013-correlation-causation-identifiers-on-events.md) | Correlation & causation identifiers on integration events | Accepted |
| [0014](./adr/0014-audit-bounded-context-bus-fed-support-trail.md) | The Audit bounded context — a bus-fed support audit trail | Accepted |
| [0015](./adr/0015-gateway-identity-projection-header-mechanism.md) | Gateway identity projection as trusted headers (the mechanism) | Accepted |

</details>

### Building the system ([`developer/`](./developer/), [`prompts/`](./prompts/))

| Document | What's inside |
| --- | --- |
| [Tracing a Slice: Applying to a Job](./tracing-a-slice-apply-to-a-job.md) | A new developer's walkthrough of one complete request, start to finish — Angular → gateway → Applications' full controller → facade → business → data → repository stack → outbox → Service Bus → two independent consumers (Notifications, Audit) — with the real code at every hop. Read this first to see how the pieces fit together before building your own. |
| [Adding an Endpoint by Hand](./developer/adding-an-endpoint-manually.md) | A developer's step-by-step walkthrough for adding a REST endpoint (or event-driven consumer) to a service — the full controller → facade → business → data → repository slice, outbox, gateway route, and tests — done manually, without Claude Code. Grounded in the real Jobs service code. |
| [Adding Seed Data](./developer/adding-seed-data.md) | How to add development-only demo data so the per-service databases come up populated after `aspire run` — where seeders live, the idempotency + well-known-id conventions, host wiring, and how to re-seed. Grounded in the real service seeders. |
| [Tracing the Outbox: `JobPosted`](./developer/tracing-the-outbox-job-posted.md) | One event, every layer: `POST /jobs`'s `JobPosted` walked from controller through facade, business, mappers, data layer, repository, and the outbox row it becomes, down to the background relay — full code at each hop, plus a sequence diagram. |
| [Pattern Deep Dives](./developer/patterns/README.md) | Twelve mechanisms — layering, the outbox/inbox, caching, concurrency, the gateway, auth propagation, the audit trail, event contracts, Aspire orchestration, error handling, the frontend seam — each explained end to end from the real code that implements it, with links to the ADR that decided it. |
| [SCRUB Prompts](./prompts/scrub-prompts.md) | The prompts that stand the system up and keep it consistent. *Part 1 — Scaffolding:* a one-time, ordered sequence to create `src/`. *Part 2 — Operational templates:* reusable prompts for feature slices, cross-service events, migrations, refactors, and debugging. |
| [Audit Trail SCRUB Prompts](./prompts/audit-scrub-prompts.md) | Prompts for building the support audit trail (the `JobBoard.Audit` bounded context, correlation/causation ids, actor propagation) — the same SCRUB skeleton and golden rule as the main prompts, fixed by ADR-0011/0013/0014. |

## Where to start

- **New to the project?** Read the [High-Level Design](./design/high-level-design.md), then walk [Tracing a Slice: Applying to a Job](./tracing-a-slice-apply-to-a-job.md) to see one real request move through every layer before skimming the [ADRs](./adr/README.md).
- **Building it from scratch?** Start a Claude Code session at the repo root and begin with [SCRUB Prompts](./prompts/scrub-prompts.md), Part 1, Prompt 0.
- **Adding a feature by hand?** Follow [Adding an Endpoint by Hand](./developer/adding-an-endpoint-manually.md) — the full controller → repository slice, no Claude Code required.
- **Understanding or changing a specific mechanism?** See the [Pattern Deep Dives](./developer/patterns/README.md) — outbox/inbox, caching, concurrency, the gateway, auth propagation, the audit trail, and more.
- **Reviewing where it stands?** See the [Ongoing Architecture Plan](./design/ongoing-architecture-plan.md).
- **Deciding what to build next?** See [Product Completeness](./design/product-completeness.md) — the feature gaps, tiered by value.
- **Building the audit trail?** See [Audit Trail SCRUB Prompts](./prompts/audit-scrub-prompts.md), alongside [ADR-0013](./adr/0013-correlation-causation-identifiers-on-events.md), [ADR-0014](./adr/0014-audit-bounded-context-bus-fed-support-trail.md), and [ADR-0015](./adr/0015-gateway-identity-projection-header-mechanism.md).
