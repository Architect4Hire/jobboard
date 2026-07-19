# ADR-0014: The Audit bounded context — a bus-fed support audit trail

- **Status:** Accepted
- **Date:** 2026-07-19
- **Deciders:** Robert Felkins
- **Related:** ADR-0001 (database-per-service), ADR-0002 (events), ADR-0004 (idempotent inbox), ADR-0012 (cross-service read model), ADR-0013 (correlation/causation ids), ADR-0015 (actor projection), ADR-0011 (identity remediation), `docs/high-level-design.md` §6, §8.1

## Context

Support — a human operator or an agent — needs to reconstruct the **cradle-to-grave story of a request or entity**: who registered, who posted the job, who applied, who moved the application to `Offered`, and what notifications that triggered — across every service, from **one** place, and it must survive process restarts so it can be queried later.

The system already emits that story as integration events through each service's outbox (`JobPosted`, `JobClosed`, `ApplicationSubmitted`, `ApplicationStatusChanged`), and ADR-0013 makes each event carry the `CorrelationId`, `CausationId`, and actor needed to stitch and attribute it. What's missing is a **durable, queryable collector**.

Two boundaries constrain the answer. **No shared database** (ADR-0001): a per-service audit table would force a support query to fan out across five databases to assemble one lifecycle — the opposite of "query it from one place." And **adding a service is a deliberate architectural decision** (`CLAUDE.md`), not something to scaffold unprompted. So this ADR exists to *propose* that service explicitly.

## Decision

**We will introduce a dedicated, consumer-only `JobBoard.Audit` bounded context that subscribes to every business event and appends it to its own `auditdb`, exposing a read-only support-query surface through the gateway.** It is an instance of the cross-service read-model strategy in ADR-0012.

- **A new service, `JobBoard.Audit` (+ `.Core`).** Thin host + layered `.Core` like every other service (ADR-0005); it has **consumers, no public mutations**. It owns `auditdb` and writes only it (ADR-0001).
- **It consumes every business event.** An `Audit<Event>Consumer` (or one generic sink) subscribes to all integration-event topics and, for each message, appends one **immutable** row: event type, `CorrelationId`, `CausationId`, actor, the relevant entity ids, the occurred-at timestamp, and the full event as a **`jsonb`** payload. Consumption is idempotent via the inbox in the same transaction as the append (ADR-0004) — a redelivery must not double-write.
- **Append-only.** The trail is never updated or deleted; a correction is a new row. It is a *record*, not a mutable projection.
- **Postgres `auditdb` + `jsonb`.** Another database on the existing Postgres server (ADR-0008 local-first): plain SQL for the query surface, a `jsonb` column for heterogeneous event shapes, persistence across runs via the normal volume, zero new technology.
- **One read-only query route at the gateway.** Support queries — by `CorrelationId` (a request's whole fan-out), by entity id (one application's life), by actor, or by time window — go through a single auth-protected gateway route (ADR-0006) to the Audit service. `auditdb` is never exposed directly.
- **A read model, never a source of truth.** Audit never calls back into a service and never drives domain behavior; the owning service stays authoritative (ADR-0001, ADR-0012).

Depends on **ADR-0013** (the ids to stitch and attribute by) and **ADR-0015** (the identity-projection mechanism that supplies a trustworthy actor). The broader BOLA/IDOR remediation (ADR-0011) is deliberately *not* a prerequisite — it ships separately; until it does, the actor is trustworthy for attribution but authorization hardening remains outstanding (see ADR-0015).

## Consequences

**Positive**
- One durable, queryable store answers "what happened to this request/entity," across services, after the fact — the support use case, met from one place.
- Boundaries hold: no shared database, no service reaching into another, the gateway still the only public door, consumers still idempotent.
- Append-only + `jsonb` keeps the sink dead simple and tolerant of new event shapes without migrations for every event.

**Negative**
- **A new service** to build, run, and operate — the deliberate expansion this ADR asks approval for.
- **`auditdb` grows unbounded** without a retention/archival policy — the same housekeeping debt noted for the inbox in ADR-0004; a pruning/rollup job is a follow-up.
- **Coverage is a discipline:** the trail is only cradle-to-grave if every mutating action is represented by an audited event, including actions that don't publish today (account created, login, profile updated). The `audit.md` rule and the `add-audit-event` skill exist to enforce this.

**Neutral**
- Audit subscribes to the *same* events other consumers already receive; it adds a subscriber, not a new publish path. New event types are audited by adding a subscription, not by changing publishers.

## Alternatives considered

- **Fold audit into the Notifications service.** Tempting — Notifications already consumes events and is described as a "write-only audit log." Rejected: it conflates two concerns (sending notifications vs. recording history) and makes `notificationsdb` dual-purpose; audit deserves its own boundary and lifecycle.
- **Per-service audit tables via a `Shared` mechanism.** Respects database-per-service most strictly, but a full-lifecycle support query must then fan out across five databases and stitch results — poor fit for "one place," and it spreads retention/query concerns everywhere. Rejected.
- **A Cosmos DB store (local emulator) keyed by `CorrelationId`.** Schemaless documents are a fair match for heterogeneous events, but the Linux Cosmos emulator is preview-grade, it would be the only non-Postgres store in the stack, and its query dialect is less ubiquitous for an agent than SQL. Postgres + `jsonb` already gives document-shaped flexibility with a SQL query surface. Rejected on risk/consistency.
- **An OpenTelemetry trace backend (Jaeger/Seq/Tempo).** Purpose-built for spans and latency, but ephemeral-by-default, engineering-facing, and trace-centric — it answers "how slow," not "what happened to this request, who did it." Wrong tool for a support audit. Rejected (see ADR-0013).
