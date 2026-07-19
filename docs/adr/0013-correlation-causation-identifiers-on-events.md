# ADR-0013: Correlation & causation identifiers on integration events

- **Status:** Proposed
- **Date:** 2026-07-19
- **Deciders:** Robert Felkins
- **Related:** ADR-0002 (events), ADR-0010 (contracts leaf), ADR-0011 (actor propagation), ADR-0014 (audit trail), `docs/high-level-design.md` §6

## Context

`IIntegrationEvent` today carries a single field — a `Guid Id` (ADR-0002, ADR-0004) — used as the Service Bus `MessageId` and the inbox dedupe key. That is enough to deliver and deduplicate a message, but not enough to answer the question a support audit trail (ADR-0014) exists to answer: **"what happened to this one request, end to end, across every service it touched?"**

A single browser action fans out across services: a request to close a job publishes `JobClosed`, which causes Applications to publish `ApplicationStatusChanged`, which causes Notifications to record a message. Nothing in the event stream ties those three facts back to the originating request, or records *which* event caused *which*. There is also no **actor** on an event — no trustworthy record of who performed the action (the identity seam is ADR-0011).

This is a **Contracts** decision because it changes the shape every publisher and every consumer depends on, and because it defines what "one request's story" even means. It is deliberately **not** OpenTelemetry: this is a durable, queryable *business* correlation for support, not ephemeral engineering telemetry.

## Decision

**We will extend `IIntegrationEvent` so every integration event carries a `CorrelationId`, a `CausationId`, and the acting identity — a durable thread that lets the audit trail reconstruct any request's cradle-to-grave story.**

- **`CorrelationId`** — constant across the entire fan-out of one originating request. Minted at the gateway (ADR-0011's edge) when a request arrives without one, and propagated inward; every event a request spawns, directly or transitively, carries the same value.
- **`CausationId`** — the `Id` of the event (or inbound command) that *directly* caused this event. For a request-initiated event it is the request's own id; for a follow-on event it is the parent event's `Id`. `JobClosed → ApplicationStatusChanged` sets the latter's `CausationId = JobClosed.Id`, while both keep the original `CorrelationId`. This yields a **causal tree**, not just a flat timeline.
- **Actor** — the authenticated identity that performed the action, taken from the propagated edge identity (ADR-0011), **never** from body-supplied ids. Carried on the event so the audit trail records *who*.
- **Where it's set.** Business *builds* the event (as today) and stamps these fields from the ambient request context; the outbox write and the dispatcher (ADR-0003) are otherwise unchanged. `Contracts` stays a leaf (ADR-0010) — these are plain fields, no behavior, no EF.

## Consequences

**Positive**
- The audit trail (ADR-0014) can reconstruct a request's full lifecycle by `CorrelationId` and its causal structure by `CausationId` — the whole point of the feature.
- Correlation is **durable and queryable**, living in the event and the audit record, not in an ephemeral trace store.
- "Who did it" becomes a first-class, trustworthy fact on every event, closing a gap that ADR-0011 opens the door to.

**Negative**
- Changing `IIntegrationEvent` is a **contract change touching every event record, publisher, and consumer** — a coordinated change (and, per `CLAUDE.md`, a design conversation before code because it spans services). The `api-contract-checker` and every service's tests gate it.
- Every publish site must now supply the ambient context; a publish that leaves `CorrelationId`/actor unset is a defect the `audit.md` rule and review exist to catch.
- Depends on ADR-0011 being implemented for a trustworthy actor — until then, actor is only as good as the identity seam.

**Neutral**
- These fields are independent of any broker- or transport-level metadata; they hold across redelivery and long outages because they live in the payload, not in a message header window.

## Alternatives considered

- **Carry trace context in transport metadata only (`ServiceBusMessage.ApplicationProperties`).** Rejected as the primary mechanism: transport headers don't land in the durable audit record a support query reads, and they're awkward for an agent to query after the fact. Putting the ids in the event makes them part of the recorded fact.
- **W3C `traceparent` / OpenTelemetry span propagation across the bus.** Rejected for this use case: that serves engineers debugging latency, is ephemeral, and answers "how slow," not "what happened to this request." Support needs a durable, queryable business record — see ADR-0014.
- **`CorrelationId` only, no `CausationId`.** Rejected: correlation alone gives a flat list of everything in a request; causation preserves the parent→child structure that makes a fan-out legible (which event triggered which).
- **Status quo (`Id` only).** Rejected: it cannot answer the cross-service lifecycle question the audit trail is for.
