# Correlation, Causation & the Audit Trail

*Every integration event carries who caused it and why, so support can reconstruct any request
cradle-to-grave from one durable, append-only store — without OpenTelemetry, and without the audited
service being aware it's being audited.*

## The problem this solves

Distributed tracing (spans, OpenTelemetry) answers "was this request slow and where." It doesn't answer
"why does this candidate have two application-status emails" three weeks later, after the trace has
expired. That's a support/product question, not an ops one, and it needs a durable, queryable, causally
linked record — not a timeline that ages out. The audit trail is that record: a **read model fed by the
bus**, threaded by three identifiers every event already carries.

## How it works here

### The thread: three ids on every event

[`IIntegrationEvent`](../../../src/JobBoard.Contracts/IIntegrationEvent.cs) declares, alongside `Id`:

```csharp
Guid CorrelationId { get; }   // constant across a request's whole fan-out
Guid CausationId { get; }     // the Id of whatever DIRECTLY caused this event
Guid? ActorId { get; }        // who did it — the edge-projected identity, never body-supplied
```

`CorrelationId` never changes as one request triggers a cascade of events across services;
`CausationId` links each event to its *immediate* parent, giving the trail a causal **tree**, not just a
flat timeline sorted by time. Every concrete event record (e.g.
[`JobClosed.cs`](../../../src/JobBoard.Contracts/JobClosed.cs)) implements all three as `init`-only
properties.

### Deriving the thread: `AuditThread` and its three shapes

The rule for *where the thread comes from* lives in exactly one place —
[`AuditThreadExtensions`](../../../src/JobBoard.Shared/Requests/AuditThreadExtensions.cs) — as three
extension methods a business layer calls when it builds an event:

**Root thread** — a request-initiated event. Correlation and actor come from the edge-populated
`IRequestContext`; causation is the request's own correlation id, since the request itself is the root
cause:

```csharp
public static AuditThread RootThread(this IRequestContext context) =>
    new(context.CorrelationId, context.CorrelationId, context.ActorId);
```

Used by, e.g., [`JobBusiness.PostAsync`](../../../src/JobBoard.Jobs.Core/Business/JobBusiness.cs):
`job.ToJobPosted(_requestContext.RootThread())`.

**Follow-on thread** — an event built *while consuming* another. Correlation and actor are inherited
from the consumed event; causation becomes *that event's own* `Id`, extending the causal tree one level:

```csharp
public static AuditThread FollowOnThread(this IIntegrationEvent cause) =>
    new(cause.CorrelationId, cause.Id, cause.ActorId);
```

This is what a two-service reaction (see [Transactional Outbox & Inbox](./transactional-outbox-and-inbox.md))
uses when it republishes — `ApplicationDataLayer.CloseOpenApplicationsForJobAsync`'s `buildEvent`
callback stamps the `ApplicationStatusChanged` it emits with the consumed `JobClosed`'s `FollowOnThread()`,
so the trail can show "this status change happened *because of* that job closing," not just "at around
the same time."

### Self-originated events

A few actions happen **before an identity exists to project** — registering an account, or logging in
for the first time (the token that would carry an actor hasn't been issued yet). For those,
[`AccountBusiness`](../../../src/JobBoard.Identity.Core/Business/AccountBusiness.cs) uses the third
shape:

```csharp
public static AuditThread SelfOriginatedThread(this IRequestContext context, Guid actorId) =>
    new(context.CorrelationId, context.CorrelationId, actorId);
```

```csharp
var created = account.ToAccountCreated(_requestContext.SelfOriginatedThread(account.Id));
```

Correlation/causation still come from the request; the actor is the **action's own subject** — the
account that was just created or just authenticated — a server-derived value, never a spoofable
client-supplied one. `LoginFailed` is the odd one out: there's no account to be the actor (an unknown
email has no account, and the system never discloses which credential was wrong), so it uses
`RootThread()` and carries no subject at all — see `AuditEntryMapper.Describe` below.

### From event to durable row: the Audit service

`JobBoard.Audit` is the **sole writer** of `auditdb`, and it only appends. One generic consumer handles
every event type — [`AuditConsumer<TEvent>`](../../../src/JobBoard.Audit/Consumers/AuditConsumer.cs):

```csharp
public sealed class AuditConsumer<TEvent> : IIntegrationEventConsumer<TEvent> where TEvent : IIntegrationEvent
{
    public Task ConsumeAsync(TEvent @event, CancellationToken cancellationToken = default) =>
        _facade.RecordAsync(@event, cancellationToken);
}
```

registered once per event type in the AppHost's `audit-*` subscriptions (see
[Aspire Orchestration](./aspire-orchestration.md)). The mapping from *any* event to one uniform row is
[`AuditEntryMapper.ToAuditEntry`](../../../src/JobBoard.Audit.Core/Managers/Mappers/AuditEntryMapper.cs):
the thread and the event's own `Id`/type name come straight off the `IIntegrationEvent` interface; the
full event is serialized into the row's `jsonb` payload. The one thing that *isn't* on the interface —
which entity this event is principally about, and when it actually happened — is resolved per concrete
event type in `Describe`:

