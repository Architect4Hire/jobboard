# JobBoard — SCRUB Prompts

Prompts for driving Claude Code on JobBoard (Aspire + ASP.NET Core microservices + Angular), all
wired to the skills, rules, and subagents in your `.claude/` folder.

Two parts:

- **Part 1 — Scaffolding:** a one-time sequence, run in order, to stand the system up. Because this is a _microservice_ app, the sequence stands up the shared spine first, proves **one** service and the **whole messaging loop** end to end, then fans the proven pattern out to the rest.
- **Part 2 — Operational templates:** reusable prompts for the recurring, high-stakes moments the agent won't self-guard (features that cross the bus, migrations, refactors, debugging). Fill in the blanks and go.

Once the system is scaffolded, most day-to-day work needs no bespoke prompt — your rules, skills, subagents, and hooks carry the structure, so short instructions are enough. Reach for Part 2 only when a task is non-trivial or risky. And when you reuse one of these two or three times, promote it to a skill so you stop needing the prompt at all.

## The reusable SCRUB skeleton

```
SCOPE:        what to build/change + which service/project it touches
CONSTRAINT:   the rules to honor (stack, conventions, plan-first)
RESTRICTION:  explicit "do NOT" guardrails
USAGE:        which skills / subagents / tools to use
BEHAVIOR:     how to proceed — plan, approve, small steps, test, report
```

## How to use these

- Run the Part 1 prompts **in order**, one at a time. Don't paste the whole file at once.
- Each assumes `CLAUDE.md` (repo root) and the `.claude/` folder — rules (`aspire.md`, `backend.md`, `frontend.md`, `messaging.md`, `gateway.md`), skills (`add-endpoint`, `add-component`, `add-aspire-resource`), and subagents (`code-reviewer`, `test-gap-analyzer`, `api-contract-checker`) — are already in place.
- Every prompt asks Claude to **plan first and wait for approval** before editing — read the plan before you say go; that's the biggest quality lever. For microservices it's also where you catch a boundary being drawn in the wrong place, before any code exists.
- Use `/clear` between big steps to keep context lean; rules and skills reload on their own.
- **The golden rule of the sequence:** don't fan out to five services until _one_ service and the full event loop work. Prompts 1–5 build the template; Prompt 6 replicates it.

---

# Part 1 — Scaffolding (run once, in order)

## Prompt 0 — Solution skeleton + shared spine

```
SCOPE: Stand up the JobBoard solution skeleton only (no domain, no endpoints). Create an Aspire 13
solution on .NET 10 with these projects under src/: JobBoard.AppHost (orchestrator),
JobBoard.ServiceDefaults, JobBoard.Contracts (class library — empty but for an IIntegrationEvent
marker with a Guid Id), JobBoard.Shared (class library — empty shell for cross-cutting code), and
JobBoard.Gateway (a YARP reverse proxy, no routes yet). In the AppHost declare a local PostgreSQL
server resource and register the gateway; register the Angular app in src/web as a JavaScript app so
Aspire launches it. Set the project reference direction Contracts <- Shared, and have the gateway and
ServiceDefaults reference nothing app-specific.

CONSTRAINT: Follow .claude/rules/aspire.md and .claude/rules/gateway.md. All resources are local
containers. The repo root already has Directory.Build.props, Directory.Packages.props (central
package management), global.json, and .editorconfig — new projects inherit them, so do NOT set
TargetFramework/Nullable per project and do NOT put versions on PackageReferences; add each
<PackageVersion> to Directory.Packages.props instead, pinned to what you install. Verify the exact
Aspire commands, template names, and API (AddJavaScriptApp, YARP wiring, package names) against
https://aspire.dev before running anything — do not guess.

RESTRICTION: Do NOT add any service host, DbContext, domain model, endpoint, or messaging yet. Do NOT
hardcode any connection string or localhost:port. Do NOT add real cloud/Azure resources. Do NOT let
Contracts reference anything, or Shared reference Contracts' consumers.

USAGE: Use the aspire CLI/templates; use the aspireify skill if available. Use plan mode.

BEHAVIOR: First show me the plan: the exact projects, their references, the AppHost wiring, and the
commands you'll run. Wait for my approval. Then scaffold, run `aspire run`, and tell me what the
dashboard shows (Postgres + gateway + web, all healthy).
```

