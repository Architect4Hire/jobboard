# ADR-0010: Contracts as a leaf library; status crosses as strings

- **Status:** Accepted
- **Date:** 2026-07-18
- **Deciders:** Robert Felkins
- **Related:** ADR-0002 (events), ADR-0005 (layering), `docs/high-level-design.md` §4.1, §8, `.claude/rules/messaging.md`
- **Implements:** `JobBoard.Contracts/*` (`IIntegrationEvent`, `JobPosted`, `JobClosed`, `ApplicationSubmitted`, `ApplicationStatusChanged`)

## Context

Event-driven integration (ADR-0002) requires *some* shared code — the event record types both publisher and consumer serialize against. Shared code between services is exactly where coupling creeps in: if the shared library grows to hold DTOs, helpers, or (worst) shared **domain enums**, a change in one service's domain silently becomes a breaking change for others, and the boundary erodes. The question is how to share the *minimum* necessary — the event shape — without sharing anything that couples domains.

## Decision

**We will keep `JobBoard.Contracts` a strict leaf library containing only integration-event records, and cross enumerable values (e.g. status) as strings, not shared enums.**

- **Contracts is a leaf.** It references nothing; everything else may reference it. It holds event `record`s implementing `IIntegrationEvent` (a `Guid Id`) and the marker interface — **no** entities, ServiceModels, EF, helpers, or DTOs that aren't events.
- **Events carry primitives + minimal denormalized data.** IDs plus the least a consumer needs to avoid a call-back (e.g. `JobPosted(JobId, EmployerId, Title, Location, PostedOnUtc)`).
- **Status crosses as a string.** An application/job status is serialized as a string in the event, and each service maps it to/from its *own* domain enum internally. No service's enum type lives in Contracts.
- **Changing an existing event is a contract change** affecting every consumer, and is treated as one (additive/versioned, not silently mutated).

## Consequences

**Positive**
- The shared surface is as small as it can be — event shapes only — so cross-service coupling is minimized.
- No service's domain type leaks across the boundary; a service can refactor its internal `JobStatus`/`ApplicationStatus` enum freely as long as the string values it emits stay stable.
- Contracts being a true leaf keeps the reference graph acyclic (ADR-0005) by construction.

**Negative**
- **String status trades compile-time safety for decoupling:** a typo or a renamed value becomes a *runtime* mismatch, not a build error. This is a real risk (the review flags "silent enum contract-drift") and is mitigated by a planned **contract test / generated client** (60-day item) that turns drift into a build failure.
- Denormalized data in events can go stale relative to the source (accepted — events are facts at a point in time; consumers keep their own copy).
- Versioning discipline is manual: evolving an event safely (additive fields, new versioned types) is a convention to uphold, not something the type system enforces across services.

**Neutral**
- Serialization uses `JsonSerializerDefaults.Web` on both sides (outbox serialize / processor deserialize) so payloads round-trip; the runtime type is used on serialize so derived record fields are captured.

## Alternatives considered

- **Shared domain enums in Contracts** (so status is strongly typed across services). Rejected: it puts a domain type in the shared library, coupling every consumer to the publisher's enum and inviting exactly the erosion this ADR prevents.
- **A richer shared model library (DTOs, helpers).** Rejected: turns Contracts into a coupling magnet; anything beyond the event shape belongs in a service's `.Core`.
- **A schema registry (Avro/Protobuf) for events.** Heavier than this scope needs; the leaf-record approach plus a future contract test achieves drift-detection without the extra infrastructure. Revisit if the event set or consumer count grows substantially.