```csharp
private static (Guid? SubjectId, DateTime OccurredOnUtc) Describe(IIntegrationEvent @event) => @event switch
{
    JobPosted e => (e.JobId, e.PostedOnUtc),
    ApplicationStatusChanged e => (e.ApplicationId, e.ChangedOnUtc),
    LoginFailed e => ((Guid?)null, e.OccurredOnUtc),   // no subject — never disclose which credential failed
    _ => throw new NotSupportedException(/* ... */),   // fail loud: a new audited event needs a case here
};
```

An unmapped event throws rather than recording a bogus row — the `add-audit-event` skill exists so a
new event always gets its `Describe` case.

The append itself is inbox-guarded exactly like any other consumer (see
[Transactional Outbox & Inbox](./transactional-outbox-and-inbox.md)) —
[`AuditDataLayer.AppendAsync`](../../../src/JobBoard.Audit.Core/Data/AuditDataLayer.cs) checks
`HasProcessedAsync` and marks processed inside the same transaction as the `AddAsync`, so a redelivered
message is a no-op and each event is recorded exactly once.

### Reading it back

[`AuditController`](../../../src/JobBoard.Audit/Controllers/AuditController.cs) exposes exactly one
route, `GET audit/entries`, filterable by correlation id, entity (subject) id, actor, and time window —
the four axes the [`trace-a-request`](../../../.claude/skills/trace-a-request/SKILL.md) skill queries.
It's read-only by construction: there is no write action anywhere in the Audit service's public surface,
and the gateway proxies it behind the `authenticated` policy — `auditdb` is never reachable directly.

## Why

[ADR-0013](../../adr/0013-correlation-causation-identifiers-on-events.md) is the identifier contract;
[ADR-0014](../../adr/0014-audit-bounded-context-bus-fed-support-trail.md) is the decision to build
Audit as a bus-fed bounded context rather than, say, mining OpenTelemetry spans;
[ADR-0015](../../adr/0015-gateway-identity-projection-header-mechanism.md) is why the actor is
trustworthy (see [Authentication & Identity Propagation](./authentication-and-identity-propagation.md)).
[ADR-0012](../../adr/0012-cross-service-read-model-strategy.md) frames the audit trail as one concrete
instance of the still-open cross-service read-model question (see
[Database-per-Service & Data Ownership](./database-per-service-and-data-ownership.md)).

## Pitfalls / rules to respect

- **Every mutating action needs an audited event.** A state-changing action with no published event is a
  coverage gap — use the `add-audit-event` skill when adding one.
- **Stamp the thread at the publish site, using the right shape.** `RootThread()` for a request-initiated
  event, `FollowOnThread()` when building an event while consuming another, `SelfOriginatedThread()` only
  for the handful of cradle events that predate any identity.
- **Audit never calls back into a service and never drives behavior.** It's a read model; the owning
  service stays authoritative for its own data.
- **No secrets or needless PII in the trail.** The `jsonb` payload is the same event a consumer already
  sees — nothing more sensitive should be added just because it's "for audit."
- **A new audited event needs a `Describe` case**, or `AuditEntryMapper` throws by design rather than
  recording a row with no subject.

See `.claude/rules/audit.md` for the full standing-rule list.

## Reference map

| Concern | Real file |
| --- | --- |
| The identifier contract | [`IIntegrationEvent.cs`](../../../src/JobBoard.Contracts/IIntegrationEvent.cs) |
| Deriving the thread | [`AuditThread.cs`](../../../src/JobBoard.Shared/Requests/AuditThread.cs) · [`AuditThreadExtensions.cs`](../../../src/JobBoard.Shared/Requests/AuditThreadExtensions.cs) |
| Stamping at the publish site | [`JobBusiness.cs`](../../../src/JobBoard.Jobs.Core/Business/JobBusiness.cs) · [`AccountBusiness.cs`](../../../src/JobBoard.Identity.Core/Business/AccountBusiness.cs) |
| The audit sink | [`AuditConsumer.cs`](../../../src/JobBoard.Audit/Consumers/AuditConsumer.cs) · [`AuditEntryMapper.cs`](../../../src/JobBoard.Audit.Core/Managers/Mappers/AuditEntryMapper.cs) · [`AuditDataLayer.cs`](../../../src/JobBoard.Audit.Core/Data/AuditDataLayer.cs) |
| The query surface | [`AuditController.cs`](../../../src/JobBoard.Audit/Controllers/AuditController.cs) |
| Standing rules | [`.claude/rules/audit.md`](../../../.claude/rules/audit.md) |
