# Tracing a Slice: Applying to a Job

*A new developer's walkthrough of ONE complete request, start to finish — every hop it takes, in the
real code that carries it, and the reasoning that put each piece where it is. Where
[Adding an Endpoint by Hand](./developer/adding-an-endpoint-manually.md) teaches the pattern generically (worked
from Jobs' post-a-job endpoint), this guide follows a single request as it actually happens: a candidate
clicks a button in the browser, and by the time the dust settles, three services and two databases have
each done their part without any of them calling each other directly.*

The slice: **a candidate applies to a job.** It's a good one to learn the architecture from because it
touches almost everything at once — a protected Angular action, the gateway's JWT check, the full
controller → facade → business → data layer → repository stack, a transactional outbox write, a Service
Bus fan-out to **two** independent subscribers, and an idempotent consumer on the other side. Read this
once, and every other endpoint in the repo is a variation on the same shape.

Every "why" claim below is traceable to a specific [ADR](./adr/README.md) — this guide doesn't invent
rationale, it explains the one the repo's decision records already committed to. Where the current code
knowingly falls short of its own target design, that's called out too, not smoothed over.

---

## The shape of the trip

```
Angular (job-detail)                                                     Postgres: applicationsdb
   │  candidate clicks "Apply now"                                             ▲
   ▼                                                                           │ same transaction
ApplicationService.submit()                                              ApplicationRepository.AddAsync
   │  POST /applications  (JWT on the Authorization header)                    ▲
   ▼                                                                           │
Gateway (YARP)  ── route "applications", AuthorizationPolicy: authenticated ── │
   │  proxies to the Aspire resource "applications"                     ApplicationDataLayer.SubmitAsync
   ▼                                                                     (ExecuteInTransactionAsync)
ApplicationsController.Submit                                                  ▲
   │                                                                           │ + IOutbox.EnqueueAsync
   ▼                                                                           │   (same tx, same commit)
ApplicationFacade.SubmitAsync  ── validates the ViewModel ──┐                  │
   │                                                        │                  │
   ▼                                                        │                  │
ApplicationBusiness.SubmitAsync                             │                  │
   │  builds the Application entity + the ApplicationSubmitted event          │
   └────────────────────────────────────────────────────────┴────────────────┘
                                                                    │
                                                                    ▼
                                                        OutboxMessages row committed
                                                        (Destination = "ApplicationSubmitted")
                                                                    │
                                                     OutboxDispatcher polls, relays, stamps ProcessedOnUtc
                                                                    │
                                                                    ▼
                                            Service Bus topic "ApplicationSubmitted" (the emulator)
                                                     ┌──────────────┴──────────────┐
                                                     ▼                             ▼
                                    sub "notifications-submitted"          sub "audit-submitted"
                                                     │                             │
                                                     ▼                             ▼
                                     JobBoard.Notifications                JobBoard.Audit
                                     ApplicationSubmittedConsumer          AuditConsumer<ApplicationSubmitted>
                                     → inbox check → NotificationLog row   → inbox check → auditdb row
                                     in notificationsdb
```

Two databases change (`applicationsdb`, then — asynchronously, independently — `notificationsdb` and
`auditdb`), and **no service ever calls another service's HTTP API or opens another service's database.**
The only thing that crosses the wire between them is the `ApplicationSubmitted` fact, and only the
outbox dispatcher ever puts it on the bus.

**Why is the trip shaped like this at all, rather than one service calling the next two directly?**
[ADR-0001](./adr/0001-microservices-database-per-service.md) draws the hardest boundary available —
database-per-service, no exceptions — specifically to keep JobBoard from becoming "a distributed
monolith with all the cost and none of the benefit." Once Applications physically cannot open
`notificationsdb`, a synchronous call would be the *only* other way to make Notifications react, and
[ADR-0002](./adr/0002-event-driven-integration-over-service-bus.md) rules that out too: a chain of
synchronous calls makes each service's availability depend on every service downstream of it, which
directly fights the system's top quality goal — *correctness under partial failure*. Everything below is
what those two decisions look like once you follow one request through them.

---

## 1. The browser: Angular submits the application

The candidate is on the job detail page. `src/web/src/app/features/job-detail/job-detail.ts` injects a
typed `ApplicationService` and a `Session` (the JWT-derived identity) and wires the button's click
handler:

```ts
protected apply(): void {
  const candidateId = this.session.userId();
  const job = this.job();
  if (!candidateId || !job || this.applying()) {
    return;
  }

  this.applying.set(true);
  this.applyError.set(null);

  this.applicationService
    .submit({ candidateId, jobId: job.id, resumeReference: null })
    .pipe(takeUntilDestroyed(this.destroyRef))
    .subscribe({
      next: (application) => {
        this.submitted.set(application);
        this.applying.set(false);
      },
      error: () => {
        this.applyError.set('Could not submit your application. Please try again.');
        this.applying.set(false);
      },
    });
}
```

The `applying()` guard at the top isn't defensive filler — without it, a fast double-click fires two
`POST`s before the first response returns and `applying` flips true. Signals make that cheap to guard:
`applying` and `applyError` are plain `WritableSignal`s the template reads directly, no
`async` pipe, no manual subscription bookkeeping (`takeUntilDestroyed` handles the one subscription this
component *does* own — the HTTP call itself — so leaving the page mid-request doesn't leak it).

The template (`job-detail.html`) only shows the button to a signed-in candidate on an open job, and
swaps to a confirmation once `submitted()` holds a value:

```html
<div class="apply">
  @if (submitted(); as application) {
    <p class="apply__done">
      ✓ Application submitted — <app-application-status [status]="application.status" />
    </p>
  } @else if (job.status !== JobStatus.Open) {
    <p class="apply__closed">This position is no longer accepting applications.</p>
  } @else if (isCandidate()) {
    <button type="button" class="btn btn--cta btn--block apply__btn"
            [disabled]="applying()" (click)="apply()">
      {{ applying() ? 'Submitting…' : 'Apply now' }}
    </button>
  } @else if (!isAuthenticated()) {
    <a class="btn btn--primary btn--block" routerLink="/login">Log in to apply</a>
  }
</div>
```

Notice the `job.status !== JobStatus.Open` branch — the UI hides the button on a closed job. **This is
the only place that rule is enforced.** Keep that in mind; it matters in step 3, where you'll see the
server has no matching check at all.

`ApplicationService` (`src/web/src/app/core/api/application.service.ts`) is a thin typed wrapper —
notice it never points at `applications` directly, only at the gateway's base URL:

```ts
@Injectable({ providedIn: 'root' })
export class ApplicationService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${inject(API_BASE_URL)}/applications`;

  /** POST /applications — submit an application. */
  submit(body: SubmitApplicationRequest): Observable<Application> {
    return this.http.post<Application>(this.baseUrl, body);
  }
}
```

That's deliberate, not incidental: [ADR-0006](./adr/0006-single-api-gateway-yarp.md) makes the gateway
the Angular app's *only* dependency. `API_BASE_URL` resolves to the gateway in every environment; there
is no per-service base URL anywhere in `src/web/` to accidentally point at instead. That's what lets the
five backend services be split, merged, or renamed without a single Angular file changing, as long as
the gateway's public routes hold.

`SubmitApplicationRequest` is a hand-kept TypeScript mirror of the C# ViewModel it will become on the
other side (`src/web/src/app/core/models/application.model.ts`):

```ts
/** Mirrors SubmitApplicationViewModel — the POST /applications request body. */
export interface SubmitApplicationRequest {
  candidateId: string;
  jobId: string;
  resumeReference: string | null;
}
```

That "hand-kept mirror" is a real cost, not a style choice made for free: [ADR-0010](./adr/0010-contracts-leaf-status-as-string.md)
keeps `JobBoard.Contracts` (the shared C# surface) a leaf with *no* generated client and no shared DTOs
crossing into TypeScript, precisely so the frontend can't couple to backend internals. The trade is that
nothing currently catches this interface drifting out of sync with `SubmitApplicationViewModel`
automatically — a rename on one side is a silent mismatch until a request fails at runtime.
[`api-contract-checker`](../.claude/agents/) exists as a subagent specifically to catch that class of
drift on review, because the type system can't.

An `HttpInterceptor` (not shown here) attaches the candidate's JWT as an `Authorization: Bearer …`
header to every outgoing request — that's what lets the gateway's next check pass.

---

## 2. The gateway: the only public door, and the JWT check

The request lands on `POST /applications` at the gateway. YARP matches it against the `applications`
route in `src/JobBoard.Gateway/appsettings.json`:

```json
"applications": {
  "ClusterId": "applications",
  "AuthorizationPolicy": "authenticated",
  "Match": { "Path": "/applications/{**catch-all}" }
}
```

```json
"applications": {
  "Destinations": {
    "applications": { "Address": "http://applications" }
  }
}
```

Two things happen here that matter. First, `"AuthorizationPolicy": "authenticated"` means the gateway
rejects the request with a `401` before it ever reaches the Applications service if the JWT is missing
or invalid — the service itself never re-validates it. [ADR-0007](./adr/0007-identity-issued-symmetric-jwt.md)
put the signing key only in Identity (which issues) and the gateway (which validates); every other
service trusts the gateway's gate rather than re-implementing JWT validation five times.

Second, the destination address, `http://applications`, is not a hostname anyone configured; it's the
**Aspire resource name** the Applications project was registered under in the AppHost, resolved by
service discovery. That's the one sanctioned literal-looking string in the whole system (see
[`.claude/rules/gateway.md`](../.claude/rules/gateway.md)) — everywhere else, a literal `host:port` is a
bug the "wire through Aspire" restriction in `CLAUDE.md` exists to prevent.

