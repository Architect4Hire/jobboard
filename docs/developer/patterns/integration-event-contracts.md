# Integration Event Contracts

*`JobBoard.Contracts` is a strict leaf: event records only, nothing that could couple two services'
domains together. Status crosses as a string, on purpose.*

## The problem this solves

Event-driven integration ([Transactional Outbox & Inbox](./transactional-outbox-and-inbox.md)) needs
*some* code both publisher and consumer share — the event shape. Shared code between services is
exactly where coupling creeps back in: the moment the shared library holds a domain enum, a DTO, or a
helper, a change to one service's internals silently becomes a breaking change for every consumer, and
the database-per-service boundary ([Database-per-Service & Data Ownership](./database-per-service-and-data-ownership.md))
erodes from the inside. `Contracts` is deliberately starved down to the smallest possible shared
surface: the fact, and nothing that could carry a domain type along with it.

## How it works here

### A leaf, and only a leaf

[`IIntegrationEvent`](../../../src/JobBoard.Contracts/IIntegrationEvent.cs) is the only interface in the
project. Every event is a `sealed record` implementing it, named in the **past tense** — `JobPosted`,
`JobClosed`, `ApplicationSubmitted`, `ApplicationStatusChanged`, `AccountCreated`, `LoggedIn`,
`LoginFailed`, `ProfileUpdated`. `Contracts` references nothing (not even `Shared`), and every service
plus `Shared` references it — that's what makes it a leaf, and it's what keeps the whole reference graph
acyclic (`Contracts` ← `Shared` ← `.Core` ← host).

### Minimal fields — IDs plus just enough to avoid a call-back

[`JobClosed`](../../../src/JobBoard.Contracts/JobClosed.cs):

```csharp
public sealed record JobClosed(Guid Id, Guid JobId, Guid EmployerId, DateTime ClosedOnUtc) : IIntegrationEvent
{
    public Guid CorrelationId { get; init; }
    public Guid CausationId { get; init; }
    public Guid? ActorId { get; init; }
}
```

`EmployerId` is denormalized reference data, not a foreign key — a consumer that needs to know who owned
the closed posting doesn't have to call back into Jobs to find out. This is the event-carried-state-
transfer half of [Database-per-Service & Data Ownership](./database-per-service-and-data-ownership.md).
[`AccountCreated`](../../../src/JobBoard.Contracts/AccountCreated.cs) shows the same discipline the
other direction — it carries `Email` and `Role` (what the audit trail needs) but never the password or
its hash.

Every event also carries the four fields *no* concrete record chooses for itself: `Id` (the outbox
row key / Service Bus `MessageId` / inbox dedupe key) and the audit thread —
`CorrelationId`/`CausationId`/`ActorId` — covered in
[Correlation, Causation & the Audit Trail](./correlation-causation-and-audit-trail.md). A business layer
stamps `Id` fresh in its mapper (`ToJobClosed`), and the thread via `AuditThreadExtensions`; `Contracts`
just declares the shape.

### Status crosses as a string, never a shared enum

[`ApplicationStatusChanged`](../../../src/JobBoard.Contracts/ApplicationStatusChanged.cs) carries
`FromStatus`/`ToStatus` as plain `string`, not Applications' `ApplicationStatus` enum:

```csharp
public sealed record ApplicationStatusChanged(
    Guid Id, Guid ApplicationId, Guid CandidateId, Guid JobId,
    string FromStatus, string ToStatus, DateTime ChangedOnUtc) : IIntegrationEvent
```

Each consuming service maps the string to/from its own domain type internally; no service's enum ever
lives in `Contracts`. That trades compile-time safety for decoupling — a typo or a renamed value becomes
a *runtime* mismatch, not a build error — which is a real, named risk (see ADR-0010's consequences), not
an oversight. Treat any change to a status string's *value* (not just its Contracts shape) as a contract
change across every consumer.

### Changing an event is a contract change

Because every consumer deserializes by the event's runtime type name (see
[`IntegrationEventProcessor`](../../../src/JobBoard.Shared/Messaging/IntegrationEventProcessor.cs) in
[Transactional Outbox & Inbox](./transactional-outbox-and-inbox.md)), adding a required field or
renaming one breaks every consumer's deserialization silently until it's tested. Prefer additive changes
(a new optional field) over mutating an existing one; a genuinely breaking change means versioning the
event type, not editing the record in place.

## Why

[ADR-0010](../../adr/0010-contracts-leaf-status-as-string.md) is the decision and names the specific
risk (silent enum/string drift) plus the mitigation on the roadmap (a contract test). ADR-0002 is why
events exist at all.

## Pitfalls / rules to respect

- **No entities, ServiceModels, EF, or DTOs that aren't events land in `Contracts`.** If two services
  seem to need the same *domain* type, they don't — that's the boundary telling you something.
- **Past tense, always.** An event is a fact that already happened (`JobClosed`), never a command
  (`CloseJob`).
- **Carry the minimum a consumer needs**, not "whatever's convenient." Denormalize a field to avoid a
  call-back; don't smuggle a whole entity graph across.
- **No service's enum crosses the boundary.** Status, and anything similarly enumerable, is a string on
  the wire and mapped locally on each side.
- **A published event is forever, until versioned.** Treat any shape or value change as a breaking
  change for every current consumer, not a private refactor.

See `.claude/rules/messaging.md` for the full standing-rule list.

## Reference map

| Concern | Real file |
| --- | --- |
| The marker interface | [`IIntegrationEvent.cs`](../../../src/JobBoard.Contracts/IIntegrationEvent.cs) |
| Denormalized-field example | [`JobClosed.cs`](../../../src/JobBoard.Contracts/JobClosed.cs) |
| Status-as-string example | [`ApplicationStatusChanged.cs`](../../../src/JobBoard.Contracts/ApplicationStatusChanged.cs) |
| PII-minimal example | [`AccountCreated.cs`](../../../src/JobBoard.Contracts/AccountCreated.cs) |
| Every event today | [`src/JobBoard.Contracts/`](../../../src/JobBoard.Contracts/) |
