# Layered Service Architecture

*Thin host + layered `.Core` library — Facade → Business → Data layer → Repository — with three model
types at the boundary. The shape every one of the five services repeats.*

## The problem this solves

A microservice has to stay changeable by someone other than its original author, and it has to keep
EF Core, caching, and Service Bus out of each other's way. A single flat "controller does everything"
class makes that impossible to review or test in isolation: you can't unit-test a validation rule
without a database, or a repository query without spinning up a transaction. JobBoard's answer is to
give every layer exactly one job and let each depend only on the *interface* of the layer below it, so
each is testable and replaceable on its own.

## How it works here

Every service is **two projects**. The host (`JobBoard.<Service>`) is deliberately thin — entry points
and the composition root, nothing else. The library (`JobBoard.<Service>.Core`) holds the whole stack:

```
Controller  →  Facade              →  Business                   →  DataLayer                →  Repository
Consumer    ↗   (validate the VM +     (VM→domain, domain rules,     (compose repo calls +      (EF queries;
  (bus in)      cache; return SM)       decide + build the event,     enqueue the outbox row      returns
                                        domain→SM)                    as ONE transaction)         entities)
                                                                    → IOutbox (event → row)
```

Walking the **Jobs** service's `POST /jobs` end to end:

**Controller** — [`JobsController.cs`](../../../src/JobBoard.Jobs/Controllers/JobsController.cs) binds
a `PostJobViewModel`, calls the facade, returns `ActionResult<JobDetailServiceModel>`. No logic.

**Facade** — [`JobFacade.cs`](../../../src/JobBoard.Jobs.Core/Facade/JobFacade.cs):

```csharp
public async Task<JobDetailServiceModel> PostAsync(PostJobViewModel viewModel, CancellationToken cancellationToken = default)
{
    await _postValidator.ValidateAndThrowAsync(viewModel, cancellationToken);
    var posted = await _business.PostAsync(viewModel, cancellationToken);
    await InvalidateListAsync(cancellationToken);
    return posted;
}
```

Validates the inbound ViewModel and owns the read-through cache (see
[Read-Through Caching](./read-through-caching.md) for the full `ListAsync`/`InvalidateListAsync` story).
It never maps a model, never opens a `DbContext`, never touches the bus.

**Business** — [`JobBusiness.cs`](../../../src/JobBoard.Jobs.Core/Business/JobBusiness.cs):

```csharp
public async Task<JobDetailServiceModel> PostAsync(PostJobViewModel viewModel, CancellationToken cancellationToken = default)
{
    var job = viewModel.ToEntity();
    var posted = job.ToJobPosted(_requestContext.RootThread());
    var saved = await _dataLayer.AddAsync(job, posted, cancellationToken);
    return saved.ToDetailServiceModel();
}
```

Translates ViewModel → Domain, decides a write is fact-worthy and **builds** the event (never sends
it), and maps Domain → ServiceModel on the way out. `CloseAsync` in the same file shows the other half
of business's job: a data-dependent domain rule (`job.Status != JobStatus.Open` → `DomainException`)
that no earlier layer could check.

**Data layer** — [`JobDataLayer.cs`](../../../src/JobBoard.Jobs.Core/Data/JobDataLayer.cs) composes
repository calls and enqueues the outbox row in the same transaction (its own deep dive:
[Transactional Outbox & Inbox](./transactional-outbox-and-inbox.md)). It holds no `DbContext` of its
own — only `IJobRepository` and `IOutbox`.

**Repository** — [`JobRepository.cs`](../../../src/JobBoard.Jobs.Core/Data/JobRepository.cs) is the
only layer that knows EF Core exists. `ListAsync` projects straight to `JobSummaryServiceModel` in
SQL — a list read never materializes a full entity. `GetAsync` returns the Domain entity with its
`Include`s. `CloseIfOpenAsync` is a conditional `UPDATE`, not a load-mutate-save — see
[Concurrency Control](./concurrency-control.md) for why that matters.

