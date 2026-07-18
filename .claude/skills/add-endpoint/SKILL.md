---
name: add-endpoint
description: >
  Add a REST endpoint (or an event-driven handler) to one JobBoard service, using the layered
  controller → facade → business → data layer → repository architecture inside the service, plus the
  cross-service seams microservices need. Use whenever creating or extending routes — e.g. "add an
  endpoint to post a job", "let candidates withdraw an application", "close applications when a job
  closes". Produces the controller action (or Service Bus consumer), view models, service models,
  facade (validate + cache), business (translate + rules + decide which event to publish), data layer
  (compose data ops + write the outbox row atomically), repository, validators, mappers,
  integration-event contract, gateway route, DI wiring, and tests that match this repo's conventions.
---

# Add an endpoint

First decide **which service owns this**, then build inside it. The owner is the service whose
database holds the data being changed or read — a job lives in **Jobs**, an application in
**Applications**, an account in **Identity**, and so on. If the work reads or writes two services'
data, it is **not one endpoint**: it's an endpoint in one service plus an **integration event** the
other reacts to. Never open a second database. When in doubt about ownership, stop and ask.

Once the owner is fixed, remember the service is **two projects**: a **thin host**
(`JobBoard.<Service>`) holding only entry points and the composition root, and a **library**
(`JobBoard.<Service>.Core`) holding the whole facade→repository stack. Both build on
`JobBoard.Shared`. Everything is organized **type-first**. Stop for review before running migrations.

## Target layout (host + Core library + Shared)

```
JobBoard.<Service>/                     # HOST — thin: entry points + composition root only
├── Controllers/            # <Feature>Controller.cs — HTTP surface (ViewModel in, ServiceModel out)
├── Consumers/              # <Event>Consumer : IIntegrationEventConsumer<TEvent> — calls the SAME facade
└── Program.cs             # composition root: Add<Service>Core() + the Shared registration extensions

JobBoard.<Service>.Core/                # LIBRARY — the whole facade → business → data → repository stack
├── Facade/                 # I<Feature>Facade  + <Feature>Facade   (validate VM + cache + return SM)
├── Business/               # I<Feature>Business + <Feature>Business (VM→domain, rules, build event, domain→SM)
├── Data/
│   ├── <Service>DbContext.cs   # derives from Shared's base context → inherits Outbox/Inbox sets
│   ├── I<Feature>DataLayer + <Feature>DataLayer   (compose data ops + enqueue outbox row via IOutbox)
│   └── I<Feature>Repository + <Feature>Repository (EF queries; ExecuteInTransactionAsync from the base repo)
├── Managers/
│   ├── Validators/         # FluentValidation validators for the view models
│   ├── Models/
│   │   ├── ViewModels/     # inbound request types — the ONLY thing the controller binds
│   │   ├── ServiceModels/  # outbound response types — the ONLY thing the API returns
│   │   └── Domain/         # EF entities + domain exceptions
│   └── Mappers/            # VM→domain, domain→ServiceModel, domain→integration event
├── Migrations/
└── Add<Service>CoreServiceCollectionExtensions.cs   # Add<Service>Core(): registers every layer + validators

JobBoard.Shared/   (referenced by every .Core; built once, reused)
    base DbContext (OutboxMessages + InboxMessages sets) · base repository + ExecuteInTransactionAsync ·
    IOutbox/IInbox + impls · OutboxDispatcher + Service Bus processor host · IIntegrationEventConsumer<T> ·
    global exception handler · cache abstraction · shared error shape
```

The host references **its own** `.Core` and nothing else; `.Core` references `JobBoard.Shared` and
`JobBoard.Contracts`. Integration-event **records** live in `Contracts`; the outbox/inbox/dispatcher
**machinery** lives in `Shared` and is reused unchanged — most endpoints add an event *type* and a
topic mapping, never new plumbing.

## The three model types (this is the core idea, and it's per service)