## Prompt 1 — The shared spine: base persistence + outbox/inbox (JobBoard.Shared)

```
SCOPE: Fill in JobBoard.Shared with the cross-cutting persistence + messaging MECHANISM every
service will reuse: a base DbContext exposing OutboxMessages and InboxMessages DbSets (+ EF config);
a base repository exposing ExecuteInTransactionAsync built for the Aspire Npgsql execution strategy
(callback form, retry-safe); IOutbox (serialize an IIntegrationEvent to an OutboxMessages row on the
same DbContext) and IInbox (record/check a handled message id); a global exception handler mapping
ValidationException -> 400 and the domain exception -> 4xx via a shared error shape; a cache
abstraction; and the IIntegrationEventConsumer<TEvent> interface. Provide AddSharedPersistence() and
AddSharedExceptionHandler() registration extensions. No Service Bus yet — just the DB-side spine.

CONSTRAINT: Follow .claude/rules/backend.md and .claude/rules/messaging.md. Match the outbox/inbox
contract described in the add-endpoint skill exactly (deterministic event Id, at-least-once, same
transaction as the write).

RESTRICTION: Shared holds MECHANISM only — no service's domain, business, ViewModels, or
ServiceModels. Do NOT talk to Service Bus here yet. Do NOT open user-initiated transactions
(ExecuteInTransactionAsync must accept the whole operation as a callback). No hardcoded config.

USAGE: Use plan mode. Delegate the final review to the code-reviewer subagent.

BEHAVIOR: Plan the types and the transaction/outbox contract and show me the interfaces before
writing. Wait for approval. Implement with unit tests for IOutbox (writes a row) and the base repo
(rolls back on a mid-operation throw), run `dotnet test`, run the code-reviewer, and summarize.
```

## Prompt 2 — Service Bus + the relay: dispatcher + processor host (JobBoard.Shared)

```
SCOPE: Add the send/receive relay to JobBoard.Shared: an OutboxDispatcher BackgroundService that
polls unprocessed OutboxMessages oldest-first, sends each to Azure Service Bus (ServiceBusMessage
MessageId = row Id, Subject = event-type name), then stamps ProcessedOnUtc; and a Service Bus
processor host BackgroundService that receives messages and dispatches them to the registered
IIntegrationEventConsumer<TEvent> handlers, deduping via IInbox. Provide AddSharedMessaging<TDbContext>().
In the AppHost, add the Azure Service Bus resource running as a LOCAL EMULATOR, and reference it from
the gateway placeholder only for now.

CONSTRAINT: Follow .claude/rules/aspire.md and .claude/rules/messaging.md. The Service Bus client
comes from the Aspire integration (AddAzureServiceBusClient); the emulator is a local container.
Verify AddAzureServiceBus(...).RunAsEmulator(...), the emulator entity-config format, and
AddAzureServiceBusClient against https://aspire.dev and the Service Bus emulator docs before wiring.

RESTRICTION: The dispatcher is the ONLY thing that sends to Service Bus. Consumers MUST be idempotent
via the inbox. Do NOT hardcode a namespace or connection string. Do NOT add real Azure resources — the
emulator only.

USAGE: Use the add-aspire-resource skill for the Service Bus resource. Use plan mode. Delegate
review to the code-reviewer subagent.

BEHAVIOR: Plan the dispatcher loop, the processor dispatch + dedup path, and the emulator wiring, and
show me the topic/subscription model before writing. Wait for approval. Implement with unit tests
(dispatcher sends+stamps, leaves failed sends for retry; processor applies once on duplicate id), run
`dotnet test`, run the code-reviewer, and summarize.
```

## Prompt 3 — The template service: Jobs host + Jobs.Core (wired, no domain)

