# ADR-0004: Idempotent inbox over at-least-once delivery

- **Status:** Accepted
- **Date:** 2026-07-18
- **Deciders:** Robert Felkins
- **Related:** ADR-0003 (outbox), ADR-0002 (events), `docs/high-level-design.md` §6, `.claude/rules/messaging.md`
- **Implements:** `JobBoard.Shared/Messaging/{Inbox,IntegrationEventProcessor,ServiceBusProcessorHost}.cs`, `JobBoard.Shared/Persistence/InboxMessage.cs`

## Context

The transactional outbox (ADR-0003) delivers **at-least-once** by design: a crash between sending a message and stamping it processed re-sends the same `MessageId`. Service Bus itself can also redeliver (e.g. a lock expiry, a processing crash before completion). Therefore **every consumer will, eventually, see some message twice**. If a handler isn't safe to run twice, that's a correctness bug — closing the same applications twice, sending duplicate emails, corrupting a counter. Chasing exactly-once delivery at the transport is a known trap; the tractable, correct approach is **idempotent consumption**.

## Decision

**We will make every consumer idempotent via a per-service inbox: the consumer records the handled `MessageId` in an `InboxMessages` table in the same transaction as its side effect, and no-ops on a repeat.**

- **Dedupe key.** The inbox keys on the message's `Id` (the deterministic event ID, carried as the Service Bus `MessageId`).
- **Atomic with the effect.** The inbox insert and the side effect commit in one transaction. Either both happen (first delivery) or the consumer sees the row already exists and no-ops (repeat).
- **Manual settlement.** The `ServiceBusProcessorHost` runs with `AutoCompleteMessages = false`; a message is completed **only after** the consumer succeeds. A throw leaves it unsettled, so Service Bus redelivers it — which the inbox makes safe.
- **Own-store only.** A consumer writes only its own service's database (ADR-0001); the inbox lives alongside the side-effect rows so the atomicity is real.

## Consequences

**Positive**
- Redelivery is a no-op; the system tolerates the at-least-once semantics the outbox and broker guarantee, without duplicate effects.
- No dependency on fragile transport-level "exactly-once" claims.
- Failure handling is honest: a failing consumer leaves the message unsettled and it retries, rather than being silently dropped.

**Negative**
- **The `InboxMessages` table grows unbounded** without a retention/pruning policy — a housekeeping job is a future task.
- Idempotency is a **discipline the consumer must uphold**: the dedupe check and the side effect must share the transaction. A handler that checks then acts outside one transaction reintroduces the double-effect window. (This is enforced by convention in `.claude/rules/messaging.md` and reviewed.)
- **Poison messages** (a message that always throws) will redeliver indefinitely under the current default; an explicit dead-letter policy is a 90-day plan item.

**Neutral**
- Deduplication is at the *application* level (inbox), independent of any broker-side duplicate-detection window — so it holds even across long redelivery gaps.

## Alternatives considered

- **Rely on Service Bus duplicate detection.** Rejected as the sole mechanism: it only covers a bounded time window and the broker's own scope, not an outbox re-send after a long outage; the application-level inbox is authoritative.
- **Design every handler to be naturally idempotent without bookkeeping.** Attractive where achievable (e.g. an upsert), but not general — many effects (send an email, append a log, emit a follow-on event) aren't naturally idempotent, so a uniform inbox is the reliable default.
- **Chase exactly-once delivery end-to-end.** Rejected: not achievable in practice across a DB + broker boundary; "effectively once via idempotent consumers" is the correct, standard target.