**Why put both concerns — routing *and* auth — in one gateway instead of exposing five services
directly?** [ADR-0006](./adr/0006-single-api-gateway-yarp.md) is explicit about the alternative it
rejected: exposing every service to the browser scatters auth, CORS, and rate-limiting across five hosts
and — worse — freezes the internal service boundaries into the public contract, so a service could never
be split or merged without breaking the SPA. One gateway means auth is enforced *once*, at the edge, and
the boundary the Angular app depends on (the gateway's routes) is decoupled from the boundary the
backend team is free to reshape (which services exist behind it).

The gateway also projects the validated token's claims into trusted internal headers before it proxies
the request onward — a YARP transform copying `sub`/`role` into `X-User-Id`/`X-User-Header`-style
headers, stripping any client-supplied copies first. That mechanism is
[ADR-0015](./adr/0015-gateway-identity-projection-header-mechanism.md), and it exists for one narrow
reason: the support audit trail ([ADR-0013](./adr/0013-correlation-causation-identifiers-on-events.md))
needs a trustworthy *actor* on every event, and the actor has to come from somewhere the client can't
forge. `IRequestContext` downstream in Applications (step 4) reads exactly these headers.

**This is not the same as authorization, and the repo is explicit about that gap.** ADR-0015 was
deliberately carved out of a larger, still-**Proposed** decision —
[ADR-0011](./adr/0011-token-derived-identity-propagation.md) — which would also *stop services trusting
client-supplied ids for who they're acting as*. ADR-0015 only ships the projection mechanism (so the
audit trail has an honest actor to attribute writes to); it does **not** yet require Applications to
*use* that projected identity as the candidate on a submission. Keep this in mind — it resurfaces
concretely in step 4.

---

## 3. JobBoard.Applications: controller → facade → business → data layer → repository

