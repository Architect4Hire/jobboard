---
name: add-audit-event
description: >
  Make a JobBoard business action land in the support audit trail — the durable, queryable
  cradle-to-grave record support uses. Use whenever adding or changing a mutating action that should be
  auditable — e.g. "audit account creation", "record profile updates in the trail", "make sure closing
  a job shows up for support". Produces (publish side) the past-tense integration-event record carrying
  the CorrelationId/CausationId/actor thread and its outbox publish, and (consume side) the Audit
  service's idempotent append to auditdb — following ADR-0011/0013/0014 and .claude/rules/audit.md.
---

# Add an audit event

The support audit trail records **facts about what happened**, collected from the bus by the
`JobBoard.Audit` service and appended to `auditdb`. Making an action auditable is **two sides of one
event**, never a cross-service write:

- **Publish side** — in the service that *owns* the action: the action publishes a past-tense
  integration event through its outbox, and that event carries the **thread** (`CorrelationId`,
  `CausationId`, actor) from ADR-0013.
- **Consume side** — in `JobBoard.Audit`: an audit consumer records the event as one **immutable** row,
  idempotent via the inbox.

Read [`.claude/rules/audit.md`](../../rules/audit.md), [ADR-0013](../../../docs/adr/0013-correlation-causation-identifiers-on-events.md),
and [ADR-0014](../../../docs/adr/0014-audit-bounded-context-bus-fed-support-trail.md) first. This skill
builds on [`add-endpoint`](../add-endpoint/SKILL.md) — the layered publish path (business builds the
event, the data layer enqueues it atomically, the dispatcher sends it) is exactly that skill's steps;
here we focus on the **audit-specific** parts: the thread, and the append.

## First: does the action already publish?

Decide before touching code — the owner is the service whose database the action changes.

- **It already emits an event** (e.g. `JobClosed`, `ApplicationStatusChanged`) → you only need to
  ensure that event **carries the full thread** and that the **Audit service subscribes** to it. Skip
  to step 3.
- **It doesn't emit yet** (e.g. account created, login, profile updated) → add a small past-tense event
  in the owning service (steps 1–2), then have Audit record it (steps 3–5).

Never make the action write `auditdb` itself, and never have Audit call back into the owning service —
that's a cross-service write and a broken boundary.

## Target layout (the two sides)

```
JobBoard.<Owner>/  + .Core/         # PUBLISH SIDE — the owning service
    business builds the event and stamps the thread; data layer enqueues it to the outbox atomically
    (the add-endpoint publish path, unchanged) — see steps 1–2

JobBoard.Contracts/                 # the shared event record (past-tense, carries the thread)
    <Action>ed.cs : IIntegrationEvent  →  Id, CorrelationId, CausationId, actor, entity ids, OnUtc

JobBoard.Audit/  + .Core/           # CONSUME SIDE — records the fact
├── Consumers/  Audit<Action>edConsumer.cs (or one generic sink)   # host entry point, idempotent
└── .Core/      AuditFacade/Business/Data → append an AuditEntry row (event as jsonb) to auditdb
```

## The thread (the audit-specific fields)

Every auditable event carries, beyond its own `Id`:

- **`CorrelationId`** — the originating request's thread, constant across the whole fan-out. Read from
  the ambient request context (minted at the gateway); never generate a fresh one mid-flow.
- **`CausationId`** — the `Id` of whatever *directly* caused this event: the request itself for a
  request-initiated action, or the **consumed event's `Id`** when the action is a reaction (a consumer
  that emits a follow-on event inherits the consumed event's `CorrelationId` and sets its
  `CausationId` to that event's `Id`).
- **actor** — the authenticated identity from the propagated edge headers (ADR-0011), **never** a
  body-supplied id. For unauthenticated cradle events (e.g. registration), record the subject the
  action creates, and mark it as self-originated — not a spoofable client value.

## Steps