```
SCOPE: Stand up the FIRST service as the template for the rest: JobBoard.Jobs (thin ASP.NET Core Web
API host — entry points + composition root) and JobBoard.Jobs.Core (class library — the facade ->
business -> data -> repository home). Create JobsDbContext in Jobs.Core deriving from the Shared base
context (so it inherits Outbox/Inbox sets). Wire the host: AddServiceDefaults, AddJobsCore()
(empty for now), the Aspire Npgsql integration keyed to a "jobsdb" database, AddAzureServiceBusClient,
AddSharedPersistence/AddSharedMessaging<JobsDbContext>/AddSharedExceptionHandler. In the AppHost add
jobsdb to the Postgres server, register the Jobs host with WithReference to jobsdb + Service Bus, and
WaitFor the database.

CONSTRAINT: Follow .claude/rules/backend.md, .claude/rules/aspire.md, .claude/rules/messaging.md.
References: Jobs.Core -> Shared + Contracts; Jobs -> Jobs.Core + ServiceDefaults. Host stays thin.

RESTRICTION: Do NOT add any Job domain model or endpoint yet — this step proves the two-project wiring
and the DbContext only. Do NOT put logic in the host. No hardcoded connection strings; no gateway
route yet.

USAGE: Use plan mode. Delegate review to the code-reviewer subagent.

BEHAVIOR: Plan the two projects, their references, and the exact host registration lines, and show me
before writing. Wait for approval. Build, run `aspire run`, confirm Jobs shows healthy in the
dashboard with jobsdb connected, and report.
```

## Prompt 4 — Jobs domain + migration (Jobs.Core)

```
SCOPE: Add the Jobs domain to JobBoard.Jobs.Core: entities Job (title, description, location, salary
band, status), Category and Tag with sensible relationships (a Job has many Categories/Tags). Put them
under Managers/Models/Domain. Create the initial migration for JobsDbContext (which also brings in the
Outbox/Inbox tables from the Shared base context).

CONSTRAINT: Follow .claude/rules/backend.md and .claude/rules/aspire.md. The DbContext lives in
Jobs.Core; the host is the startup project for EF.

RESTRICTION: Do NOT apply the migration yet — create it and stop for review. Do NOT add endpoints in
this step. Do NOT hand-edit the generated migration except to read it.

USAGE: Use plan mode.

BEHAVIOR: Plan the entities/relationships and show me the model before writing. Wait for approval.
Generate the migration with:
  dotnet ef migrations add InitialJobs --project ../JobBoard.Jobs.Core --startup-project . --context JobsDbContext
show me the generated file, and stop before any database update.
```

## Prompt 5 — Prove the loop: Jobs endpoints + JobClosed event, end to end

```
SCOPE: Implement the first Jobs endpoints AND the first event, to prove the whole vertical + the bus
in one slice. Endpoints in Jobs.Core/host: list jobs (filter by category), get one job, post a job,
close a job. Closing a job must publish a JobClosed integration event through the outbox (in the same
transaction as the status write). Add JobClosed to JobBoard.Contracts. Add the gateway routes for the
client-facing Jobs endpoints.

CONSTRAINT: Follow .claude/rules/backend.md, .claude/rules/messaging.md, .claude/rules/gateway.md.

RESTRICTION: Only ViewModels in, only ServiceModels out — no EF entity crosses the controller, no
domain type crosses the service boundary. Business builds the event; the data layer enqueues it
atomically; only the dispatcher sends it. Do NOT touch other services or the UI.

USAGE: Use the add-endpoint skill for every route (it covers the outbox + gateway steps).
Delegate review to the code-reviewer subagent and the api-contract-checker.

BEHAVIOR: Plan the endpoints, the JobClosed contract, and where the publish is enqueued, and wait for
approval. Implement, apply the pending migration when I approve, run `dotnet test` green, then
`aspire run` and show me a JobClosed message flowing (outbox row -> dashboard/bus). Run the reviewer +
api-contract-checker and summarize.
```

## Prompt 6 — Close the loop: Applications service consumes JobClosed