Before the code: why does a "submit an application" request pass through *five* named layers instead of
one method that does the work? [ADR-0005](./adr/0005-thin-host-core-layered-library.md) names the
failure modes this is built to prevent — fat controllers that accrete business logic over time, and
project references so tangled that any layer can reach any other, making "the rules live here" a matter
of convention instead of something the compiler enforces. Each layer below has exactly one job and is
physically unable to do the others' (the repository has no validator to call; the facade has no
`DbContext` to reach for); that's what "boundaries enforced by the compiler, not good intentions" means
in practice.

### ViewModel and validator — the inbound shape

`SubmitApplicationViewModel` (`src/JobBoard.Applications.Core/Managers/Models/ViewModels/`) is the only
shape the controller binds from the wire:

```csharp
public sealed record SubmitApplicationViewModel
{
    public Guid CandidateId { get; init; }
    public Guid JobId { get; init; }
    public string? ResumeReference { get; init; }
}
```

Its validator checks shape only — not domain rules:

```csharp
public sealed class SubmitApplicationViewModelValidator : AbstractValidator<SubmitApplicationViewModel>
{
    public SubmitApplicationViewModelValidator()
    {
        RuleFor(x => x.CandidateId).NotEmpty();
        RuleFor(x => x.JobId).NotEmpty();
        RuleFor(x => x.ResumeReference).MaximumLength(2048).When(x => x.ResumeReference is not null);
    }
}
```

**Notice what's *not* here, and why.** There's no `job.status !== Open` check to mirror the Angular
guard from step 1, and no explicit "candidate hasn't already applied" rule. Both are absent for the same
underlying reason: `.claude/rules/backend.md` draws the line at "shape/format only — required fields,
lengths, ranges" for a FluentValidation rule, because a validator has no database connection and can't
answer a data-dependent question truthfully at the moment it runs (a job could close, or a duplicate
application could land, in the gap between the check and the write — see the concurrency note two
sections down). The duplicate-application rule *does* get enforced, but as a database constraint caught
in the data layer, not a pre-check here — that's covered below. The **job-must-be-open** rule, notably,
isn't enforced anywhere server-side today; only the Angular button-hiding in step 1 stops a submission to
a closed job. A duplicate-tab or replayed request can still submit past a closed posting. That's a real
gap in this exact slice, not a design choice — worth knowing before you copy this pattern.

### Controller — bind, call the facade, return

`src/JobBoard.Applications/Controllers/ApplicationsController.cs`:

```csharp
[ApiController]
[Route("applications")]
public sealed class ApplicationsController : ControllerBase
{
    private readonly IApplicationFacade _facade;
    public ApplicationsController(IApplicationFacade facade) => _facade = facade;

    [HttpPost]
    public async Task<ActionResult<ApplicationDetailServiceModel>> Submit(
        [FromBody] SubmitApplicationViewModel viewModel,
        CancellationToken cancellationToken)
    {
        var application = await _facade.SubmitAsync(viewModel, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = application.Id }, application);
    }
}
```

No logic — bind, delegate, return. `.claude/rules/backend.md` states this as a hard rule, not a
style preference, because the controller is the one layer every request must pass through regardless of
which feature it's serving; if logic accumulates here, it accumulates somewhere no single layer owns.

### Facade — validate, then delegate

`ApplicationFacade.SubmitAsync` (`src/JobBoard.Applications.Core/Facade/ApplicationFacade.cs`):

```csharp
public async Task<ApplicationDetailServiceModel> SubmitAsync(
    SubmitApplicationViewModel viewModel,
    CancellationToken cancellationToken = default)
{
    await _submitValidator.ValidateAndThrowAsync(viewModel, cancellationToken);
    return await _business.SubmitAsync(viewModel, cancellationToken);
}
```

A failed `ValidateAndThrowAsync` throws `ValidationException`, which the shared exception handler maps
to a `400` with field detail — nobody writes that mapping by hand; `JobBoard.Shared`'s exception handler
owns the shared error shape once, for every service, so a client always gets the same error contract
regardless of which service answered.

The facade is also where **read-through caching** lives when a service has one — Jobs' `JobFacade`
caches its list read and invalidates on write (see [ADR-0009](./adr/0009-read-through-cache-generation-invalidation.md)
for the fail-open, generation-token pattern). Applications doesn't cache this particular read/write pair;
not every facade method needs it, only the ones sitting in front of a hot list read. The point of naming
"facade" as its own layer isn't "always cache here" — it's "if anything caches, it's only ever here,"
so a reader never has to check business or data-layer code to know whether a response might be stale.

### Business — translate, build the event, decide nothing more

`ApplicationBusiness.SubmitAsync` (`src/JobBoard.Applications.Core/Business/ApplicationBusiness.cs`):

```csharp
public async Task<ApplicationDetailServiceModel> SubmitAsync(
    SubmitApplicationViewModel viewModel,
    CancellationToken cancellationToken = default)
{
    var application = viewModel.ToEntity();
    var @event = application.ToApplicationSubmitted(_requestContext.RootThread());

    var saved = await _dataLayer.SubmitAsync(application, @event, cancellationToken);
    return saved.ToDetailServiceModel();
}
```

Two mappers do the real translation (`src/JobBoard.Applications.Core/Managers/Mappers/ApplicationMappers.cs`):

```csharp
public static Application ToEntity(this SubmitApplicationViewModel vm)
{
    var now = DateTime.UtcNow;
    return new Application
    {
        Id = Guid.NewGuid(),
        CandidateId = vm.CandidateId,
        JobId = vm.JobId,
        ResumeReference = vm.ResumeReference,
        Status = ApplicationStatus.Submitted,
        SubmittedOnUtc = now,
        StatusChangedOnUtc = now,
    };
}

public static ApplicationSubmitted ToApplicationSubmitted(this Application application, AuditThread thread) =>
    new(Guid.NewGuid(), application.Id, application.CandidateId, application.JobId, application.SubmittedOnUtc)
    {
        CorrelationId = thread.CorrelationId,
        CausationId = thread.CausationId,
        ActorId = thread.ActorId,
    };
```

