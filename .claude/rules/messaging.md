---
paths:
  - src/JobBoard.Shared/**
  - src/JobBoard.Contracts/**
  - src/JobBoard.*/Consumers/**
---
# Messaging rules — Azure Service Bus + hand-rolled outbox/inbox

Services talk to each other only through **integration events** over **Azure Service Bus** (a local
emulator in dev). Reliability is a **hand-rolled transactional outbox** — no MassTransit, no
third-party outbox. The mechanism lives once in `JobBoard.Shared`; the event records live in
`JobBoard.Contracts`; consumers live in each service host.

- **Events are facts, in `Contracts`.** An integration event is an immutable `record` implementing
  `IIntegrationEvent` (a `Guid Id`), named in the **past tense** (`JobClosed`, `ApplicationSubmitted`),
  carrying only the fields a consumer needs — IDs plus the minimum denormalized data to avoid a
  call-back. No behavior, no EF, no service's Domain types. Changing an existing event is a **contract
  change** that affects every consumer; treat it as such. Every event also carries a `CorrelationId`,
  `CausationId`, and the acting identity for the audit trail — see `.claude/rules/audit.md` (ADR-0013).
- **Publish through the outbox, atomically.** Business *builds* the event; the data layer writes it
  to the service's own `OutboxMessages` table via `IOutbox` **inside the same transaction** as the
  domain write (`ExecuteInTransactionAsync`). A write that commits without its outbox row — or an
  event sent outside the outbox — is the bug this rule exists to prevent. Nothing but the dispatcher
  sends to Service Bus.
- **The dispatcher is the only sender.** The `OutboxDispatcher` (in `Shared`, a `BackgroundService`)
  polls unprocessed rows oldest-first, sends each as a `ServiceBusMessage` with `MessageId` = the row
  `Id` and `Subject` = the event-type name, then stamps `ProcessedOnUtc`. Delivery is **at-least-once**
  (a crash between send and stamp resends the same `MessageId`).
- **Consumers are idempotent, via the inbox.** A `<Event>Consumer` implements
  `IIntegrationEventConsumer<TEvent>` (from `Shared`); the Service Bus processor host resolves and
  calls it. In the same transaction as its side effect it checks `InboxMessages` for the message ID,
  applies the change and records the ID, or no-ops on a repeat. A handler that isn't safe to run twice
  is a bug, not an edge case.
- **A consumer writes only its own service's database.** Reacting to another service's event means
  doing work in *your* store — never reach back into the publisher, and never add a synchronous call
  in place of the event.
- **No addresses in code.** The `ServiceBusClient` comes from the Aspire integration
  (`AddAzureServiceBusClient`), keyed to the AppHost `servicebus` resource — never a hardcoded
  namespace or connection string. New topics/subscriptions are declared as Aspire resources (and in
  the emulator's entity config), not invented at runtime.

Verify the Service Bus emulator surface (`AddAzureServiceBus(...).RunAsEmulator(...)`, the entity-config
format, `AddAzureServiceBusClient`) against https://aspire.dev and the emulator docs — the transport
binding drifts; the outbox/inbox pattern itself is ours and stable.
