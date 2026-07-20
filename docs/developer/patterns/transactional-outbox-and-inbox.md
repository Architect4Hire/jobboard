# Transactional Outbox & Inbox

*How an event ships exactly when the write that caused it commits, and how a consumer survives
receiving it more than once. The hand-rolled reliability mechanism behind every cross-service fact.*

## The problem this solves

Two systems (a database and a message broker) can't commit in the same transaction. If a service wrote
its domain row and *then* called Service Bus, a crash between the two leaves a committed change with no
event — the rest of the system never finds out. If it published first, a crash after the send but
before the commit publishes a fact that never happened. The outbox turns the second write into a *local*
one (same database, same transaction as the domain change), so it's atomic with the write it describes,
and defers the actual network send to a background relay that can safely fail and retry. The mirror
problem on the receiving side — Service Bus only promises **at-least-once** delivery — means a consumer
has to be safe to run twice; the inbox is the ledger that makes that true.

## How it works here

### Publish side: write the row, not the message

`IOutbox` ([`IOutbox.cs`](../../../src/JobBoard.Shared/Messaging/IOutbox.cs)) is a thin serializer over
the *current request's own* `DbContext`:

```csharp
public async Task EnqueueAsync(IIntegrationEvent @event, CancellationToken cancellationToken = default)
{
    var existing = await _context.OutboxMessages.FindAsync([@event.Id], cancellationToken);
    if (existing is not null) return;   // deterministic id → replay-safe

    var message = new OutboxMessage
    {
        Id = @event.Id, Type = @event.GetType().Name, Destination = @event.GetType().Name,
        Payload = JsonSerializer.Serialize(@event, @event.GetType(), SerializerOptions),
        OccurredOnUtc = DateTime.UtcNow, ProcessedOnUtc = null,
    };
    await _context.OutboxMessages.AddAsync(message, cancellationToken);
}
```
([`Outbox.cs`](../../../src/JobBoard.Shared/Messaging/Outbox.cs)) — no `SaveChanges`, no network call.
It stages a row on the same change tracker the domain write is using. `IOutbox` and the domain write are
both driven through
[`BaseRepository<TContext>.ExecuteInTransactionAsync`](../../../src/JobBoard.Shared/Persistence/BaseRepository.cs),
which is what actually commits them together:

```csharp
public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
{
    var strategy = Context.Database.CreateExecutionStrategy();
    return await strategy.ExecuteAsync(async token =>
    {
        await using var transaction = await Context.Database.BeginTransactionAsync(token);
        var result = await operation(token);
        await Context.SaveChangesAsync(token);   // flushes the domain rows AND the outbox row together
        await transaction.CommitAsync(token);
        return result;
    }, cancellationToken);
}
```

A data layer hands the *whole* write — including the `IOutbox.EnqueueAsync` call — into that callback,
e.g. [`JobDataLayer.AddAsync`](../../../src/JobBoard.Jobs.Core/Data/JobDataLayer.cs):

```csharp
return await _repository.ExecuteInTransactionAsync(async token =>
{
    var saved = await _repository.AddAsync(job, token);
    await _outbox.EnqueueAsync(@event, token);
    return saved;
}, cancellationToken);
```

A throw on either leg rolls both back. Why a callback instead of a plain `BeginTransactionAsync`?
Aspire's Npgsql integration turns on retry-on-failure, and its execution strategy **refuses to run
inside a transaction it did not open**. Handing in the whole operation lets the strategy own the
boundary and replay it on a transient fault — which means the callback **may run more than once**, and
the outbox's deterministic-id check above is what makes that safe.

### The relay: the only thing that talks to Service Bus

Nothing above ever touched a `ServiceBusClient`. [`OutboxRelay`](../../../src/JobBoard.Shared/Messaging/OutboxRelay.cs)
is the one place that does — it reads unprocessed rows oldest-first and sends each, stamping
`ProcessedOnUtc` as it goes:

```csharp
var pending = await context.OutboxMessages
    .Where(m => m.ProcessedOnUtc == null).OrderBy(m => m.OccurredOnUtc).Take(_options.BatchSize)
    .ToListAsync(cancellationToken);

foreach (var row in pending)
{
    var sender = _senders.GetOrAdd(row.Destination, _client.CreateSender);
    var message = new ServiceBusMessage(row.Payload) { MessageId = row.Id.ToString(), Subject = row.Type };
    await sender.SendMessageAsync(message, cancellationToken);
    row.ProcessedOnUtc = DateTime.UtcNow;
    // (a failed send breaks the loop so ordering holds and the row retries next poll)
}
await context.SaveChangesAsync(cancellationToken);
```

`MessageId` is the outbox row's own `Id` — the event's `Id` — so a crash between a successful send and
the `SaveChangesAsync` stamp simply resends the same `MessageId` on the next poll; that's the
at-least-once contract, made explicit. [`OutboxDispatcher`](../../../src/JobBoard.Shared/Messaging/OutboxDispatcher.cs)
is the `BackgroundService` that drives `OutboxRelay` on a `PeriodicTimer`, opening a fresh DI scope
(and thus a fresh `DbContext`) each tick, and swallowing a poll failure so one bad tick doesn't tear
down the loop. Both classes are registered once, per host, by `AddSharedMessaging<TDbContext>()` — a
service never writes dispatcher code, only a destination topic.

### Receive side: one processor per subscription, dispatched generically