**`ToEntity(this SubmitApplicationViewModel vm)` takes `vm.CandidateId` straight from the request body.**
This is the concrete edge of the gap flagged at the end of step 2. `_requestContext` (populated from the
gateway's trusted `X-User-Id` header per ADR-0015) is sitting right there in this same class, used one
line later for the *event's* `ActorId` — but the *domain write* still trusts the body-supplied
`CandidateId`, not the token-derived identity. In other words: the audit trail will correctly record
*who called the endpoint*, but the endpoint itself doesn't yet check that the caller and the candidate
being submitted-for are the same person. That's exactly the BOLA/IDOR seam
[ADR-0011](./adr/0011-token-derived-identity-propagation.md) is still **Proposed** to close — "remove
`CandidateId`/`EmployerId` from inbound ViewModels; derive the owning id from the propagated identity
instead." Until that ADR is accepted and implemented, an authenticated candidate can technically submit
an application *as* a different candidate id by editing the request body. Understanding why this line
looks the way it does — and why it's a known, tracked gap rather than an oversight — is arguably the
single most useful thing this slice can teach about how the codebase's ADRs and its code relate.

Setting that aside: `RootThread()` (`src/JobBoard.Shared/Requests/AuditThreadExtensions.cs`) is worth
reading once, because it's the one place the causation rule for the whole system is written down:

```csharp
/// The thread for a request-initiated event: correlation and actor come from the edge-populated
/// context, and causation is the request's own id — the correlation id — since the request itself is
/// the root cause (there is no parent event).
public static AuditThread RootThread(this IRequestContext context) =>
    new(context.CorrelationId, context.CorrelationId, context.ActorId);
```

For *this* event, `CausationId == CorrelationId`, because nothing else caused this submission — the
candidate's click is the root. Contrast that with a **follow-on** event built while *consuming* another
(e.g. if Applications ever emitted something in reaction to `JobClosed`): that uses
`FollowOnThread(this IIntegrationEvent cause)` instead, which sets `CausationId = cause.Id` while
carrying the *same* `CorrelationId` forward. That distinction is exactly what turns a flat list of events
into the causal tree the audit trail needs to answer "what triggered what," not just "what happened" —
see [ADR-0013](./adr/0013-correlation-causation-identifiers-on-events.md) for why a flat correlation
alone was rejected as insufficient.

Business **builds** the fact; it never sends it. `.claude/rules/backend.md` states this split plainly:
business decides *whether* a change warrants an event and constructs it; only the data layer enqueues
it, and only the outbox dispatcher ever transmits it. Collapsing "decide" and "send" into one step is
exactly what makes the dual-write bug ([ADR-0003](./adr/0003-hand-rolled-transactional-outbox.md),
below) possible in the first place — so the layers are split specifically to make that bug structurally
harder to write by accident.

### Data layer — one atomic transaction, domain row + outbox row together

`ApplicationDataLayer.SubmitAsync` (`src/JobBoard.Applications.Core/Data/ApplicationDataLayer.cs`) is
where the two facts — "an application now exists" and "an event describing it should ship" — become one
indivisible unit:

```csharp
public async Task<Application> SubmitAsync(
    Application application,
    ApplicationSubmitted @event,
    CancellationToken cancellationToken = default)
{
    try
    {
        return await _repository.ExecuteInTransactionAsync(
            async token =>
            {
                var saved = await _repository.AddAsync(application, token);
                // Same DbContext, same transaction: the event ships iff this row commits.
                await _outbox.EnqueueAsync(@event, token);
                return saved;
            },
            cancellationToken);
    }
    catch (DbUpdateException ex) when (ApplicationRepository.IsDuplicateApplicationViolation(ex))
    {
        throw new DomainException(
            "application.duplicate",
            $"Candidate '{application.CandidateId}' has already applied to job '{application.JobId}'.",
            StatusCodes.Status409Conflict);
    }
}
```

**Why does this need a transaction at all, instead of writing the application, then separately telling
Service Bus about it?** [ADR-0003](./adr/0003-hand-rolled-transactional-outbox.md) names the exact
failure this prevents: publishing an event is a *dual write* — one write to Postgres, a conceptually
separate write to a message broker — and the two systems don't share a commit. Write-then-send and crash
in between: the application exists but nobody downstream ever hears about it, silently. Send-then-write
and crash in between: Notifications and Audit react to an application that was never actually saved. The
outbox sidesteps both failure modes by making the *second* write a row in the *same* Postgres database,
inside the *same* transaction as the first — so from the broker's perspective there's no dual write at
all, just one commit and (later, separately) one relay.

**Why is the whole operation handed to `ExecuteInTransactionAsync` as a callback, rather than an
ordinary `BeginTransactionAsync`/`Commit`?** This is an Aspire-specific wrinkle, and it's worth knowing
because it explains a shape you'll see in every write-and-emit method in the codebase: Aspire's Npgsql
integration enables retry-on-failure, and that execution strategy **refuses to run inside a
caller-opened transaction** — and it may legitimately *replay* the whole callback on a transient fault.
Handing the entire unit of work in as a callback lets the two coexist. The consequence is that this
lambda must be safe to run more than once, which is exactly why `IOutbox.EnqueueAsync` keys on the
event's own `Id` and no-ops if that row is already staged — a replay re-stages the same row instead of
double-enqueuing it. If the mapper in step 3 built a *new* `Guid` on every call instead of a
deterministic one per logical event, this replay-safety guarantee would silently break.

**Why does the duplicate-application check live here — as a caught `DbUpdateException` — instead of a
`SELECT`-then-check before the insert?** This is the concurrency shape `docs/developer/adding-an-endpoint-manually.md`
calls "guard the write, not the read": two requests can both pass a `SELECT ... WHERE CandidateId = @c
AND JobId = @j` check before either one commits its `INSERT` — the check and the write aren't atomic
against each other unless the database itself is the referee. A unique index on `(CandidateId, JobId)`
makes Postgres the referee: the second insert fails at `SaveChanges`, inside this transaction, and the
`catch` maps that failure to a retryable `409` the client already knows how to handle. That's also
exactly why the classification lives in the data layer and not the repository (next section) — the
transaction that can actually observe and roll back the failure is opened here, not there.

### Repository — the query, and the classifier

`ApplicationRepository` (`src/JobBoard.Applications.Core/Data/ApplicationRepository.cs`):

```csharp
public async Task<Application> AddAsync(Application application, CancellationToken cancellationToken = default)
{
    await Context.Applications.AddAsync(application, cancellationToken);
    return application;
}

/// True when the exception is the unique-index violation on (CandidateId, JobId).
public static bool IsDuplicateApplicationViolation(DbUpdateException exception) =>
    exception.InnerException switch
    {
        PostgresException pg => pg.SqlState == PostgresErrorCodes.UniqueViolation
            && (pg.ConstraintName?.Contains("CandidateId", StringComparison.OrdinalIgnoreCase) ?? false),
        _ => false,
    };
```

The repository stages the write and can *classify* a constraint violation (it's the layer that knows
what a Postgres constraint name means), but it never *catches* one — the `try`/`catch` lives one layer up
because that's where the transaction it needs to roll back actually lives. Splitting "recognize this
error" from "decide what to do about it" this way is what lets the repository stay a pure EF-queries
layer with zero domain-exception vocabulary of its own.

### Wiring it together — DI and the composition root

`src/JobBoard.Applications.Core/DependencyInjection/ApplicationsCoreServiceCollectionExtensions.cs`
registers every layer, scoped, plus the validators from this assembly:

```csharp
public static IServiceCollection AddApplicationsCore(this IServiceCollection services)
{
    services.AddScoped<IApplicationRepository, ApplicationRepository>();
    services.AddScoped<IApplicationDataLayer, ApplicationDataLayer>();
    services.AddScoped<IApplicationBusiness, ApplicationBusiness>();
    services.AddScoped<IApplicationFacade, ApplicationFacade>();
    services.AddValidatorsFromAssemblyContaining<ApplicationsCoreMarker>();
    return services;
}
```

And `src/JobBoard.Applications/Program.cs` — the host's *entire* job — calls that plus the shared
cross-cutting extensions:

```csharp
builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<ApplicationsDbContext>("applicationsdb");   // Aspire — connection injected
builder.AddAzureServiceBusClient("servicebus");                       // Aspire

builder.Services.AddApplicationsCore();
builder.Services.AddSharedPersistence<ApplicationsDbContext>();       // IOutbox/IInbox + base repository
builder.Services.AddSharedMessaging<ApplicationsDbContext>();         // OutboxDispatcher + processor host
builder.Services.AddSharedExceptionHandler();
builder.Services.AddIntegrationEventConsumer<JobClosed, JobClosedConsumer>("applications-jobclosed");
builder.Services.AddSharedRequestContext();                           // reads the gateway's trusted headers

builder.Services.AddControllers();
```

Registration is deliberately split this way — `Add<Service>Core()` owned by the library, called once
from the host — so that "which layers exist" is defined next to the layers themselves, not duplicated or
drifted in a host file per service. No connection string, no broker address, no `localhost` anywhere;
`"applicationsdb"` and `"servicebus"` are Aspire resource names, resolved by the AppHost at run time —
the [Restrictions section of `CLAUDE.md`](../CLAUDE.md) calls a literal host:port here a violation on
sight, not a style nit, because it's the thing that would make this code behave differently in a
teammate's environment than in yours.

---

## 4. The integration event — the only thing that crosses the boundary

`src/JobBoard.Contracts/ApplicationSubmitted.cs` — a past-tense fact, small, no domain types:

```csharp
public sealed record ApplicationSubmitted(
    Guid Id,
    Guid ApplicationId,
    Guid CandidateId,
    Guid JobId,
    DateTime SubmittedOnUtc) : IIntegrationEvent
{
    public Guid CorrelationId { get; init; }
    public Guid CausationId { get; init; }
    public Guid? ActorId { get; init; }
}
```

`Id` is stamped fresh by the business layer — it's both the `OutboxMessages` row's primary key and the
Service Bus `MessageId` every consumer dedupes on. This is the entire contract between Applications and
everyone downstream — Notifications and Audit both react to exactly this shape and nothing more; neither
knows Applications' `Application` entity exists.

**Why so little on it?** [ADR-0010](./adr/0010-contracts-leaf-status-as-string.md) treats
`JobBoard.Contracts` as a strict leaf specifically so it can't become a coupling magnet: it holds *only*
event records, no entities, no ServiceModels, no shared enums. Notice `ApplicationSubmitted` carries
`CandidateId`/`JobId` as bare `Guid`s and *not* a `Status`, but other events in this codebase (like
`ApplicationStatusChanged`) that do carry a status cross it as a **string**, each consumer mapping it to
its own local enum — deliberately, so that Applications' internal `ApplicationStatus` enum can be
refactored freely without silently breaking Notifications or Audit at compile time (the trade being that
a typo or renamed value becomes a *runtime* mismatch instead of a build error, which is why
`api-contract-checker` exists as a review gate rather than relying on the compiler alone).

Also worth noticing: this record carries **only** what a consumer needs — no `ResumeReference`, even
though the domain `Application` entity has one. That's the "minimum denormalized data to avoid a
call-back" rule from [ADR-0002](./adr/0002-event-driven-integration-over-service-bus.md) — enough for
Notifications to compose a message and Audit to record a fact, without either of them needing to ask
Applications "and what was in the résumé field?" after the fact.

---

## 5. The outbox dispatcher — the only thing that talks to Service Bus

Nobody in the request path above called Service Bus directly. A background service does that, on its
own clock, from `JobBoard.Shared`. `OutboxDispatcher` (`src/JobBoard.Shared/Messaging/OutboxDispatcher.cs`)
is a `BackgroundService` that ticks on a timer and hands the real work to `OutboxRelay`:

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    using var timer = new PeriodicTimer(_options.PollInterval);
    do
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<BaseDbContext>();
            await _relay.RelayAsync(context, stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Outbox relay poll failed; retrying on the next tick.");
        }
    }
    while (await timer.WaitForNextTickAsync(stoppingToken));
}
```

`OutboxRelay.RelayAsync` (`src/JobBoard.Shared/Messaging/OutboxRelay.cs`) does the actual send-and-stamp,
oldest row first:

```csharp
public async Task RelayAsync(BaseDbContext context, CancellationToken cancellationToken = default)
{
    var pending = await context.OutboxMessages
        .Where(m => m.ProcessedOnUtc == null)
        .OrderBy(m => m.OccurredOnUtc)
        .Take(_options.BatchSize)
        .ToListAsync(cancellationToken);

    foreach (var row in pending)
    {
        var sender = _senders.GetOrAdd(row.Destination, _client.CreateSender);
        var message = new ServiceBusMessage(row.Payload)
        {
            MessageId = row.Id.ToString(),   // dedupe key on every consumer's inbox
            Subject = row.Type,
        };
        await sender.SendMessageAsync(message, cancellationToken);
        row.ProcessedOnUtc = DateTime.UtcNow;
    }

    await context.SaveChangesAsync(cancellationToken);
}
```

`row.Destination` is `"ApplicationSubmitted"` — the event-type name, which doubles as the Service Bus
topic name by convention. **Why is "send, then stamp" — rather than "stamp, then send" — the order?**
Because ADR-0003 explicitly accepts **at-least-once**, never at-most-once, delivery: if the process dies
after `SendMessageAsync` succeeds but before `row.ProcessedOnUtc` commits, the same row goes out again
next tick with the *same* `MessageId`. That's a deliberate trade — the alternative (stamp first) risks
the opposite failure, a message that's marked sent but never actually left the process, which is a
silently lost event and strictly worse than a harmless duplicate. "Fine, because every consumer dedupes"
is not a hand-wave here — it's the reason ADR-0004 (the inbox, step 7) exists at all.

Also notice `catch (Exception ex) when (ex is not OperationCanceledException)` inside the *inner* try in
`RelayAsync`'s caller: a poll failure — say, Postgres is briefly unreachable — logs and lets the next
tick retry, rather than tearing the `BackgroundService` down. An unstamped row just gets picked up again;
nothing is lost by a transient failure here, only delayed.

**A known limitation worth carrying forward, not discovering the hard way:** this "poll unprocessed rows
oldest-first" relay assumes a single dispatcher instance per service. ADR-0003 flags this explicitly —
running two replicas of Applications would let both dispatchers see the same unprocessed row and
double-send it (still safe, because of the inbox, but wasteful and not the intended steady state).
Multi-instance requires a claim mechanism (`FOR UPDATE SKIP LOCKED` or a leased batch) that doesn't exist
yet; it's a tracked 90-day item, not an oversight in this code.

This code is generic — the same dispatcher instance, unmodified, relays events for every service in the
system. Applications never wrote a line of Service Bus plumbing; `AddSharedMessaging<ApplicationsDbContext>()`
in `Program.cs` (step 3) is what registered it. That reuse is the entire point of putting the mechanism
in `JobBoard.Shared` once ([ADR-0005](./adr/0005-thin-host-core-layered-library.md)) instead of each
service hand-rolling its own outbox loop.

---

## 6. The AppHost — where the topic and its two subscriptions actually exist

None of the above works unless the topic and its subscriptions are declared somewhere. That's
`src/JobBoard.AppHost/AppHost.cs`, the one file that knows about every resource in the system:

```csharp
// Events Applications publishes. Topic name = event-type name (the outbox Destination convention).
var applicationSubmittedTopic = serviceBus.AddServiceBusTopic("ApplicationSubmitted");
applicationSubmittedTopic.AddServiceBusSubscription("notifications-submitted");
applicationSubmittedTopic.AddServiceBusSubscription("audit-submitted");
```

Two subscriptions on one topic is the whole story of how *both* Notifications and Audit react to the
same publish without Applications knowing either exists — Service Bus fans the message out, once per
subscription, and each service's processor host reads its own subscription independently. This is
exactly the "new reactions attach as new subscribers without touching publishers" benefit
[ADR-0002](./adr/0002-event-driven-integration-over-service-bus.md) names: Audit was added to this system
well after Applications and Notifications existed, and adding it required exactly one new line here plus
one new consumer class in `JobBoard.Audit` — zero changes to `ApplicationBusiness`, `ApplicationDataLayer`,
or anything else in the publishing service.

**Why `"notifications-submitted"` and not something shorter, like `"applications"`?** Aspire resource
names — project names, database names, topic names, *and* subscription names — all share **one global
namespace** in the model, not one namespace per topic. A subscription literally can't be named
`"applications"`, because that string is already claimed by the Applications project resource a few
lines above. Naming it `notifications-submitted` (service + event, abbreviated) sidesteps that collision
and also happens to make a subscription self-describing when you're staring at the Service Bus emulator
UI wondering who reads what. This constraint is easy to trip over — the exact bug it causes is a
same-named subscription silently resolving to the wrong resource — so it's worth internalizing here
rather than after the fact.

The Applications project itself, and its database, are wired the same declarative way:

```csharp
var applicationsDb = postgres.AddDatabase("applicationsdb");
...
var applications = builder.AddProject<Projects.JobBoard_Applications>("applications")
    .WithReference(applicationsDb)
    .WithReference(serviceBus)
    .WaitFor(applicationsDb)
    .WaitFor(serviceBus);   // runs the outbox dispatcher + processor host, so it uses the bus