```
SCOPE: Scaffold the SECOND service using the Jobs template (JobBoard.Applications + .Core, wired like
Prompt 3, applicationsdb), add its Application domain + migration (candidateId, jobId, status:
Submitted/Reviewed/Offered/Rejected/Withdrawn) and its submit/withdraw/advance-status endpoints, THEN
add a JobClosedConsumer that reacts to Jobs' event by closing the affected open applications in its
OWN database — deduped via the inbox. Submitting/advancing publishes ApplicationSubmitted /
ApplicationStatusChanged.

CONSTRAINT: Follow .claude/rules/backend.md, .claude/rules/messaging.md, .claude/rules/gateway.md.
This is the canonical two-service shape: one event, two databases, no shared table.

RESTRICTION: The consumer writes ONLY Applications' database, never Jobs'. It MUST be idempotent.
Do NOT add a synchronous call back to Jobs. Do NOT expose EF entities.

USAGE: Use the add-endpoint skill for both the endpoints and the consumer (its "Reacting to
another service" section is exactly this). Delegate review to the code-reviewer + api-contract-checker.

BEHAVIOR: Plan the service wiring, the endpoints, and the consumer's idempotent close path, and wait
for approval. Implement, run both services' tests green, then `aspire run` and demonstrate: close a
job in Jobs -> JobClosed on the bus -> Applications closes the matching applications exactly once
(prove idempotency by redelivering). Run the reviewers and summarize.
```

## Prompt 7 — Fan out the remaining services (Identity, Profiles, Notifications)

```
SCOPE: Stand up the remaining three services, each following the proven Jobs/Applications template
(thin host + .Core + own database + Shared spine): JobBoard.Identity (accounts + JWT issuance,
identitydb), JobBoard.Profiles (candidate + employer profiles, profilesdb), and JobBoard.Notifications
(no public HTTP; consumes ApplicationSubmitted / ApplicationStatusChanged / JobPosted and writes to a
notification log in notificationsdb). Wire JWT validation at the gateway so protected routes require a
token from Identity.

CONSTRAINT: Follow all rules in .claude/rules/. Each service is identical in SHAPE to Jobs — only the
domain differs. Notifications has consumers but no controllers or gateway routes.

RESTRICTION: Do NOT invent a new pattern per service — replicate the template. No shared database,
no service reaching into another's data, no logic in a host. Keep JWT secrets out of source; wire via
config/Aspire.

USAGE: Use the add-endpoint skill for every endpoint and consumer; use add-aspire-resource for
each new database/topic. Delegate review to the code-reviewer + api-contract-checker per service.

BEHAVIOR: Do these ONE service at a time, each: plan -> approve -> implement -> migrate (on approval)
-> `dotnet test` green -> review. Report after each before starting the next. Do not batch all three.
```

## Prompt 8 — Angular shell + data services (to the gateway)

```
SCOPE: Set up the Angular app in src/web (strict TS, standalone components). Add typed services on
HttpClient — JobService, ApplicationService, ProfileService — plus model interfaces mirroring each
service's ServiceModels exactly. All requests go to the GATEWAY base URL read from Aspire-injected
config. Add an auth HttpInterceptor that attaches the Identity JWT and handles 401.

CONSTRAINT: Follow .claude/rules/frontend.md and .claude/rules/aspire.md.

RESTRICTION: Do NOT hardcode any URL, and do NOT target a service directly — only the gateway. Do NOT
call HttpClient from components. No `any`. Tokens go through the interceptor, not per call.

USAGE: Use plan mode. Delegate review to the api-contract-checker (models vs ServiceModels) and
the code-reviewer.

BEHAVIOR: Show me how you read the injected gateway URL, the model interfaces, and the interceptor
before writing. Wait for approval. Implement, run `ng test`, run the api-contract-checker, and report
any drift between the models and the backend ServiceModels.
```

## Prompt 9 — Core UI components

```
SCOPE: Build the core UI wired to the typed services: job-list (cards + category filter), job-detail
(+ apply), post-job-form (employer), application-list (candidate's applications + status),
application-status badge, and login/register.

CONSTRAINT: Follow .claude/rules/frontend.md.

RESTRICTION: Standalone components only, kebab-case files. Use the async pipe (or clean up
subscriptions). Models match the ServiceModels. Do NOT bypass the typed services or reach past the
gateway to stitch two services together.

USAGE: Use the add-component skill for each component. Delegate the final review to the
code-reviewer subagent.

BEHAVIOR: Plan the component tree and data flow, wait for approval, implement, run `ng test`, run the
code-reviewer, and summarize.
```

## Prompt 10 — End-to-end run + verification

