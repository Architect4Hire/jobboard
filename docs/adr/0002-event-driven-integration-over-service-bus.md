# ADR-0002: Event-driven integration over Azure Service Bus

- **Status:** Accepted
- **Date:** 2026-07-18
- **Deciders:** Robert Felkins
- **Related:** ADR-0001 (database-per-service), ADR-0003 (outbox), ADR-0004 (inbox), ADR-0010 (contracts leaf), `docs/high-level-design.md` §6, `.claude/rules/messaging.md`

## Context

With database-per-service (ADR-0001), services still need to coordinate: closing a job must close its applications; posting a job, submitting an application, and changing an application's status must all produce notifications. Two integration styles are on the table — **synchronous request/response** between services, or **asynchronous events**. Synchronous chatter re-couples services (a caller is only as available as its callee, and a chain of calls multiplies latency and failure) and tends to leak commands and domain types across boundaries. The system's top quality attribute is *correctness under partial failure*, which favours temporal decoupling.

## Decision

**We will integrate services exclusively through asynchronous integration events over Azure Service Bus. Events are past-tense facts, never commands.**

- Cross-context communication is **only** via events (`JobPosted`, `JobClosed`, `ApplicationSubmitted`, `ApplicationStatusChanged`) — immutable `record`s implementing `IIntegrationEvent`, living in `JobBoard.Contracts`.
- Events are **facts about what happened**, carrying IDs plus the *minimum denormalized data* a consumer needs to avoid a call-back (e.g. `JobPosted` carries `Title` + `Location`).
- **No synchronous service-to-service calls** and **no reaching into another service's database**. A consumer reacts by doing work in *its own* store.
- Events are published through each service's transactional outbox (ADR-0003) and consumed idempotently via an inbox (ADR-0004).

## Consequences

**Positive**
- Temporal decoupling: a consumer can be down when an event is published and still process it later. Publishers don't know or wait for consumers.
- The event set is a small, explicit, versionable contract surface (ADR-0010).
- New reactions (e.g. a future analytics service) attach as new subscribers without touching publishers.

**Negative**
- Eventual consistency across services — a UI may briefly see "job closed" before "applications closed." This must be designed for, not assumed away.
- Debugging a cross-service flow requires correlation across the bus (today a gap; the 60-day plan adds `traceparent` propagation through the outbox).
- Changing an existing event is a breaking **contract change** affecting every consumer; it must be treated as such.

**Neutral**
- Choosing Azure Service Bus specifically (over RabbitMQ/Kafka) aligns with the .NET/Azure target and runs locally via the emulator (ADR-0008). The outbox/inbox mechanism is transport-agnostic; only the thin send/receive binding is Service Bus-specific.

## Alternatives considered

- **Synchronous REST/gRPC between services.** Rejected: re-introduces availability/latency coupling and invites command-style calls and shared DTOs across boundaries.
- **Kafka / event streaming.** Rejected for this scope: a log-oriented broker is heavier than needed for fact notification, and Service Bus's topics/subscriptions + at-least-once semantics fit the pattern directly on the .NET/Azure target.
- **Shared database as the integration point** (services poll a common table). Rejected: violates ADR-0001 outright.