A request enters as a **ViewModel** and a response leaves as a **ServiceModel** — those are the only
types on the wire. In between, work is done on **Domain** entities. There is no separate DTO layer:
the domain entity *is* the internal shape, so a loaded entity maps directly to a service model.
Nothing leaks — no EF entity ever reaches the controller, and no view model ever reaches the DB.
**And nothing crosses a service boundary except an integration event or a ServiceModel over the
gateway** — a Domain entity from Jobs never appears in Applications; if Applications needs it, it
consumes the event and stores its own copy.

| Type | Folder | Lives between | Who creates it |
|------|--------|---------------|----------------|
| **ViewModel** | `Managers/Models/ViewModels/` | client → controller → facade | model binder |
| **Domain** entity | `Managers/Models/Domain/` | business ↔ data ↔ EF | business (from the VM) / EF (on load) |
| **ServiceModel** | `Managers/Models/ServiceModels/` | business → facade → controller → client | business (from the entity) |
| **IntegrationEvent** | `JobBoard.Contracts/` | this service → outbox → Service Bus → other services | business (builds it) |

## The layers (strict responsibilities)

```
Controller  →  Facade              →  Business                    →  DataLayer                →  Repository
Consumer    ↗   (validate the VM +     (translate VM→domain,          (compose repository calls    (EF queries;
  (bus in)      cache; return SM)       apply domain rules,            + enqueue the outbox row     returns
                                        DECIDE + BUILD the event,      as ONE transaction)          entities)
                                        domain→SM)                                                → IOutbox (serialize event→row)
```

- **Controller** (`Controllers/`) — HTTP only: bind the **ViewModel**, call the facade, return an
  `ActionResult<ServiceModel>`. No validation, cache, logic, or data access; never sees an entity.
- **Consumer** (host `Consumers/`) — the bus's version of a controller: an `<Event>Consumer`
  implementing `IIntegrationEventConsumer<TEvent>` (from `JobBoard.Shared`), which the shared Service
  Bus **processor host** resolves and calls when an event arrives from another service. It maps the
  event to a call on the **same facade** a controller would use, and it is **idempotent** — it checks
  the `InboxMessages` table for the message ID and no-ops on a repeat. It adds no domain logic of its
  own; it's an entry point in the host, not a layer.
- **Facade** (`Facade/`) — the boundary: **validates** the ViewModel (via a `Managers/Validators/`
  validator), handles **caching** of ServiceModels (read-through on queries, invalidate on writes),
  and returns ServiceModels. No orchestration, mapping, EF, or bus. Depends on `I<Feature>Business`.
- **Business** (`Business/`) — **domain rules, translation, and the decision to emit an event**:
  translates the validated **ViewModel → Domain** entity, applies data-dependent domain rules (e.g.
  "can't apply to a closed job"), **decides which integration event the change warrants and builds
  it** (a `Contracts` record, with a fresh event `Id`), and maps the returned **Domain entity →
  ServiceModel**. It builds the event; it neither serializes nor sends it. No validation, caching, or
  EF. Depends on `I<Feature>DataLayer`.
- **DataLayer** (`Data/`) — **composes data operations and enqueues the event atomically**: turns
  one logical write into however many repository calls it takes, and — because the event must ship
  exactly when the write commits — writes the outbox row **in the same transaction** by calling
  `IOutbox` (which runs on the same scoped `DbContext`). It owns the **transaction boundary**: it
  knows which repository calls and which outbox write form one unit. Passes an operation straight
  through when a single repository call with no event already *is* the whole operation. Depends on
  `I<Feature>Repository` and `IOutbox` — it holds no `DbContext`, so every query still belongs to the
  repository. No rules, mapping, cache, validation, or Service Bus (the dispatcher does the send,
  later and elsewhere).
