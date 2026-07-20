# Adding an Endpoint by Hand

*A developer's walkthrough for adding a REST endpoint (or an event-driven handler) to a JobBoard
service — the same architecture the `add-endpoint` skill produces, done manually, no Claude Code
required. Every code snippet in this guide is real code from the **Jobs** service; copy the shape,
swap the names.*

This guide is the human-readable twin of [`.claude/skills/add-endpoint/SKILL.md`](../../.claude/skills/add-endpoint/SKILL.md).
If you follow it and the [checklist](#the-checklist) passes, your endpoint matches house style. For the
*why* behind each rule, see the [ADRs](../adr/README.md) — most relevantly
[0005 (thin host + layered `.Core`)](../adr/0005-thin-host-core-layered-library.md),
[0003 (transactional outbox)](../adr/0003-hand-rolled-transactional-outbox.md), and
[0004 (idempotent inbox)](../adr/0004-idempotent-inbox-at-least-once-delivery.md).

---

## Step 0 — Decide which service owns this (before you touch code)

The owner is **the service whose database holds the data being changed or read.** A job lives in
**Jobs**, an application in **Applications**, an account in **Identity**, a profile in **Profiles**.
Build the whole endpoint inside that one service.

> **The one-service rule.** If satisfying the request needs *another* service's data to **change**,
> it is **not one endpoint**. It's an endpoint in one service **plus an integration event** the other
> service reacts to. You may never open a second database, and you may never let a Domain entity from
> one service appear in another. When you're unsure who owns it, stop and settle ownership first — a
> change that spans two services is a design conversation, not a bigger commit. See
> [Reacting to another service](#reacting-to-another-service-the-two-service-shape).

Once the owner is fixed, remember the service is **two projects**:

| Project | Role | What goes here |
| --- | --- | --- |
| `JobBoard.<Service>` | **Host** — thin | `Controllers/`, `Consumers/`, `Program.cs` (composition root). Entry points only. |
| `JobBoard.<Service>.Core` | **Library** — the stack | `Facade/` `Business/` `Data/` `Managers/{Models,Validators,Mappers}` `Migrations/`. Everything else. |

Both build on `JobBoard.Shared` (the outbox/inbox/dispatcher/base-repo machinery you reuse unchanged)
and reference `JobBoard.Contracts` (integration-event records). The reference direction is one-way and
acyclic — `Contracts` ← `Shared` ← `.Core` ← host. A host references **its own** `.Core` and nothing
else. If a reference points sideways to another service, the design is wrong.

---

## The mental model: three model types, five layers

**Three model types are the whole idea.** A request enters as a **ViewModel** and a response leaves as
a **ServiceModel** — those are the only types on the wire. In between, work is done on **Domain**
entities (the EF shape). There is no separate DTO layer. Nothing leaks: no EF entity ever reaches the
controller, no ViewModel ever reaches the database, and no Domain entity ever crosses the *service*
boundary (that's what integration events are for).

| Type | Folder (`<Service>.Core/Managers/Models/`) | Lives between | Created by |
| --- | --- | --- | --- |
| **ViewModel** | `ViewModels/` | client → controller → facade | model binder |
| **Domain** entity | `Domain/` | business ↔ data ↔ EF | business (from VM) / EF (on load) |
| **ServiceModel** | `ServiceModels/` | business → facade → controller → client | business (from entity) |
| **IntegrationEvent** | *`JobBoard.Contracts/`* | this service → outbox → bus → others | business (builds it) |

**Five layers, each with a strict job.** Data flows down through interfaces and back up. Each layer
depends only on the **interface** of the layer below (`IJobFacade` → `IJobBusiness` → `IJobDataLayer`
→ `IJobRepository`), never a concrete class or a lower layer's dependencies.

```
Controller  →  Facade              →  Business                   →  DataLayer                →  Repository
Consumer    ↗   (validate the VM +     (VM→domain, domain rules,     (compose repo calls +      (EF queries;
  (bus in)      cache; return SM)       DECIDE + BUILD the event,     enqueue the outbox row      returns
                                        domain→SM)                    as ONE transaction)         entities)
                                                                    → IOutbox (event → row)
```

| Layer | Project / folder | Owns | Must NOT do |
| --- | --- | --- | --- |
| **Controller / Consumer** | host `Controllers/` `Consumers/` | Bind a ViewModel (or map an event), call the facade, return `ActionResult<ServiceModel>`. Consumer dedupes via the inbox. | Validation, caching, rules, data access. Never sees an entity. |
| **Facade** | `.Core/Facade/` | Validate the ViewModel; read-through cache of ServiceModels. | Mapping, orchestration, EF, bus. |
| **Business** | `.Core/Business/` | Translate VM→Domain, apply domain rules, **build** the event, map Domain→ServiceModel. | Validation, cache, EF, *sending* the event. |
| **Data layer** | `.Core/Data/` | Compose repository calls + **enqueue the outbox row in the same transaction** via `IOutbox`. Owns the transaction boundary. | Rules, mapping, cache, validation, holding a `DbContext`, Service Bus. |
| **Repository** | `.Core/Data/` | EF queries against `<Service>DbContext`; `ExecuteInTransactionAsync` (inherited). | Rules, cache, validation, outbox knowledge. |

The **data layer's only non-feature dependency is `IOutbox`** — that's how the outbox row lands on the
same scoped `DbContext` and enlists in the same transaction as the domain write.

---

## The build order

Build **bottom-up** (models → repository → data layer → business → facade → entry point → wiring). Each
layer compiles against the one below it, so this order never leaves you with a dangling reference. The
worked example below adds the **post-a-job** endpoint; every file shown already exists in the repo — read
them alongside this guide.

### 1. ViewModel — the inbound shape

`<Service>.Core/Managers/Models/ViewModels/PostJobViewModel.cs`. The only type the controller binds from
the wire. Keep it flat and request-shaped; no behavior.

```csharp
// One public type per file. ViewModels are request-shaped records.
public sealed record PostJobViewModel(
    string Title,
    string Description,
    string Location,
    SalaryBandViewModel Salary,
    Guid EmployerId,
    IReadOnlyList<JobClassificationViewModel> Categories,
    IReadOnlyList<JobClassificationViewModel> Tags);
```

### 2. ServiceModel — the outbound shape

`<Service>.Core/Managers/Models/ServiceModels/`. The only type the API returns. A **detail** model for
a single-item read/write; a **summary** model for lists. The summary is what the repository projects to
directly in SQL (step 4).

```csharp
public sealed record JobDetailServiceModel(
    Guid Id, string Title, string Description, string Location,
    SalaryBandServiceModel Salary, JobStatus Status, Guid EmployerId,
    IReadOnlyList<JobClassificationServiceModel> Categories,
    IReadOnlyList<JobClassificationServiceModel> Tags, DateTime CreatedOnUtc);

public sealed record JobSummaryServiceModel(
    Guid Id, string Title, string Location, SalaryBandServiceModel Salary,
    JobStatus Status, IReadOnlyList<string> Categories, DateTime CreatedOnUtc);
```

### 3. Validator — shape rules at the edge

`<Service>.Core/Managers/Validators/PostJobViewModelValidator.cs`. A FluentValidation
`AbstractValidator<TViewModel>` for **every write ViewModel**. **Shape/format only** — required fields,
lengths, ranges. Data-dependent rules that need the database ("can't apply to a closed job") are *domain*
rules and go in business (step 6), not here.

```csharp
public sealed class PostJobViewModelValidator : AbstractValidator<PostJobViewModel>
{
    public PostJobViewModelValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Location).NotEmpty();
        RuleFor(x => x.EmployerId).NotEmpty();
        // ...salary band, classifications, etc.
    }
}
```

The facade throws `ValidationException`; the shared exception handler maps it to a `400` with field
detail — you never write that mapping.

### 4. Mappers — the translation seams

`<Service>.Core/Managers/Mappers/JobMappers.cs`. Extension methods for the seams the **business** layer
owns, kept in one place instead of scattered through the layers:

- `ViewModel.ToEntity()` — VM → Domain (a write).
- `Entity.ToServiceModel()` — Domain → ServiceModel (every response).
- `Entity.ToIntegrationEvent()` — Domain → the `Contracts` record, **stamping a fresh event `Id`** (only
  if this write emits an event).

```csharp
public static class JobMappers
{
    public static Job ToEntity(this PostJobViewModel vm) => new()
    {
        Id = Guid.NewGuid(),
        Title = vm.Title,
        // ...
        Status = JobStatus.Open,
        CreatedOnUtc = DateTime.UtcNow,
    };

    public static JobDetailServiceModel ToDetailServiceModel(this Job job) => new(
        job.Id, job.Title, job.Description, job.Location, /* ... */ job.CreatedOnUtc);

    // Builds the fact, stamping a fresh event id (its outbox-row key AND Service Bus MessageId).
    public static JobPosted ToJobPosted(this Job job) =>
        new(Guid.NewGuid(), job.Id, job.EmployerId, job.Title, job.Location, job.CreatedOnUtc);
}
```

> The **list-summary projection is *not* a mapper** — the repository projects it in SQL (step 4) so the
> database never materializes full entities for a list.

### 5. Repository — data only

`<Service>.Core/Data/IJobRepository.cs` + `JobRepository.cs`. EF Core queries against
`<Service>DbContext`, one self-contained operation per method. The interface **extends `IRepository`**
(from `JobBoard.Shared`) so the data layer can call `ExecuteInTransactionAsync`; the class **extends
`BaseRepository<TContext>`** to inherit it.

- **Detail reads / writes** return the **Domain entity** (with the `Include`s it needs).
- A **list read** projects straight to its summary **ServiceModel in SQL** — never loads entities.
- A **state transition** is a **conditional write** (`UPDATE ... WHERE ... AND Status = @expected`), not
  a load-mutate-save (see [concurrency](#concurrency-guard-the-write-not-the-read)).
- It may **classify** a constraint violation, but it never *catches* one (that happens later, inside the
  transaction — see step 6).

```csharp
public sealed class JobRepository : BaseRepository<JobsDbContext>, IJobRepository
{
    public JobRepository(JobsDbContext context) : base(context) { }

    // List read → projects to the summary ServiceModel in SQL. AsNoTracking, no entity materialized.
    public async Task<IReadOnlyList<JobSummaryServiceModel>> ListAsync(
        string? categorySlug, CancellationToken cancellationToken = default)
    {
        var query = Context.Jobs.AsNoTracking().Where(j => j.Status == JobStatus.Open);
        if (!string.IsNullOrWhiteSpace(categorySlug))
            query = query.Where(j => j.Categories.Any(c => c.Slug == categorySlug));

        return await query
            .OrderByDescending(j => j.CreatedOnUtc)
            .Select(j => new JobSummaryServiceModel(/* ...columns... */))
            .ToListAsync(cancellationToken);
    }

    // Detail read → returns the entity with its graph.
    public Task<Job?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        Context.Jobs.AsNoTracking()
            .Include(j => j.Categories).Include(j => j.Tags)
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

    // Write → stages the insert (the data layer commits it inside a transaction).
    public async Task<Job> AddAsync(Job job, CancellationToken cancellationToken = default)
    {
        await Context.Jobs.AddAsync(job, cancellationToken);
        return job;
    }

    // State transition → authoritative conditional UPDATE. Reports whether a row actually flipped.
    public async Task<bool> CloseIfOpenAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var affected = await Context.Jobs
            .Where(j => j.Id == id && j.Status == JobStatus.Open)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, JobStatus.Closed), cancellationToken);
        return affected > 0;
    }

    // Classifier only — the catch lives in the data layer (the transaction owner).
    public static bool IsDuplicateSlugViolation(DbUpdateException exception) => /* inspect provider ex */;
}
```

### 6. Data layer — compose + enqueue the outbox row atomically

`<Service>.Core/Data/IJobDataLayer.cs` + `JobDataLayer.cs`. Turns one logical write into however many
repository calls it takes, and — because the event must ship **exactly when the write commits** — writes
the outbox row **in the same transaction** by calling `IOutbox`. It depends on `IJobRepository` and
`IOutbox`, and holds **no `DbContext` of its own**.

**Reads pass straight through** (single self-contained op, no transaction):

```csharp
public Task<Job?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
    _repository.GetAsync(id, cancellationToken);
```

**Writes that emit an event (or write more than once) are wrapped in a transaction.** Hand the *whole*
operation — including the `IOutbox.EnqueueAsync` — to `ExecuteInTransactionAsync` as a callback:

```csharp
public async Task<Job> AddAsync(Job job, JobPosted @event, CancellationToken cancellationToken = default)
{
    try
    {
        return await _repository.ExecuteInTransactionAsync(
            async token =>
            {
                var saved = await _repository.AddAsync(job, token);
                await _outbox.EnqueueAsync(@event, token);   // same DbContext, same transaction
                return saved;                                // a throw on any leg rolls BOTH rows back
            },
            cancellationToken);
    }
    catch (DbUpdateException ex) when (JobRepository.IsDuplicateSlugViolation(ex))
    {
        // The violation surfaces from SaveChanges *inside* the transaction, so the catch lives here.
        throw new DomainException("job.classification_conflict",
            "A category or tag with the same slug was just created. Please retry.",
            StatusCodes.Status409Conflict);
    }
}
```

> **Why a callback and not a `BeginTransactionAsync`?** Aspire's Npgsql integration enables
> retry-on-failure, and its execution strategy **refuses to run inside a caller-opened transaction**. You
> pass the whole unit in so the two can coexist. Two consequences: (1) the callback **may run more than
> once**, so it must be safe to repeat — writing the outbox row by a deterministic event `Id` keeps a
> replay from duplicating it (`Outbox.EnqueueAsync` dedupes on id); and (2) only work done *through this
> `DbContext`* is transactional — a raw HTTP call to another service inside the callback is **not** rolled
> back and doesn't belong here. That's what the event is for.

`IOutbox.EnqueueAsync` serializes the `Contracts` event to an `OutboxMessages` row (Id = the event's Id,
payload JSON, event-type name, destination topic). Nothing here touches Service Bus — the **dispatcher**
relays the row later (step 9).

> **Pass-through is fine.** When a single repository call with no event already *is* the whole operation,
> the data-layer method is a one-liner. That's still the seam business depends on — don't skip it.

### 7. Integration event — *only if other services care*

`src/JobBoard.Contracts/JobPosted.cs`. An immutable **record** implementing `IIntegrationEvent` (a
`Guid Id`), **past-tense** (a fact that happened, not a command), carrying **only** the fields a consumer
needs — IDs plus the minimum denormalized data to avoid a call-back. No behavior, no EF, no reference to
your service's Domain types.

```csharp
public sealed record JobPosted(
    Guid Id, Guid JobId, Guid EmployerId,
    string Title, string Location, DateTime PostedOnUtc) : IIntegrationEvent;
```

`Contracts` is a **leaf** — it references nothing, and everything references it. Changing an existing
event is a **contract change** affecting every consumer; treat it as one. If your endpoint changes only
its own service's data and no one reacts, **skip this step** — not every write emits an event.

### 8. Business — rules, translation, and the decision to emit

`<Service>.Core/Business/IJobBusiness.cs` + `JobBusiness.cs`. The domain brain. Depends only on
`IJobDataLayer`.

- **Detail read:** map the returned entity → ServiceModel.
- **List read:** pass the data layer's projected summaries straight through.
- **Write:** translate ViewModel → Domain, apply **data-dependent domain rules** (throwing
  `DomainException` on violation), **decide which event the change warrants and build it**, hand it to the
  data layer alongside the entity, then map the persisted entity → ServiceModel.

It **builds** the event; it never serializes or sends it.

```csharp
public async Task<JobDetailServiceModel> PostAsync(PostJobViewModel vm, CancellationToken ct = default)
{
    var job = vm.ToEntity();
    var posted = job.ToJobPosted();                       // BUILD the event (business decides it's warranted)
    var saved = await _dataLayer.AddAsync(job, posted, ct); // hand entity + event down together
    return saved.ToDetailServiceModel();
}

public async Task<JobDetailServiceModel> CloseAsync(Guid id, CancellationToken ct = default)
{
    var job = await _dataLayer.GetAsync(id, ct)
        ?? throw new DomainException("job.not_found", $"Job '{id}' was not found.", StatusCodes.Status404NotFound);

    if (job.Status != JobStatus.Open)                     // domain rule (fast path)
        throw new DomainException("job.not_open", $"Job '{id}' is not open and cannot be closed.");

    var closed = job.ToJobClosed();
    var didClose = await _dataLayer.CloseAsync(job.Id, closed, ct); // authoritative guard
    if (!didClose)                                        // a concurrent close won → conflict, no event shipped
        throw new DomainException("job.not_open", $"Job '{id}' is not open and cannot be closed.");

    job.Status = JobStatus.Closed;
    return job.ToDetailServiceModel();
}
```

### 9. Facade — validation + caching

`<Service>.Core/Facade/IJobFacade.cs` + `JobFacade.cs`. The boundary the controller/consumer calls. It
**validates** the ViewModel with the injected `IValidator<T>`, owns **read-through caching** of
ServiceModels (cache on queries, invalidate on writes), and returns ServiceModels. Depends on
`IJobBusiness`, the validator, and `ICache`. No mapping, orchestration, EF, or bus.

```csharp
public async Task<JobDetailServiceModel> PostAsync(PostJobViewModel vm, CancellationToken ct = default)
{
    await _postValidator.ValidateAndThrowAsync(vm, ct);   // global handler maps ValidationException → 400
    var posted = await _business.PostAsync(vm, ct);
    await InvalidateListAsync(ct);                        // a new posting joins the list — refresh it
    return posted;
}
```

The cache is **fail-open**: it's an optimization, not a source of truth, so a Redis blip degrades to
serving from the source rather than failing the request. See
[`JobFacade.ListAsync`](../../src/JobBoard.Jobs.Core/Facade/JobFacade.cs) for the full read-through +
generation-token invalidation pattern (documented in
[ADR-0009](../adr/0009-read-through-cache-generation-invalidation.md)).

### 10. Entry point — a controller *or* a consumer (host project)

**For a client-driven route** — a thin **Controller** action in
`JobBoard.<Service>/Controllers/<Feature>Controller.cs`. Bind the ViewModel, call the facade, return
`ActionResult<ServiceModel>`. No logic.

```csharp
[ApiController]
[Route("jobs")]
public sealed class JobsController : ControllerBase
{
    private readonly IJobFacade _facade;
    public JobsController(IJobFacade facade) => _facade = facade;

    [HttpPost]
    public async Task<ActionResult<JobDetailServiceModel>> Post(
        [FromBody] PostJobViewModel viewModel, CancellationToken cancellationToken)
    {
        var job = await _facade.PostAsync(viewModel, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = job.Id }, job);
    }
}
```

**For an event-driven reaction** — a **Consumer** in `JobBoard.<Service>/Consumers/<Event>Consumer.cs`
implementing `IIntegrationEventConsumer<TEvent>` (from `JobBoard.Shared`). The shared processor host
resolves and calls it when the event arrives. It maps the event to a facade call and is **idempotent via
the inbox**: in the same transaction as its side effect, it checks `InboxMessages` for the message ID,
applies the change and records the ID, or no-ops if already present (use the same
`ExecuteInTransactionAsync` seam so the inbox row and the side effect commit together). No domain logic of
its own — it's an entry point, not a layer.

### 11. DI wiring — owned by `.Core`, called from the host

Registration lives in the **library**, not scattered through the host. In
`JobBoard.<Service>.Core/DependencyInjection/<Service>CoreServiceCollectionExtensions.cs`, register every
layer (scoped) plus the validators from this assembly:

```csharp
public static IServiceCollection AddJobsCore(this IServiceCollection services)
{
    services.AddScoped<IJobRepository, JobRepository>();
    services.AddScoped<IJobDataLayer, JobDataLayer>();
    services.AddScoped<IJobBusiness, JobBusiness>();
    services.AddScoped<IJobFacade, JobFacade>();
    services.AddValidatorsFromAssemblyContaining<JobsCoreMarker>();  // once, from Core
    return services;
}
```

If you added a new layer *interface*, add its `AddScoped`. If you only added methods to existing layers,
this file is already correct. The host's `Program.cs` is just the composition root — it calls
`AddJobsCore()` plus the shared extensions; **no per-layer wiring lives in the host**:

```csharp
builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<JobsDbContext>("jobsdb");     // Aspire — connection from the AppHost resource
builder.AddAzureServiceBusClient("servicebus");          // Aspire
builder.AddRedisDistributedCache("cache");               // Aspire

builder.Services.AddJobsCore();                          // the .Core stack + validators
builder.Services.AddSharedPersistence<JobsDbContext>();  // IOutbox/IInbox + base repository
builder.Services.AddSharedMessaging<JobsDbContext>();    // OutboxDispatcher + processor host
builder.Services.AddSharedCaching();                     // ICache → RedisCache
builder.Services.AddSharedExceptionHandler();
```

**If you added a consumer** (step 10), register it so the processor host can resolve it:

```csharp
builder.Services.AddScoped<IIntegrationEventConsumer<JobClosed>, JobClosedConsumer>();
```

Everything comes from the Aspire integrations — **never a hardcoded connection string, broker address,
or `localhost:port`.**

### 12. The dispatcher + the destination topic (send side — mostly one-time)

You do **not** write send-side plumbing. The **`OutboxDispatcher`** (in `JobBoard.Shared`, a
`BackgroundService` already registered by `AddSharedMessaging`) polls unprocessed `OutboxMessages`
oldest-first, sends each to Service Bus as a `ServiceBusMessage` (`MessageId` = the row Id, `Subject` =
the event-type name, body = the payload), then stamps `ProcessedOnUtc`. It is the **only** thing that
talks to Service Bus on the send side, and delivery is **at-least-once** (a crash between send and stamp
resends the same `MessageId`, which consumers dedupe).

A **new event type** usually just needs its **destination topic** mapped and that topic declared as a
Service Bus resource in the **AppHost** (and the emulator's entity config). Use the
[`add-aspire-resource`](../../.claude/skills/add-aspire-resource/SKILL.md) skill or add the resource by hand
in `src/JobBoard.AppHost/AppHost.cs` — the dispatcher loop itself is reused unchanged.

### 13. Gateway route — *client-facing endpoints only*

A service endpoint is unreachable from the browser until the gateway proxies it — **the gateway is the
only public door.** Add a YARP route + cluster in `src/JobBoard.Gateway/appsettings.json`, mapping the
public path to the owning service **by its Aspire resource name** (`http://jobs`), never a `host:port`:

```json
"ReverseProxy": {
  "Routes": {
    "jobs": { "ClusterId": "jobs", "Match": { "Path": "/jobs/{**catch-all}" } }
  },
  "Clusters": {
    "jobs": { "Destinations": { "jobs": { "Address": "http://jobs" } } }
  }
}
```

Add `"AuthorizationPolicy": "authenticated"` to a route that requires a valid JWT (see the
`applications` and `profiles-*-write` routes for examples). An **internal-only** endpoint (e.g. anything
in Notifications) deliberately has **no** route — that's by design. A client-facing one without a route
is a bug you'll notice as a 404 from the browser.

### 14. Migration — *only if the model changed*

The `DbContext` lives in `.Core` but DI/config resolve in the **host**, so run from the host folder with
both projects named:

```bash
cd src/JobBoard.Jobs
dotnet ef migrations add <Name> --project ../JobBoard.Jobs.Core --startup-project . --context JobsDbContext
# review the generated migration, then:
dotnet ef database update --project ../JobBoard.Jobs.Core --startup-project . --context JobsDbContext
```

Confirm it's clean and commit it:

```bash
dotnet ef migrations has-pending-model-changes --project ../JobBoard.Jobs.Core --startup-project . --context JobsDbContext
```

Never hand-edit a generated migration except to review it, and never point `--context` at another
service's context. The `OutboxMessages` / `InboxMessages` tables (from the Shared base context) come in
via the service's first migration — you don't add them.

### 15. Tests — per layer, mock the layer below

Run `dotnet test` **in the owning service**; for bus-crossing work, run the **consumer** service's tests
too.

| Layer | What to assert | Test double |
| --- | --- | --- |
| **Repository** | The query returns the expected entities / summary ServiceModels. | Real/containerized Postgres |
| **Data layer** | For a composed write: the right repository calls **and** the right event enqueued, **in order**, commit last, and **nothing** commits when a leg throws. Back it with **one real-database test** that a mid-operation failure leaves neither the domain row nor the outbox row. Pass-through: delegates unchanged. | Mocked `IOutbox` + `IRepository` (+ one real DB) |
| **Business** | Reads: mapping + list pass-through. Writes: VM→domain translation, the domain rule, and that it **builds the correct event** (and none when unwarranted). | Mocked `IDataLayer` |
| **Facade** | A cache **hit**, a **miss**, and a **validation failure**. | Mocked `IBusiness`, real validator, in-memory cache |
| **Consumer** (if added) | The event maps to the right facade call; a **duplicate message ID applies the side effect once** (inbox blocks the replay). | Mocked facade + inbox |
| **Endpoint** | Happy path + one validation failure; assert on ServiceModels, post ViewModels; assert the outbox row was written on the happy path. | `WebApplicationFactory<Program>` |

> `Program.cs` ends with `public partial class Program;` precisely so `WebApplicationFactory<Program>`
> can host the real pipeline. Don't remove it.

---

## Concurrency: guard the write, not the read

Two requests can pass the same read-side check before either commits, so **a rule enforced only against
a loaded entity is not enforced.** Two shapes recur; both belong in the **data-layer operation (the
transaction)**, never the read:

- **State transition** (close a job, advance an application) → a **conditional write**:
  `UPDATE ... SET Status = @next WHERE Id = @id AND Status = @expected` (EF `ExecuteUpdateAsync`). Treat
  **0 rows affected** as "someone got here first" → the conflict, and **emit no event**. The loaded
  entity's status check is only a fast path; the conditional write is the authoritative guard (and it
  lets the read go `AsNoTracking`). See `CloseIfOpenAsync` above.
- **Get-or-create** (resolve categories/tags by slug, create if missing) → the `SELECT`-then-`INSERT`
  races: two posts insert the same brand-new key and the second trips the unique index.
  `catch (DbUpdateException e) when (Repo.IsDuplicate…Violation(e))` in the **data-layer** method and
  throw a **retryable 409** `DomainException`; a client retry then finds and reuses the committed row.
  See `AddAsync` above.

---

## Reacting to another service (the two-service shape)

"When a job closes, close its open applications" is **not** a Jobs endpoint that writes Applications'
data. It's two pieces, in two databases, with one event between them and no shared table:

1. **Jobs** already publishes `JobClosed` from its close-job endpoint — business builds it, the data
   layer enqueues it to Jobs' outbox in the write transaction, Jobs' dispatcher relays it (steps 6–9,
   done in Jobs).
2. **Applications** adds a `JobClosedConsumer` (step 10) that its processor host calls; the consumer
   dedupes on the message ID via **its** inbox and calls **its own** facade to close the affected
   applications in **its** database — same layers, same rules, same outbox if that in turn emits
   `ApplicationStatusChanged`.

That's the whole pattern. Never reach across; never add a synchronous call in place of the event.

---

## The checklist

Before you call the endpoint done:

- [ ] The change lives in **one** owning service; anything another service needs went out as a
      `Contracts` **event**, not a cross-database read.
- [ ] Files are in the right **project**: controllers + consumers + `Program.cs` in the host;
      facade/business/data/repository + models/validators/mappers in `<Service>.Core`; outbox/inbox/
      dispatcher/base-repo **reused** from `JobBoard.Shared`.
- [ ] The host is thin — no facade→repository logic in a controller, consumer, or `Program.cs`;
      registration goes through `Add<Service>Core()` + the Shared extensions.
- [ ] Reference direction holds: `Contracts` ← `Shared` ← `.Core` ← host; the host references only its
      own `.Core`.
- [ ] Only ViewModels enter and only ServiceModels leave; no EF entity crosses the controller boundary,
      no Domain entity crosses the service boundary.
- [ ] Controller/consumer are thin; the consumer dedupes via the **inbox** and is idempotent.
- [ ] Facade owns validation + caching; business translates + applies rules + **builds** the event (never
      serializes/sends it); the data layer composes repo calls **and** the `IOutbox` write into one atomic
      operation; the repository does queries only.
- [ ] Any write-and-emit (or multi-write) commits the domain row(s) + outbox row **together or not at
      all**.
- [ ] Concurrency is guarded in the **write**: a state transition uses a conditional
      `UPDATE ... WHERE Status = @expected` (0 rows → conflict, no event); a get-or-create maps the
      unique-constraint violation to a retryable 409.
- [ ] Integration event (if any) is a small **past-tense** record with an `Id` in `JobBoard.Contracts`,
      no domain types; only the **OutboxDispatcher** sends it, with `MessageId` = the outbox row Id.
- [ ] `DbContext`, cache, and Service Bus client all come from **Aspire integrations** — no hardcoded
      strings.
- [ ] Client-facing endpoints have a gateway route (by Aspire resource name); internal-only ones
      deliberately don't.
- [ ] Tests per layer pass (incl. facade cache hit/miss/validation, the data-layer order/short-circuit +
      a real-database rollback test, consumer inbox idempotency, and the endpoint integration test);
      migration reviewed, committed, `--context` correct, and `has-pending-model-changes` clean (if the
      model changed).

---

## Reference map

| You're touching… | Read the real file |
| --- | --- |
| Controller | [`JobsController.cs`](../../src/JobBoard.Jobs/Controllers/JobsController.cs) |
| Facade (+ cache pattern) | [`JobFacade.cs`](../../src/JobBoard.Jobs.Core/Facade/JobFacade.cs) |
| Business (+ rules, build event) | [`JobBusiness.cs`](../../src/JobBoard.Jobs.Core/Business/JobBusiness.cs) |
| Data layer (+ atomic outbox) | [`JobDataLayer.cs`](../../src/JobBoard.Jobs.Core/Data/JobDataLayer.cs) |
| Repository (+ conditional write, classifier) | [`JobRepository.cs`](../../src/JobBoard.Jobs.Core/Data/JobRepository.cs) |
| Mappers | [`JobMappers.cs`](../../src/JobBoard.Jobs.Core/Managers/Mappers/JobMappers.cs) |
| DI wiring | [`JobsCoreServiceCollectionExtensions.cs`](../../src/JobBoard.Jobs.Core/DependencyInjection/JobsCoreServiceCollectionExtensions.cs) · [`Program.cs`](../../src/JobBoard.Jobs/Program.cs) |
| Integration event | [`JobPosted.cs`](../../src/JobBoard.Contracts/JobPosted.cs) |
| Gateway route | [`appsettings.json`](../../src/JobBoard.Gateway/appsettings.json) |
| The canonical playbook | [`.claude/skills/add-endpoint/SKILL.md`](../../.claude/skills/add-endpoint/SKILL.md) |
| The standing rules | [`.claude/rules/backend.md`](../../.claude/rules/backend.md) · [`messaging.md`](../../.claude/rules/messaging.md) · [`gateway.md`](../../.claude/rules/gateway.md) |