Each layer's interface is the contract the one above depends on (`IJobFacade` → `IJobBusiness` →
`IJobDataLayer` → `IJobRepository`) — never a concrete class, never a lower layer's own dependencies
(business never sees `IOutbox`; the facade never sees `IJobRepository`).

### The three model types (and the mappers between them)

Only two shapes cross the controller boundary: a **ViewModel** in, a **ServiceModel** out. In between,
work happens on a **Domain** entity (the EF shape). No EF entity ever reaches the controller; no Domain
entity ever crosses the *service* boundary (that's what integration events are for).

| Type | Folder | Lives between | Created by |
| --- | --- | --- | --- |
| ViewModel | `Managers/Models/ViewModels/` | client → controller → facade | model binder |
| Domain entity | `Managers/Models/Domain/` | business ↔ data ↔ EF | business (from VM) / EF (on load) |
| ServiceModel | `Managers/Models/ServiceModels/` | business → facade → controller → client | business (from entity) |

The translation seams are kept in one place —
[`JobMappers.cs`](../../../src/JobBoard.Jobs.Core/Managers/Mappers/JobMappers.cs) — as extension
methods business calls: `ToEntity()` (VM→Domain), `ToDetailServiceModel()` (Domain→ServiceModel), and
`ToJobPosted(AuditThread)` / `ToJobClosed(AuditThread)` (Domain→integration event, stamping a fresh
event `Id` plus the audit thread — see
[Correlation, Causation & the Audit Trail](./correlation-causation-and-audit-trail.md)). The
list-summary projection is deliberately **not** a mapper — the repository projects it in SQL so the
database never materializes entities it's only going to flatten again.

## Why

[ADR-0005](../../adr/0005-thin-host-core-layered-library.md) made this the standing shape, precisely
so a service's *internal* structure can be reasoned about the same way regardless of which service
you're in — the domain differs, the skeleton doesn't.

## Pitfalls / rules to respect

- **The host stays thin.** If logic is creeping into a controller, a consumer, or `Program.cs`, it's in
  the wrong project — it belongs in `.Core`.
- **No layer reaches past the one directly below it.** A controller never sees `IJobDataLayer`; a
  business class never sees `IOutbox` or a `DbContext`.
- **Nothing leaks.** No EF entity crosses the controller; no ViewModel reaches the database; no Domain
  entity crosses the service boundary.
- **One type per file.** Records, enums, and interfaces included — an interface and its implementation
  are two files.
- **Registration is owned by `.Core`.** `Add<Service>Core()` registers every layer + validators from
  its own assembly; the host's `Program.cs` just calls it.

See `.claude/rules/backend.md` for the full standing-rule list, and
[Adding an Endpoint by Hand](../adding-an-endpoint-manually.md) for the step-by-step build order this
shape implies (bottom-up: models → repository → data layer → business → facade → entry point).

## Reference map

| Layer | Real file |
| --- | --- |
| Controller | [`JobsController.cs`](../../../src/JobBoard.Jobs/Controllers/JobsController.cs) |
| Facade | [`JobFacade.cs`](../../../src/JobBoard.Jobs.Core/Facade/JobFacade.cs) |
| Business | [`JobBusiness.cs`](../../../src/JobBoard.Jobs.Core/Business/JobBusiness.cs) |
| Data layer | [`JobDataLayer.cs`](../../../src/JobBoard.Jobs.Core/Data/JobDataLayer.cs) |
| Repository | [`JobRepository.cs`](../../../src/JobBoard.Jobs.Core/Data/JobRepository.cs) |
| Mappers | [`JobMappers.cs`](../../../src/JobBoard.Jobs.Core/Managers/Mappers/JobMappers.cs) |
| DI wiring | [`JobsCoreServiceCollectionExtensions.cs`](../../../src/JobBoard.Jobs.Core/DependencyInjection/JobsCoreServiceCollectionExtensions.cs) |
| Standing rules | [`.claude/rules/backend.md`](../../../.claude/rules/backend.md) |