```

`"applications"` here is the Aspire resource name — the same string the gateway's YARP cluster targets
as `http://applications` in step 2. There's exactly one place in the whole codebase that string is
*defined* (here); everywhere else, it's *resolved*. That's the practical meaning of "wire through
Aspire, never hardcode": every `WithReference` is Aspire injecting the *current run's* actual connection
info (a Postgres port, a Service Bus emulator address) into the dependent service at startup, so the
same code runs unmodified on your machine, a teammate's machine, and CI, each with different literal
addresses underneath.

---

## 7. JobBoard.Notifications: the first subscriber

The processor host (shared, from `JobBoard.Shared`, wired by `AddSharedMessaging`) reads the
`notifications-submitted` subscription and, for each message, resolves and calls the registered
consumer. `ApplicationSubmittedConsumer` (`src/JobBoard.Notifications/Consumers/ApplicationSubmittedConsumer.cs`)
is deliberately thin:

```csharp
public sealed class ApplicationSubmittedConsumer : IIntegrationEventConsumer<ApplicationSubmitted>
{
    private readonly INotificationFacade _facade;
    public ApplicationSubmittedConsumer(INotificationFacade facade) => _facade = facade;

    public Task ConsumeAsync(ApplicationSubmitted @event, CancellationToken cancellationToken = default) =>
        _facade.HandleApplicationSubmittedAsync(@event, cancellationToken);
}
```

