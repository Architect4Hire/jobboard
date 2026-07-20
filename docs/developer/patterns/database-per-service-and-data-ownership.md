# Database-per-Service & Data Ownership

*Every service reads and writes only its own database. Two services never JOIN — they duplicate the
little reference data they need, or react to an event. Cross-service reads are still an open decision.*

## The problem this solves

A shared database is the fastest way to make five "independent" services into one tightly coupled
system: any service can change any table's shape and break another service's queries, and you can never
deploy or reason about one service without the others. Owning your own database is what makes the
service boundary real instead of aspirational — the boundary a shared connection string would erase.

## How it works here

[`AppHost.cs`](../../../src/JobBoard.AppHost/AppHost.cs) declares one Postgres server and **six**
separate database resources off it, one per service:

```csharp
var postgres = builder.AddPostgres("postgres");
var jobsDb = postgres.AddDatabase("jobsdb");
var applicationsDb = postgres.AddDatabase("applicationsdb");
var identityDb = postgres.AddDatabase("identitydb");
var profilesDb = postgres.AddDatabase("profilesdb");
var notificationsDb = postgres.AddDatabase("notificationsdb");
var auditDb = postgres.AddDatabase("auditdb");
```

Each service project is `WithReference`d to **only its own** database (e.g. `jobs.WithReference(jobsDb)`)
— never to a second one. There is no mechanism in this codebase for a service to open a connection to
another service's database; the only way data crosses a service boundary at all is an integration event
over the bus (see [Transactional Outbox & Inbox](./transactional-outbox-and-inbox.md)).

### When two services need to agree on the same thing

Two shapes recur, and neither is a foreign key:

**1. Duplicate a well-known id as a literal (seed/reference data).** The demo employer account exists
in `identitydb`; its company profile exists in `profilesdb`. They agree because both seeders hardcode
the *same* `Guid` literal — never a cross-service foreign key:

```csharp
// Every seeder that needs this account uses the identical literal.
public static readonly Guid EmployerId = new("e0000000-0000-0000-0000-000000000001");
```

See [Adding Seed Data](../adding-seed-data.md) for the full convention (per-row idempotency guards,
where each seeder lives).

**2. Denormalize what a consumer needs onto the event itself.** [`JobClosed`](../../../src/JobBoard.Contracts/JobClosed.cs)
carries `EmployerId` alongside `JobId` — Applications never has to call back into Jobs to find out who
owns the posting it's reacting to; the fact already carries the minimum data a consumer needs. This is
**event-carried state transfer**, formalized in
[Integration Event Contracts](./integration-event-contracts.md).

### Cross-service *reads* — the honest state today

A screen like "my applications, with the job title and employer name" needs data that lives in three
services (Applications, Jobs, Profiles). Today that's stitched **client-side** in the Angular app — the
events already carry enough denormalized ids/fields to make that workable at this size. That's a
tactic, not a ratified strategy: [ADR-0012](../../adr/0012-cross-service-read-model-strategy.md) is
still **Proposed**. It lays out three real options —

- **(A) API composition** — a composition endpoint at the gateway fans out and assembles the response.
- **(B) Materialized read models** — a read-optimized store per query need, kept current via the same
  integration events. This is the same shape as the [audit trail](./correlation-causation-and-audit-trail.md),
  which is effectively read-model (B) applied to one particular query (a request's full history).
- **(C) Event-carried state transfer, formalized** — what's happening today, just named on purpose.

— and explicitly rejects pre-building a full CQRS/event-sourcing rig before a specific screen forces the
question. If you're about to build a new cross-service screen, that ADR is where the decision belongs,
not a one-off client-side stitch that becomes precedent by accident.

## Why

[ADR-0001](../../adr/0001-microservices-database-per-service.md) is the decision itself.
[ADR-0012](../../adr/0012-cross-service-read-model-strategy.md) is the still-open follow-on for reads
that need more than one service's data.

## Pitfalls / rules to respect

- **No shared database, ever.** Needing another service's data is a signal to consume its event and keep
  a local copy, or route the query through the gateway — never a second connection string. If you're
  tempted to add one, stop and say so (per `CLAUDE.md`).
- **`Contracts` carries facts, not foreign keys.** An event may denormalize a *field* a consumer needs,
  but a Domain entity from one service must never cross into another's code.
- **Seed data follows the same rule.** Each seeder writes only its own database; cross-service alignment
  is a duplicated literal id, never a shared table (see [Adding Seed Data](../adding-seed-data.md)).
- **A new cross-service read is a design conversation**, not a client-side workaround that quietly
  becomes the pattern — that's exactly the drift ADR-0012 exists to prevent.

## Reference map

| Concern | Real file |
| --- | --- |
| Per-service database resources | [`AppHost.cs`](../../../src/JobBoard.AppHost/AppHost.cs) |
| Reference-data duplication convention | [Adding Seed Data](../adding-seed-data.md) |
| Event-carried denormalization example | [`JobClosed.cs`](../../../src/JobBoard.Contracts/JobClosed.cs) |
| The open cross-service-read decision | [ADR-0012](../../adr/0012-cross-service-read-model-strategy.md) |