```
SCOPE: Bring the whole system up and verify it end to end: `aspire run`, then confirm the dashboard
shows Postgres (every database), the Service Bus emulator, all five services, the gateway, and the
Angular app healthy; that a request traces gateway -> owning service -> bus; and that the primary
flows work in the browser (post a job, apply, see status update, get a notification logged). Apply any
pending migrations if needed.

CONSTRAINT: Follow .claude/rules/aspire.md and .claude/rules/messaging.md.

RESTRICTION: Fix only wiring/config issues that block the end-to-end flow. Do NOT add features or
refactor unrelated code. Ask before applying any migration.

USAGE: Use the aspire CLI + dashboard; use the test-gap-analyzer to tell me what's under-tested
before I call this done.

BEHAVIOR: Report a short health summary (each resource + service, up/down), one full event trace you
verified, what you fixed, and the test-gap-analyzer's prioritized list of missing tests (flag any
consumer without an idempotency test).
```

## Prompt 11 — Stretch: add a cache resource to a hot read

```
SCOPE: Add a local Redis cache to the AppHost and use it in ONE service (e.g. Jobs) to cache the job
list at the facade, with invalidation on post/close.

CONSTRAINT: Follow .claude/rules/aspire.md and .claude/rules/backend.md. Caching lives only in the
facade and caches ServiceModels.

RESTRICTION: Local container only. No hardcoded connection details — wire via WithReference and the
Aspire client integration. Keep the AppHost declarative. Only the one service for now.

USAGE: Use the add-aspire-resource skill. Delegate review to the code-reviewer subagent.

BEHAVIOR: Plan the cache wiring + invalidation, wait for approval, implement, verify a cache hit/miss
in the dashboard, run the reviewer, and summarize.
```

---

# Part 2 — Operational templates (reuse anytime)

These are for the moments the agentic layer won't self-guard. Copy a block, fill the `<...>`, and
run. Delete lines that don't apply.

## Template A — Feature delivery (vertical slice within one service)

_Use for any new capability that lives in a single service + its UI. This is your everyday default._

```
SCOPE: Deliver <feature> end to end in the <service> service: <API change> and <UI change>. Scope is
this feature and this service only.

CONSTRAINT: Follow the rules in .claude/rules/. Match existing patterns rather than inventing new ones.
Host stays thin; logic lives in <service>.Core.

RESTRICTION: Do NOT change unrelated files, another service, or public ServiceModel/gateway contracts.
Do NOT open a second database or add a sync call to another service. No hardcoded config.

USAGE: Use the add-endpoint and/or add-component skills. Delegate review to the code-reviewer
subagent (+ api-contract-checker if a ServiceModel changed).

BEHAVIOR: Plan the vertical slice (data -> API -> gateway route -> UI) and wait for approval.
Implement in small steps, run `dotnet test` and `ng test` green, run the reviewer, and summarize.
```

## Template B — Cross-service change (event across the bus)

_Use whenever a change in one service must cause a change in another. This is the shape to reach for
instead of a synchronous call or a shared table._

```
SCOPE: When <trigger in service A>, <effect in service B>. Deliver it as: A publishes/uses the
<Event> integration event, and B consumes it to <do the effect> in B's own database.

CONSTRAINT: Follow .claude/rules/messaging.md and .claude/rules/backend.md. One event, two databases,
no shared table (the add-endpoint "Reacting to another service" pattern).

RESTRICTION: B writes ONLY its own database. The consumer MUST be idempotent (inbox). Do NOT add a
synchronous call from A to B or B to A. Keep the <Event> contract minimal — only the fields B needs.
Any change to an existing event in Contracts is a contract change — call it out.

USAGE: Use the add-endpoint skill in BOTH services (publish side in A, consumer side in B).
Delegate review to the code-reviewer + api-contract-checker.

BEHAVIOR: Plan the event shape, the publish point in A, and B's idempotent consumer, and wait for
approval. Implement, run both services' tests, then demonstrate the flow on `aspire run` including a
redelivery to prove idempotency. Summarize.
```

## Template C — Database / migration change (one service)

_Use for any schema or data change. Migrations are the closest thing here to irreversible, so the
guardrails are deliberately tight._