- **Repository** (`Core/Data/`) — **data only**: EF Core queries against the Aspire-provided
  `<Service>DbContext`, plus `ExecuteInTransactionAsync` (inherited from the **base repository in
  `JobBoard.Shared`** — the one thing it exposes that isn't a query; it takes the data layer's whole
  operation, including the `IOutbox` write, as a callback and runs it in a transaction, so the domain
  rows and the outbox row commit together or not at all). Detail reads and writes return the **Domain
  entity**; a **list** read projects straight to its summary **ServiceModel** in SQL. Each method is
  one self-contained data operation. No rules, cache, or validation. It **classifies** a DB constraint
  violation (e.g. a `static bool IsDuplicate<X>Violation(DbUpdateException)` that inspects the provider
  exception), but note it can't *catch* one: a staging method (`AddAsync`) only queues the write; the
  violation is thrown later by `SaveChanges` **inside `ExecuteInTransactionAsync`**, so the **data
  layer** (the transaction owner) is where the `catch` lives — it calls the repository's classifier and
  maps the hit to a `DomainException`. See the get-or-create concurrency note in step 6.

Each layer depends on the **interface** of the one below it (`IJobFacade` → `IJobBusiness` →
`IJobDataLayer` → `IJobRepository`), never on a concrete class or a lower layer's dependencies.
`IOutbox` is the one shared (non-feature) dependency the data layer holds, so the outbox row lands on
the same `DbContext` and enlists in the same transaction as the domain write.

**Where a rule goes when it's ambiguous.** Same test as always — by *reason*, not by call count:
- **Business vs data layer:** if the sequencing is a **domain** decision (a rule says what may
  happen — "reject an application to a closed job") it's business; if it's a **persistence**
  consequence (the store must simply be left consistent) it's the data layer.
- **Who owns the event, at each stage:** *deciding* an event is warranted and *building* it is a
  **domain** decision → **business**. *Serializing* it to an outbox row is a persistence mechanism →
  **`IOutbox`**. *Enqueuing* that row atomically with the write → **data layer** (inside the
  transaction). *Sending* it to Service Bus and marking it processed → the **OutboxDispatcher**
  background service, entirely off the request path. If business talks to Service Bus, or the data
  layer serializes JSON, or the dispatcher invents an event, a seam has slipped.
- **One service or two:** if satisfying the request needs another service's data to *change*, it's
  two pieces — an endpoint here and an **event** the other service consumes. Don't reach across.

## Steps

1. **ViewModel** → `Managers/Models/ViewModels/`. Define the inbound request type(s) (e.g.
   `SubmitApplicationViewModel`). The only shape the controller binds from the wire.

2. **ServiceModel** → `Managers/Models/ServiceModels/`. Define the outbound response type(s) (e.g.
   `ApplicationSummaryServiceModel`, `ApplicationDetailServiceModel`). The only shape the API returns.

