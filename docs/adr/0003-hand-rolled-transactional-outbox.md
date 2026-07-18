# ADR-0003: Hand-rolled transactional outbox as the publish mechanism

- **Status:** Accepted
- **Date:** 2026-07-18
- **Deciders:** Robert Felkins
- **Related:** ADR-0002 (events), ADR-0004 (inbox), `docs/high-level-design.md` §6, `.claude/rules/messaging.md`
- **Implements:** `JobBoard.Shared/Messaging/{Outbox,OutboxRelay,OutboxDispatcher}.cs`, `JobBoard.Shared/Persistence/{BaseRepository,OutboxMessage}.cs`

## Context

Publishing an integration event (ADR-0002) creates the classic dual-write problem: a service must both **commit its domain change** and **publish an event**, and these touch two different systems (its database and the broker). If it writes the DB then sends — and crashes in between — the event is lost. If it sends then writes — and crashes — a phantom event describes something that never committed. Neither "just send inside the transaction" (the broker isn't transactional with the DB) nor a distributed transaction (2PC — operationally heavy, poorly supported, a scalability tax) is acceptable.

Separately, the Aspire Npgsql integration enables **retry-on-failure**, whose execution strategy refuses to run inside a caller-opened transaction and may **replay** a unit of work on a transient fault — so any solution must be replay-safe.

## Decision

**We will use a hand-rolled transactional outbox: the integration event is written to the service's own `OutboxMessages` table in the same transaction as the domain change, and a background dispatcher relays it to Service Bus afterward. We will not use MassTransit or a third-party outbox.**

- **Atomic capture.** The data layer stages the domain rows and the outbox row on the *same scoped `BaseDbContext`* and commits them together via `BaseRepository.ExecuteInTransactionAsync`. A domain write cannot commit without its event, or vice versa.
- **Atomicity survives retries.** `ExecuteInTransactionAsync` hands the whole unit to the EF execution strategy so the strategy owns the transaction boundary and can replay on a transient fault. Replay-safety is guaranteed by **deterministic event IDs**: `Outbox.EnqueueAsync` keys on `event.Id` and no-ops if the row is already staged, so a replay re-stages the same row rather than duplicating it.
- **A single sender.** Only `OutboxRelay` (driven by the `OutboxDispatcher` `BackgroundService`) sends to Service Bus. It polls unprocessed rows oldest-first, sends each as a `ServiceBusMessage` with `MessageId` = row `Id` and `Subject` = event-type name, then stamps `ProcessedOnUtc`. A failed send stops the batch (preserving order) and is retried next poll. Nothing else — not business, not data code — ever calls the broker on the send side.
- **The mechanism lives once** in `JobBoard.Shared` and is reused by every service; the outbox tables are per-service.

## Consequences

**Positive**
- No lost or phantom events: the event ships **iff** the domain change commits.
- Replay-safe by construction; a transient DB fault can't produce duplicate outbox rows.
- The pattern is transport-agnostic — only `OutboxRelay` touches Service Bus, so the broker can be swapped without touching domain code.
- Delivery is **at-least-once** by design (see below), which ADR-0004 makes safe.

**Negative**
- **At-least-once, not exactly-once:** a crash between *send* and *stamp* re-sends the same `MessageId`. Accepted — the inbox (ADR-0004) dedupes.
- **Single-instance assumption today:** the naive "poll unprocessed, oldest-first" relay would let two replicas of a publisher double-send. Running >1 replica requires a claim (`FOR UPDATE SKIP LOCKED` or a leased batch) — a 90-day plan item, not yet built.
- **Polling latency:** events ship on the dispatcher's poll interval, not instantly. Fine for this domain.
- We own the mechanism (and its tests) rather than delegating to a library.

**Neutral**
- Owning the outbox is a deliberate part of the demonstration's value; the maintenance cost is accepted knowingly (see Alternatives).

## Alternatives considered

- **MassTransit (or another framework outbox).** Rejected **for this project specifically**: the hand-rolled mechanism *is* part of what JobBoard demonstrates, and hiding it behind a framework would defeat the purpose. (For a normal product, a mature library is often the right call — this rejection is scope-specific, not a claim that hand-rolling is generally superior.)
- **Two-phase commit (distributed transaction) across DB + broker.** Rejected: operationally heavy, poorly supported by the broker, and a scalability tax — the outbox is the industry-standard alternative precisely to avoid it.
- **Publish inline inside the domain transaction.** Rejected: the broker isn't enlisted in the DB transaction, so this is the dual-write bug, not a fix for it.
- **Change-data-capture (e.g. Debezium) off the DB log.** Rejected for this scope: powerful but a heavy operational dependency, and it externalizes a concern the app can own simply.