```
SCOPE: Make this schema/data change in the <service> service: <describe>. Produce the EF migration in
<service>.Core and update the affected ServiceModels, queries, and tests.

CONSTRAINT: Follow .claude/rules/backend.md and .claude/rules/aspire.md. DbContext lives in
<service>.Core; the host is the EF startup project.

RESTRICTION: Create the migration but do NOT run database update until I approve. Do NOT drop/rename
columns without a reversible plan. Do NOT run destructive SQL without a stated rollback. Do NOT touch
another service's database. No raw connection strings.

USAGE: Use plan mode. After the migration is generated, delegate to the code-reviewer.

BEHAVIOR: Show me three things before applying: (1) the model change, (2) the generated migration
file (from
  dotnet ef migrations add <Name> --project ../JobBoard.<Service>.Core --startup-project . --context <Service>DbContext ),
(3) the rollback story. Wait for approval. Then apply, confirm the schema, and run the tests.
```

## Template D — Refactor / cross-cutting change

_Use when changing structure without changing behavior — especially anything in JobBoard.Shared,
which touches every service. The point is Scope discipline._

```
SCOPE: Refactor <target> to <goal>. Behavior must not change. Before editing, list every file AND
every service affected, and why. (If the target is JobBoard.Shared, assume the blast radius is all
five services and say how you'll verify each still builds and passes.)

CONSTRAINT: Follow the rules in .claude/rules/. Keep ServiceModel/gateway/event contracts and test
expectations stable.

RESTRICTION: Do NOT change behavior or unrelated code. Do NOT change an event contract as part of a
"refactor". Do NOT expand beyond the listed files/services without checking in first.

USAGE: Use plan mode; use the Explore subagent to map usages first (across all services for a
Shared change). Delegate review to the code-reviewer subagent.

BEHAVIOR: First return the impact map (files + services + reason). Wait for approval. Refactor in
small, test-green steps — run the affected services' suites after each. If the blast radius grows
beyond the map, stop and re-plan with me.
```

## Template E — Debug / harden

_Use to fix a bug (minimal, root-cause) or to harden an area before shipping. Bus bugs often hide in
the seam between two services — say which side you suspect._

```
SCOPE: Diagnose and fix <bug/symptom> in <service or the A->event->B seam>, and add a regression test.
  (Harden variant: review <area> for correctness, idempotency, security, and missing tests.)

CONSTRAINT: Follow the rules in .claude/rules/.

RESTRICTION: Make the MINIMAL change that fixes the root cause — do NOT refactor around it or suppress
the symptom. If it's a messaging bug, do NOT "fix" it by adding a synchronous call. No new
dependencies.

USAGE: Use the Explore subagent to locate the cause; use the test-gap-analyzer for coverage
gaps; run /security-review for the harden variant. For a bus issue, check the outbox rows, the
dispatcher, and the consumer's inbox in that order.

BEHAVIOR: First reproduce the issue and state your root-cause hypothesis with evidence (which layer /
which service / which side of the bus). Wait for my nod on the diagnosis. Then apply the minimal fix,
add a regression test (an idempotency test if it was a consumer bug), run the suite, and summarize.
```

---

## Pro tips

- **Approve the plan, not the code.** The value is catching a wrong approach — or a boundary drawn in
  the wrong service — before it exists. If the plan is off, correct it and re-plan.

- **Prove one service and one event before fanning out.** Prompts 1–6 exist so that when Prompt 7
  replicates the template five times, the template is already known-good. Resist scaffolding all
  services at once.

- **One prompt = one clean context.** `/clear` before a new big task. Rules and skills reload
  automatically.

- **Let the guardrails work.** You wrote "no shared database" and "consumers are idempotent" into the
  rules and taught the reviewers to block violations — the RESTRICTION line just reinforces them when
  it matters most.

- **The bus is where microservices go wrong.** For anything cross-service, default to Template B, not
  a synchronous call. A shared table or an A→B HTTP call is almost always the boundary telling you the
  split is wrong.

- **Use `/rewind`** instead of stacking correction prompts on a polluted context.

- **Promote repeats to skills.** If you fill in the same operational template two or three times, that
  recurring shape wants to be a skill. Write it, and the prompt disappears.
  
  ```
  
  ```