1. **Event record** (only if the action doesn't emit yet) → `src/JobBoard.Contracts/`. Add an immutable
   past-tense `record` implementing `IIntegrationEvent` — `AccountCreated`, `ProfileUpdated`,
   `LoggedIn` — carrying the thread fields plus **only** the entity ids and minimal denormalized data
   the trail needs. No behavior, no EF, no Domain types, **no secrets** (never a password or token).
   Adding a field to an existing event is a contract change — run the api-contract-checker.

2. **Publish it** (owning service) → follow the [`add-endpoint`](../add-endpoint/SKILL.md) publish path:
   business **builds** the event and **stamps the thread** from the ambient context; the data layer
   **enqueues the outbox row in the same transaction** as the domain write via `IOutbox`; the
   dispatcher relays it. Nothing here talks to `auditdb` or Service Bus directly. If the event already
   exists, just confirm it stamps the thread (add it if missing) — that's the whole publish-side change.

3. **Subscribe Audit to it** → declare the subscription on the event's topic in the AppHost and the
   emulator entity config (use [`add-aspire-resource`](../add-aspire-resource/SKILL.md)), and register
   the audit consumer so the shared processor host resolves it. Keep the **AppHost subscription name ==
   the consumer registration string** (they share one namespace).

4. **Record it** (Audit side) → add `Audit<Action>edConsumer : IIntegrationEventConsumer<TEvent>` in
   `JobBoard.Audit/Consumers/` that calls the Audit facade to **append one `AuditEntry`**: event type,
   `CorrelationId`, `CausationId`, actor, entity ids, occurred-at, and the **full event serialized to
   the `jsonb` payload**. Append-only — never update or delete. The consumer is **idempotent**: in the
   same transaction as the append, check `InboxMessages` for the message `Id` and no-op on a repeat
   (the `ExecuteInTransactionAsync` seam, exactly as every other consumer). A generic sink that records
   *any* `IIntegrationEvent` is fine and preferred over one consumer per type — the row shape is uniform.

5. **Tests.**
   - **Publish side:** business builds the event and **stamps the correct thread** (correlation carried,
     causation derived right on both the request-initiated and reaction paths); the outbox row is
     written atomically (a mid-operation throw leaves neither the domain row nor the outbox row).
   - **Audit consumer:** the event maps to one `AuditEntry` with the right fields and jsonb payload, and
     an **idempotency** test that a redelivered message `Id` appends **exactly one** row.
   - Run `dotnet test` in the owning service **and** in `JobBoard.Audit` (this crosses the bus).

## Verify before trusting
Confirm the Service Bus emulator surface (`AddAzureServiceBus(...).RunAsEmulator(...)`, subscription
declaration, entity-config JSON, `AddAzureServiceBusClient`) and Postgres `jsonb` mapping in EF Core
against https://aspire.dev and the current docs before wiring — the transport and the provider mapping
drift; the outbox/inbox and the thread contract are ours and stable.

## Checklist before done
- [ ] The action's fact goes out as a `Contracts` **event**, not a write into `auditdb` by the owner
- [ ] The event is a small past-tense record with the **thread** (`CorrelationId`, `CausationId`,
      actor) and **no secrets/PII** beyond what support needs
- [ ] Business **stamps the thread** from the ambient context; `CausationId` is derived correctly on
      both the request-initiated and reaction paths; actor is the propagated identity, never body-supplied
- [ ] The event is enqueued to the owner's outbox **in the same transaction** as the domain write; only
      the dispatcher sends it
- [ ] Audit **subscribes** to the topic (AppHost subscription == consumer registration string) and
      **appends one immutable row** with the event as `jsonb`
- [ ] The audit consumer writes **only** `auditdb`, is **idempotent** via the inbox, and never calls
      back into a service or changes domain state
- [ ] Tests pass on both sides, including the outbox atomicity test and the audit idempotency test
      (`dotnet test` in the owner **and** in `JobBoard.Audit`)
- [ ] Ran the api-contract-checker (event changed) and the audit-coverage-checker (no mutating action
      left unaudited)