Same reasoning as the controller in step 3: a consumer is just a different kind of entry point, so it
gets the same "bind and delegate, no logic" treatment — here "binding" means receiving a strongly-typed
event instead of an HTTP body.

The facade hands straight to business, which hands to the data layer — the same
facade→business→data→repository shape as the publish side, just entered by a consumer instead of a
controller. The idempotency guarantee lives in `NotificationDataLayer.RecordAsync`
(`src/JobBoard.Notifications.Core/Data/NotificationDataLayer.cs`):

```csharp
public Task RecordAsync(NotificationLog log, Guid messageId, CancellationToken cancellationToken = default) =>
    _repository.ExecuteInTransactionAsync(
        async token =>
        {
            // Idempotency: a redelivery of the same message finds its inbox row and no-ops.
            if (await _inbox.HasProcessedAsync(messageId, token))
            {
                return;
            }

            await _repository.AddAsync(log, token);
            await _inbox.MarkProcessedAsync(messageId, token);
        },
        cancellationToken);
```

**Why does this need its own transaction, mirroring the one on the publish side?** Because
[ADR-0004](./adr/0004-idempotent-inbox-at-least-once-delivery.md) starts from a hard fact: *every*
consumer in this system **will**, eventually, see some message twice — that's not a rare edge case,
it's the direct, accepted consequence of ADR-0003's at-least-once delivery (step 5) plus whatever
redelivery Service Bus itself does on a lock expiry or a mid-processing crash. Chasing "exactly-once"
delivery at the transport level is, per the ADR, "a known trap" — the tractable fix is making the
*consumer* safe to run twice instead. The inbox check and the `NotificationLog` insert happen in one
transaction for the same reason the outbox write and the domain write did in step 3: if they were two
separate transactions, a crash between them reopens exactly the double-effect window the inbox exists to
close — a check-then-act race, just moved to the other end of the pipe. `messageId` here is `@event.Id`
— the exact same guid the outbox stamped as the Service Bus `MessageId` in step 5, so a resend and the
original are provably the same logical fact, not just similar-looking ones.