3. **Validator** → `Managers/Validators/`. Add a FluentValidation `AbstractValidator<TViewModel>`
   for each write ViewModel. Shape/format rules only — data-dependent rules that need the DB (or
   another service's state) go in business.

4. **Mappers** → `Managers/Mappers/`. Add the seams you touch: `ViewModel.ToEntity()` (business,
   VM→domain), `Entity.ToServiceModel()` (business, domain→SM), and — if this write emits an event —
   `Entity.ToIntegrationEvent()` (business, domain→the `Contracts` record, stamping a fresh event
   `Id`).

5. **Repository (`I<Feature>Repository` / `<Feature>Repository`)** → `Data/`. Add the
   query/persistence method against `<Service>DbContext`. Detail reads and writes return the **Domain
   entity** (with needed `Include`s); a list read projects to its summary **ServiceModel** in SQL.
   May translate a unique-index violation into the domain exception. One self-contained data operation
   per method. **No outbox knowledge here** — the outbox row is `IOutbox`'s job, called by the data
   layer inside the same transaction. No rules, cache, or validation.

6. **DataLayer (`I<Feature>DataLayer` / `<Feature>DataLayer`)** → `Data/`. Add the method business
   calls, composing however many repository calls the operation takes into one. **If the operation
   writes *and* emits an event, or writes more than once, make it atomic** — hand the whole operation,
   *including the outbox write*, to the repository as a callback:
   ```csharp
   var result = await _repository.ExecuteInTransactionAsync(
       async token =>
       {
           var saved = await _repository.AddApplicationAsync(application, token);
           // Same DbContext, same transaction: the event ships iff this row commits.
           await _outbox.EnqueueAsync(evt, token);
           return saved;                // a throw on any leg rolls the row AND the outbox row back
       },
       ct);
   ```
   `IOutbox.EnqueueAsync` serializes the `Contracts` event to an `OutboxMessages` row (Id = the
   event's Id, payload JSON, event-type name, destination topic) on the **same scoped `DbContext`**,
   so it enlists in the transaction with no distributed-transaction machinery — it's just another
   row. Nothing here touches Service Bus; the **OutboxDispatcher** relays the row afterward (step 11).
   **A callback, not a `BeginTransactionAsync` that hands back a transaction — and that shape is
   forced, not chosen.** Aspire's Npgsql integration enables retry-on-failure, and its execution
   strategy refuses to run inside a caller-opened transaction ("does not support user-initiated
   transactions"): it can't replay a unit whose boundaries it doesn't own. Passing the whole unit in
   is what lets the two coexist. Two consequences: the operation **may run more than once**, so it
   must be safe to repeat (writing the outbox row by a deterministic event Id keeps a replay from
   duplicating it); and only work done *through this `DbContext`* is transactional — a raw HTTP call
   to another service inside the callback is **not** rolled back and doesn't belong here (that's what
   the event is for). When a single repository call with no event already is the whole operation, this
   method is a one-line pass-through — expected, and still the seam business depends on. Depends on
   `I<Feature>Repository` and `IOutbox`; no `DbContext` of its own.

   **Concurrency — guard writes, don't trust the read.** Two requests can pass the same read-side check
   before either commits, so a rule enforced only against a loaded entity is not enforced. Two shapes
   recur; both belong in the data-layer operation (the transaction), never the read:
   - **State transition** (close a job, advance an application): make the change a **conditional
     write** — `UPDATE ... SET Status = @next WHERE Id = @id AND Status = @expected` (EF
     `ExecuteUpdateAsync`) — and treat **0 rows affected** as "someone got here first" → the conflict,
     and **emit no event**. The loaded entity's status check is only a fast path; the conditional
     write is the authoritative guard, and it lets the read go `AsNoTracking`.
   - **Get-or-create** (resolve categories/tags by slug, create if missing): the `SELECT`-then-`INSERT`
     has a race window where two posts insert the same brand-new key and the second trips the unique
     index. `catch (DbUpdateException e) when (Repo.IsDuplicate<X>Violation(e))` in this method and
     throw a **retryable 409** `DomainException` (the classifier is the repository's — step 5); a
     client retry then finds and reuses the now-committed row.

7. **Integration event** (only if the change is something other services care about) →
   `src/JobBoard.Contracts/`. Add an immutable event **record** with an `Id` (Guid) and a past-tense
   name — a fact that happened, not a command (`ApplicationSubmitted`, `JobClosed`), carrying **only**
   the fields a consumer needs (IDs, plus the minimum denormalized data to avoid a call-back). No
   behavior, no EF, no reference to this service's Domain types. This is the one type both sides
   share; keep it small, and treat a change to it as a contract change affecting every consumer.

8. **Business (`I<Feature>Business` / `<Feature>Business`)** → `Business/`. Add the method the facade
   calls. Detail reads: map the returned **entity → ServiceModel**. List reads: pass the data layer's
   projected summaries through. Writes: translate the **ViewModel → Domain** entity, apply
   data-dependent domain rules (throwing the domain exception on violation), **build the integration
   event when the change warrants one** and pass it to the data layer alongside the entity, then map
   the persisted **entity → ServiceModel**. It builds the event; it never serializes or sends it.
   Depends only on `I<Feature>DataLayer`.

9. **Facade (`I<Feature>Facade` / `<Feature>Facade`)** → `Facade/`. Add the method the controller (or
   consumer) calls. It **validates** the ViewModel with the injected `IValidator<TViewModel>` (the
   global handler maps `ValidationException` → 400), applies **caching** of ServiceModels
   (read-through on queries; invalidate affected keys on writes), and returns the ServiceModel.
   Depends on `I<Feature>Business`, the validator, and the cache abstraction. No mapping,
   orchestration, EF, or bus.

10. **Entry point (host project).** For a client-driven route: add a thin **Controller** action
    (`JobBoard.<Service>/Controllers/<Feature>Controller.cs`) that binds the ViewModel, calls the
    facade, and returns `ActionResult<ServiceModel>`. For an event-driven reaction: add a **Consumer**
    (`JobBoard.<Service>/Consumers/<Event>Consumer.cs`) implementing
    `IIntegrationEventConsumer<TEvent>` (from `JobBoard.Shared`); the shared processor host resolves
    and calls it when the event arrives. It maps the event to a facade call and is **idempotent via
    the inbox**: in the same transaction as its side effect, it checks `InboxMessages` for the message
    ID, applies the change and records the ID, or no-ops if already present. (Use the same
    `ExecuteInTransactionAsync` seam so the inbox row and the side effect commit together.) Both the
    controller and the consumer depend only on the `.Core` facade interface — no logic here.

11. **Outbox destination + dispatcher** (send side; mostly one-time). The **OutboxDispatcher** (in
    `JobBoard.Shared`, a `BackgroundService` registered per host) polls unprocessed `OutboxMessages`
    oldest-first, sends each to Service Bus (`ServiceBusMessage` with `MessageId` = the row Id,
    `Subject` = the event-type name, body = the payload) via the topic sender, then stamps
    `ProcessedOnUtc`. It is the only send-side thing that touches Service Bus, and delivery is
    **at-least-once** (crash after send, before stamp → resend with the same `MessageId`, which
    consumers dedupe). A **new event type** usually just needs its destination **topic** mapped and
    that topic declared as a Service Bus resource (AppHost + the emulator's entity config, step 12);
    the dispatcher loop itself is reused unchanged.

12. **Gateway route** (client-facing endpoints only) → `src/JobBoard.Gateway/`. Add the YARP route +
    cluster entry mapping the public path to the owning service **by its Aspire resource name** (e.g.
    `http://applications`), not a host:port. An endpoint with no gateway route is internal-only by
    design; a client-facing one without a route is unreachable — don't forget it.

13. **DI + messaging wiring.** Registration is owned by the library, not scattered through the host.
    In `JobBoard.<Service>.Core`, the `Add<Service>Core()` extension registers every layer for the
    feature (scoped) and the validators from its own assembly:
    ```csharp
    // Add<Service>CoreServiceCollectionExtensions.cs (in .Core)
    public static IServiceCollection AddApplicationsCore(this IServiceCollection services)
    {
        services.AddScoped<IApplicationRepository, ApplicationRepository>();
        services.AddScoped<IApplicationDataLayer, ApplicationDataLayer>();
        services.AddScoped<IApplicationBusiness, ApplicationBusiness>();
        services.AddScoped<IApplicationFacade, ApplicationFacade>();
        services.AddValidatorsFromAssemblyContaining<ApplicationsCoreMarker>(); // once, in Core
        return services;
    }
    ```
    The host's `Program.cs` is just the composition root — it calls that plus the shared extensions
    (no per-layer wiring in the host):
    ```csharp
    builder.AddServiceDefaults();
    builder.Services.AddApplicationsCore();
    builder.AddApplicationsDbContext();                 // Aspire Npgsql integration, keyed to "applicationsdb"
    builder.AddAzureServiceBusClient("servicebus");     // Aspire — connection from the AppHost resource
    builder.Services.AddSharedPersistence();            // IOutbox/IInbox + base repository (JobBoard.Shared)
    builder.Services.AddSharedMessaging<ApplicationsDbContext>(); // OutboxDispatcher + processor host
    builder.Services.AddSharedExceptionHandler();       // JobBoard.Shared
    builder.Services.AddScoped<IIntegrationEventConsumer<JobClosed>, JobClosedConsumer>(); // per new consumer
    ```
    Add each new `<Event>Consumer` registration so the shared processor host can resolve it. No
    hardcoded namespaces or connection strings — the DbContext and Service Bus client both come from
    the Aspire integrations.

14. **Cache backing.** Use the Aspire Redis client integration for the distributed cache (keyed to
    the AppHost `cache` resource) — no hardcoded connection details. Read-through + invalidate lives
    only in the facade, and it caches ServiceModels.

15. **Tests (per layer, mock the layer below).**
    - **Repository:** integration test against a real/containerized Postgres — the query returns the
      expected entities / summary service models.
    - **DataLayer:** unit test with a mocked `IOutbox` and `IRepository` — for a composed operation,
      that it calls the right repository methods **and enqueues the right event in the right order**,
      commits last, short-circuits correctly, and does **not** commit (or enqueue) when a leg throws;
      for a pass-through, that it delegates unchanged. A mocked transaction only proves commit/enqueue
      were *asked for*, so back any atomic composition with one **real-database** test that a
      mid-operation failure leaves **neither the domain row nor the outbox row** written.
    - **Business:** unit test with a mocked `IDataLayer` — mapping + list pass-through on reads, and
      on writes the VM→domain translation, the domain rule, and that it **builds the correct event**
      (and none when the change doesn't warrant one).
    - **Facade:** unit test with a mocked `IBusiness`, a real validator, an in-memory cache — cover a
      cache **hit**, a **miss**, and a **validation failure**.
    - **Outbox/Dispatcher:** unit test the dispatcher sends unprocessed rows, stamps them processed,
      leaves a failed send unstamped for retry, and sets `MessageId` from the row Id.
    - **Consumer** (if added): unit test that the event maps to the right facade call, and an
      **idempotency** test that a duplicate message ID applies the side effect once (inbox blocks the
      replay).
    - **Endpoint:** integration test (`WebApplicationFactory`) for the happy path plus one validation
      failure — asserting on ServiceModels, posting ViewModels; assert the outbox row was written on
      the happy path. Run `dotnet test` **in the owning service**; for bus-crossing work, run the
      consumer service's tests too.

16. **Migration (only if the model changed).** The `DbContext` lives in `.Core` but DI/config
    resolve in the host, so run from the **host** folder with both projects named:
    `dotnet ef migrations add <Name> --project ../JobBoard.<Service>.Core --startup-project . --context <Service>DbContext`,
    review, then the same with `database update`. Commit the migration and confirm
    `dotnet ef migrations has-pending-model-changes --project ../JobBoard.<Service>.Core --startup-project . --context <Service>DbContext`
    is clean. The `OutboxMessages` and `InboxMessages` tables (from the Shared base context) come in
    via the service's first migration.

## Reacting to another service (the two-service shape)

"When a job closes, close its open applications" is **not** a Jobs endpoint that writes Applications'
data. It's two pieces:
- **Jobs** already publishes `JobClosed` from its close-job endpoint — built in business, enqueued to
  Jobs' outbox in the write transaction, relayed by Jobs' dispatcher (steps 6–8, 11 done there).
- **Applications** adds a `JobClosedConsumer` (step 10) that its Service Bus processor host calls; the
  consumer dedupes on the message ID via its inbox and calls its **own** facade to close the affected
  applications in **its** database — same layers, same rules, same outbox if that in turn emits
  `ApplicationStatusChanged`.

Two databases, two services, one event over Service Bus between them, and no shared table. That's the
whole pattern.

## JobBoard domain notes
Core entities by service: **Jobs** — `Job` (title, description, location, salary band, status),
`Category`/`Tag`. **Applications** — `Application` (candidateId, jobId, status: Submitted → Reviewed
→ Offered/Rejected/Withdrawn, résumé ref). **Profiles** — `CandidateProfile`, `EmployerProfile`.
**Identity** — `Account`, roles (Employer/Candidate). Common routes: post/close a job, list/filter
jobs, apply/withdraw, advance an application's status, read a candidate's applications.

## Verify before trusting
Azure Service Bus and Aspire both move fast. Confirm the exact emulator surface —
`AddAzureServiceBus(...).RunAsEmulator(...)`, how topics/subscriptions are declared, the emulator's
entity-config JSON, and `AddAzureServiceBusClient` — against https://aspire.dev and the Service Bus
emulator docs before wiring. The outbox/inbox pattern is ours and stable; the transport binding is
the part that drifts.

## Checklist before done
- [ ] The change lives in **one** owning service; anything another service needs went out as a
      `Contracts` **event**, not a cross-database read
- [ ] Files live in the right **project**: controllers + consumers + `Program.cs` in the host;
      facade/business/data/repository + models/validators/mappers in `<Service>.Core`; outbox/inbox/
      dispatcher/processor/base-repo reused from `JobBoard.Shared` (not re-implemented per service)
- [ ] The host is thin — no facade→repository logic in a controller, consumer, or `Program.cs`;
      registration goes through `Add<Service>Core()` + the Shared extensions
- [ ] Reference direction holds: `Contracts` ← `Shared` ← `.Core` ← host; the host references only
      its own `.Core`, never another service's
- [ ] Only ViewModels enter and only ServiceModels leave the API; no EF entity crosses the controller
      boundary, and no Domain entity crosses the service boundary
- [ ] Controller/Consumer are thin entry points — no validation, cache, logic, or data access; the
      consumer dedupes via the **inbox** and is idempotent
- [ ] Facade owns validation + caching; no orchestration, mapping, EF, or bus
- [ ] Business translates VM→domain, applies domain rules, **builds the event**; it never serializes
      or sends it
- [ ] DataLayer composes repository calls **and the `IOutbox` write** into one atomic operation
      (pass-through where one call and no event suffice); no rules, mapping, cache, validation,
      `DbContext`, or Service Bus
- [ ] Any operation that writes-and-emits, or writes more than once, is wrapped in a transaction and
      commits the domain row(s) + outbox row together or not at all
- [ ] Concurrency is guarded in the write, not the read: a state transition uses a conditional
      `UPDATE ... WHERE Status = @expected` (0 rows → conflict, no event); a get-or-create catches the
      unique-constraint violation and maps it to a retryable 409 — neither relies on the loaded entity
- [ ] Repository does queries only against `<Service>DbContext`, one self-contained op per method; no
      outbox knowledge
- [ ] Each layer depends on the interface below it (`IFacade`→`IBusiness`→`IDataLayer`→`IRepository`),
      and the data layer's only non-feature dependency is `IOutbox` (same `DbContext`)
- [ ] Integration event (if any) is a small past-tense record with an `Id` in `JobBoard.Contracts`,
      no domain types
- [ ] Only the **OutboxDispatcher** sends to Service Bus; `MessageId` = the outbox row Id
- [ ] `DbContext`, cache, and Service Bus client obtained via Aspire integrations — no hardcoded
      strings/namespaces
- [ ] Client-facing endpoints have a gateway route (by Aspire resource name); internal-only ones
      deliberately don't
- [ ] Tests per layer pass, incl. facade cache hit/miss/validation, the data layer's
      order/short-circuit + a real-database row-and-outbox rollback test, dispatcher send/stamp,
      consumer inbox idempotency, and the endpoint integration test (`dotnet test` in the owning
      service; consumer service too if it crosses the bus)
- [ ] Migration reviewed, committed, `--context` correct, and `has-pending-model-changes` clean (if
      the model changed)
