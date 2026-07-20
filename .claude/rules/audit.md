---
paths:
  - src/JobBoard.Contracts/**
  - src/JobBoard.Gateway/**
  - src/JobBoard.Audit/**
  - src/JobBoard.Audit.Core/**
  - src/JobBoard.*/Consumers/**
  - src/JobBoard.*.Core/Business/**
  - src/JobBoard.*.Core/Managers/Mappers/**
---
# Audit rules — support audit trail (correlation, causation, actor)

JobBoard keeps a **support audit trail**: a durable, queryable record of what happened to a request or
entity over its lifecycle, so support (human or agent) can reconstruct any request cradle-to-grave from
one place. It is a **read model fed by the bus** (ADR-0014, ADR-0012), threaded by identifiers on the
event contract (ADR-0013) — deliberately **not** OpenTelemetry tracing. The store is `auditdb`
(Postgres + `jsonb`), owned by the `JobBoard.Audit` service.

- **Every request carries a `CorrelationId`, minted at the edge.** The gateway generates one when a
  request arrives without it, **strips any client-supplied copy** so it can't be spoofed, forwards it
  inward, and echoes it on the response for support. Never trust a client's correlation id past the
  gateway.
- **Every integration event carries correlation, causation, and actor.** `CorrelationId` stays constant
  across a request's whole fan-out; `CausationId` is the `Id` of the event or command that *directly*
  caused this one (parent → child, a causal tree); actor is the authenticated identity projected by the
  edge (ADR-0015), **never** a body-supplied id. Business stamps these when it builds the event; the outbox
  and dispatcher path is otherwise unchanged (see `.claude/rules/messaging.md`).
- **The `JobBoard.Audit` service is the only writer of `auditdb`, and it only appends.** It consumes
  every business event and writes one **immutable** row — event type, correlation, causation, actor,
  entity ids, occurred-at, and the full event as a `jsonb` payload. No updates, no deletes; a
  correction is a new row.
- **Audit consumption is idempotent, via the inbox.** Dedupe on the event `Id` in the same transaction
  as the append (ADR-0004). Redelivery must never double-write a row.
- **Audit is a read model, not a source of truth.** It never calls back into a service and never drives
  domain behavior; it only records. The owning service stays authoritative for its data (ADR-0001,
  ADR-0012).
- **Cradle to grave means every mutating action is audited.** A state-changing action with no audited
  event is a gap — actions that don't already publish (account created, login, profile updated) get an
  audit-worthy event. Shipping a new mutating endpoint includes emitting and auditing it (use the
  `add-audit-event` skill).
- **Keep secrets and needless PII out of the trail.** The `jsonb` records the event a consumer already
  sees — no tokens, passwords, or fields support doesn't need. The trail is durable and queryable;
  treat it as disclosable.
- **The support-query surface is read-only and gateway-fronted.** Queries by correlation id / entity /
  actor / time go through one auth-protected gateway route (ADR-0006) to the Audit service; never
  expose `auditdb` directly. The repeatable query workflow is the `trace-a-request` skill.

The audit trail is an instance of the read-model pattern in ADR-0012; the correlation/causation
contract is ADR-0013; the bounded context is ADR-0014. When any of those move, this rule follows.