There's also a setting you won't see printed in this file that makes the whole scheme trustworthy: the
shared `ServiceBusProcessorHost` runs with `AutoCompleteMessages = false`, and only completes (settles)
a message *after* the consumer succeeds. A throw during `ConsumeAsync` — a transient DB error, say —
leaves the message unsettled, so Service Bus redelivers it. That redelivery is exactly what the inbox
check above makes safe on a *successful* replay; on a *failing* replay (a message that always throws),
ADR-0004 is candid that it currently redelivers indefinitely under the default policy — an explicit
dead-letter policy for poison messages is a tracked gap, not yet built.

What actually gets recorded is composed by a mapper
(`src/JobBoard.Notifications.Core/Managers/Mappers/NotificationMappers.cs`):

```csharp
public static NotificationLog ToNotificationLog(this ApplicationSubmitted @event) => new()
{
    Id = Guid.NewGuid(),
    RecipientId = @event.CandidateId,
    Kind = nameof(ApplicationSubmitted),
    Subject = "Application received",
    Body = $"Your application to job {@event.JobId} was received on {@event.SubmittedOnUtc:u}.",
    CreatedOnUtc = DateTime.UtcNow,
};
```

**Be aware this is a stand-in, not a real mailer.** Notifications has no SMTP/SendGrid integration
anywhere — "sending the email" today means writing a `NotificationLog` row to `notificationsdb`
describing the message that *would* be sent. That's an honest placeholder for a demo stack, not a gap to
paper over; if you're extending this slice, the seam to add a real provider is inside
`NotificationBusiness`/`NotificationDataLayer`, after the inbox check — an actual send would need its own
failure handling (what happens if the provider is down but the row already committed?), which is exactly
why it hasn't been bolted on casually.