[`ServiceBusProcessorHost`](../../../src/JobBoard.Shared/Messaging/ServiceBusProcessorHost.cs) opens one
`ServiceBusProcessor` per `(topic, subscription)` registered in the `ConsumerRegistry`, with
`AutoCompleteMessages = false` — a message is only completed after the consumer succeeds; a throw leaves
it unsettled so Service Bus redelivers it. Each delivered message goes to
[`IntegrationEventProcessor`](../../../src/JobBoard.Shared/Messaging/IntegrationEventProcessor.cs), which
resolves the event type from the message's `Subject`, deserializes the body, and invokes the matching
`IIntegrationEventConsumer<TEvent>` from a fresh DI scope. This is purely mechanical dispatch — it never
touches idempotency itself; that's the consumer's job.

### Consumer side: the inbox makes a redelivery a no-op

A consumer (e.g. [`JobClosedConsumer`](../../../src/JobBoard.Applications/Consumers/JobClosedConsumer.cs))
is a one-liner that forwards to its own facade — no domain logic of its own. The idempotency lives one
layer down, in the data layer, alongside the side effect it guards, e.g.
[`ApplicationDataLayer.CloseOpenApplicationsForJobAsync`](../../../src/JobBoard.Applications.Core/Data/ApplicationDataLayer.cs):

```csharp
_repository.ExecuteInTransactionAsync(async token =>
{
    if (await _inbox.HasProcessedAsync(messageId, token))
        return 0;                                   // already handled — no-op

    var active = await _repository.GetActiveByJobAsync(jobId, token);
    await _repository.CloseActiveByJobAsync(jobId, target, token);
    var closedIds = await _repository.GetIdsInStatusAsync(active.Select(a => a.Id).ToList(), target, token);

    foreach (var application in active)
        if (closedIds.Contains(application.Id))
            await _outbox.EnqueueAsync(buildEvent(application), token);   // this consumer ALSO publishes

    await _inbox.MarkProcessedAsync(messageId, token);
    return closedIds.Count;
}, cancellationToken);
```

The inbox check, the side effect, the *new* outbox enqueue this consumer produces (`ApplicationStatusChanged`
per closed application), and the inbox stamp are all inside one `ExecuteInTransactionAsync` — either all
of it lands or none of it does. This is the "reacting to another service" shape in full: one event in,
zero-or-more events out, one transaction. [`Inbox.cs`](../../../src/JobBoard.Shared/Messaging/Inbox.cs)
implements the check/stamp pair the same way `Outbox.cs` does — a `FindAsync` guard on a deterministic
key (here, the message id) so a strategy retry can't double-insert the dedupe row either.

The `Audit` service's consumer follows the identical shape with a generic sink — see
[Correlation, Causation & the Audit Trail](./correlation-causation-and-audit-trail.md).

## Why

[ADR-0002](../../adr/0002-event-driven-integration-over-service-bus.md) is the decision to integrate
over events at all; [ADR-0003](../../adr/0003-hand-rolled-transactional-outbox.md) is why the outbox is
hand-rolled instead of a third-party library; [ADR-0004](../../adr/0004-idempotent-inbox-at-least-once-delivery.md)
is why the inbox exists rather than trusting exactly-once delivery.

## Pitfalls / rules to respect

- **Nothing but the dispatcher sends to Service Bus.** A write that commits without its outbox row, or
  an event sent inline from business/data code, is the exact bug this pattern exists to prevent.
- **Publish through the outbox, atomically** — the domain write and the `IOutbox.EnqueueAsync` call are
  one `ExecuteInTransactionAsync` operation, never two separate saves.
- **Every consumer dedupes via the inbox**, in the same transaction as its side effect. A handler that
  isn't safe to run twice is a bug, not an edge case.
- **A consumer writes only its own service's database.** Reacting to another service's event never
  means reaching back into the publisher or adding a synchronous call in its place.
- **The callback may run more than once** (execution-strategy retries) — anything staged inside it must
  be safe to repeat, which is exactly what the deterministic-id checks in `Outbox`/`Inbox` buy you.

See `.claude/rules/messaging.md` for the full standing-rule list.

## Reference map

| Piece | Real file |
| --- | --- |
| Outbox contract + impl | [`IOutbox.cs`](../../../src/JobBoard.Shared/Messaging/IOutbox.cs) · [`Outbox.cs`](../../../src/JobBoard.Shared/Messaging/Outbox.cs) |
| Outbox table | [`OutboxMessage.cs`](../../../src/JobBoard.Shared/Persistence/OutboxMessage.cs) |
| Dispatcher loop + relay | [`OutboxDispatcher.cs`](../../../src/JobBoard.Shared/Messaging/OutboxDispatcher.cs) · [`OutboxRelay.cs`](../../../src/JobBoard.Shared/Messaging/OutboxRelay.cs) |
| Transaction boundary | [`BaseRepository.cs`](../../../src/JobBoard.Shared/Persistence/BaseRepository.cs) |
| Inbox contract + impl | [`IInbox.cs`](../../../src/JobBoard.Shared/Messaging/IInbox.cs) · [`Inbox.cs`](../../../src/JobBoard.Shared/Messaging/Inbox.cs) |
| Inbox table | [`InboxMessage.cs`](../../../src/JobBoard.Shared/Persistence/InboxMessage.cs) |
| Receive-side host + dispatch | [`ServiceBusProcessorHost.cs`](../../../src/JobBoard.Shared/Messaging/ServiceBusProcessorHost.cs) · [`IntegrationEventProcessor.cs`](../../../src/JobBoard.Shared/Messaging/IntegrationEventProcessor.cs) |
| A two-way consumer (consumes + republishes) | [`ApplicationDataLayer.cs`](../../../src/JobBoard.Applications.Core/Data/ApplicationDataLayer.cs) · [`JobClosedConsumer.cs`](../../../src/JobBoard.Applications/Consumers/JobClosedConsumer.cs) |
| Standing rules | [`.claude/rules/messaging.md`](../../../.claude/rules/messaging.md) |