`Program.cs` registers the consumer against the exact subscription name the AppHost declared — this
string match is the whole binding, so a typo here silently drops the service's ability to receive the
event, with no compiler error to catch it (this is the same "resource names live in one shared,
stringly-typed namespace" sharp edge from step 6, arriving from the consumer side this time):

```csharp
builder.Services.AddIntegrationEventConsumer<ApplicationSubmitted, ApplicationSubmittedConsumer>("notifications-submitted");
```

Notifications has no controllers and no gateway route — `Program.cs` calls `app.MapDefaultEndpoints()`
only. It's event-only by design; there's nothing for a browser to call, and per
[ADR-0006](./adr/0006-single-api-gateway-yarp.md) "a service endpoint with no gateway route is
unreachable by design" — for Notifications, *every* endpoint is like that, which is exactly correct for
a service whose whole job is reacting to events, never fielding requests.

---

## 8. The second subscriber: JobBoard.Audit

The same publish also lands on the `audit-submitted` subscription. `JobBoard.Audit` uses one generic
consumer type for every event it records (`src/JobBoard.Audit/Consumers/AuditConsumer.cs`):

```csharp
public sealed class AuditConsumer<TEvent> : IIntegrationEventConsumer<TEvent>
    where TEvent : IIntegrationEvent
{
    private readonly IAuditFacade _facade;
    public AuditConsumer(IAuditFacade facade) => _facade = facade;

    public Task ConsumeAsync(TEvent @event, CancellationToken cancellationToken = default) =>
        _facade.RecordAsync(@event, cancellationToken);
}
```

registered once per event type it cares about:

```csharp
builder.Services.AddIntegrationEventConsumer<ApplicationSubmitted, AuditConsumer<ApplicationSubmitted>>("audit-submitted");
```

**Why is this one consumer type generic over `TEvent`, instead of a per-event class like Notifications
uses?** Because every event Audit reacts to gets treated identically — append one immutable row, keyed on
`(CorrelationId, CausationId, ActorId, Id, event-type name)` — so a bespoke `ApplicationSubmittedAuditConsumer`
class would be indistinguishable from `JobPostedAuditConsumer` except for the type parameter. Generics let
the AppHost's growing list of `audit-*` subscriptions ([ADR-0014](./adr/0014-audit-bounded-context-bus-fed-support-trail.md))
each bind to the same class with a different `TEvent`, instead of a new near-duplicate file per event —
notice this is the opposite trade from Notifications, where each event's *email content* genuinely
differs (step 7's `NotificationMappers`), so a shared consumer there would just push the differentiation
into an `if`/`switch` the type system could otherwise express for free.

Same idempotent-inbox shape as Notifications, appending one immutable row to `auditdb`. This is *why*
step 3 bothered stamping `CorrelationId`/`CausationId`/`ActorId` onto the event at all — those fields are
what let a support engineer later reconstruct this exact submission alongside everything else that
happened in the same request (see the [`trace-a-request`](../.claude/skills/trace-a-request/) skill).
And this is the other half of the honest caveat from step 3: the `ActorId` this row records is
trustworthy *as an attribution* — it genuinely came from the validated JWT via the gateway's projected
header, per ADR-0015 — but that's a narrower guarantee than "this candidate was authorized to submit
this exact application." The audit trail can tell you *who the gateway believes made this call*; it
cannot yet tell you whether the `CandidateId` inside the payload actually matched that caller, because
nothing upstream checks that today (step 3, again).

---

## What to notice, now that you've seen the whole trip

- **Nothing synchronous crosses a service boundary**, and that's not a style preference —
  [ADR-0001](./adr/0001-microservices-database-per-service.md) makes it physically impossible (no second
  connection string exists) and [ADR-0002](./adr/0002-event-driven-integration-over-service-bus.md)
  makes it the deliberate alternative to a call chain whose availability multiplies failures instead of
  isolating them.
- **The domain write and the outbox write are one transaction**, because the alternative is the
  dual-write bug ADR-0003 exists specifically to name and prevent — a system where "the application
  saved" and "the notification fired" can disagree with each other after a crash.
- **Every consumer assumes redelivery**, because ADR-0003's at-least-once send is a deliberate,
  documented trade (better a harmless duplicate than a silently lost event), and ADR-0004's inbox is
  what makes that trade survivable rather than a source of duplicate emails and double-counted audit
  rows.
- **Every string that looks like an address is a name, not a location.** `"applicationsdb"`,
  `"servicebus"`, `"applications"`, `"http://applications"` — all Aspire resource names, all defined once
  in the AppHost (step 6) and resolved everywhere else, so the same code runs correctly on any machine
  without edits.
- **`Contracts` stays almost empty on purpose** ([ADR-0010](./adr/0010-contracts-leaf-status-as-string.md)) —
  every field on `ApplicationSubmitted` is there because a specific consumer needs it, not because it was
  convenient to include.
- **The identity story has an honest, tracked seam, not a silent one.** ADR-0015 (Accepted) gets a
  trustworthy actor onto every event for *attribution*; ADR-0011 (still Proposed) is the separate,
  larger fix that would stop the domain write itself from trusting a body-supplied `CandidateId`. Step 3
  and step 8 above are the two ends of that same gap — worth understanding precisely because it's the
  kind of thing that's easy to copy into a new endpoint without noticing it's there.
- **Three services, three totally different shapes of entry point** — a public controller
  (Applications), an event-only consumer with no HTTP surface at all (Notifications), and a generic
  consumer parameterized over the event type (Audit) — and all three still follow the same
  facade→business→data→repository layering underneath ([ADR-0005](./adr/0005-thin-host-core-layered-library.md)).
  Once you've internalized this slice, that layering is the only thing you need to carry into a new one.

## Building your own slice

Ready to add a new endpoint of your own? [Adding an Endpoint by Hand](./developer/adding-an-endpoint-manually.md)
is the generic, step-by-step version of everything this guide just walked through concretely — same
layers, same build order, worked from Jobs' post-a-job endpoint instead. Use this guide to know *why*
each step exists; use that one as the checklist while you build. And if you find yourself writing
`vm.CandidateId` (or any other actor-shaped field) straight into a domain entity the way step 3 does,
that's your cue to go re-read [ADR-0011](./adr/0011-token-derived-identity-propagation.md) before you do
— it's the same gap, and it's still open.
