# JobBoard

*Project memory, written as a SCRUB prompt — Scope, Constraints, Restrictions, Usage, Behavior. Loaded every session. Every new rule has one obvious home, and every misstep is diagnosable by section.*

## Scope

- JobBoard is a job-board platform — employers post jobs, candidates apply, everyone gets notified — built to showcase a **microservice** architecture on **Aspire + ASP.NET Core + Angular**, developed with Claude Code. It does two things at once: it's a genuinely good job board, and a public demonstration of driving a *multi-service* Aspire stack with Claude Code. The reusable toolkit lives in `.claude/`.

- **In bounds:** the five bounded services, the gateway, the shared contracts library, the Angular app, and the `.claude/` toolkit that builds them.

- **Out of bounds (don't build unprompted):** a new service, a new bus, or a new shared library. Adding a *service* is an architectural decision, not a feature — propose it, don't scaffold it. "Shared" here means **integration-event contracts only**; it never means shared domain code or a shared database.

## Constraints

**Stack**

- **Orchestration:** Aspire 13 (AppHost + ServiceDefaults) on .NET 10
- **Services — two projects each, one bounded context each.** A service is a **thin host** (`JobBoard.<Service>`, ASP.NET Core Web API: entry points + composition root only) plus a **class library** (`JobBoard.<Service>.Core`) that holds the whole **facade → business → data layer → repository** stack and its models/validators/mappers. The host references its own `.Core`; nothing else. EF Core / Npgsql lives in `.Core`.
  - `JobBoard.Identity` (+`.Core`) — accounts + JWT issuance for employers and candidates. Owns `identitydb`.
  - `JobBoard.Jobs` (+`.Core`) — job postings, categories, tags, search/filter. Owns `jobsdb`. Publishes `JobPosted`, `JobClosed`.
  - `JobBoard.Applications` (+`.Core`) — applications to postings and their status lifecycle. Owns `applicationsdb`. Consumes `JobClosed`; publishes `ApplicationSubmitted`, `ApplicationStatusChanged`.
  - `JobBoard.Profiles` (+`.Core`) — candidate résumés and employer company profiles. Owns `profilesdb`.
  - `JobBoard.Notifications` (+`.Core`) — consumes events and sends email; no public HTTP surface. Owns `notificationsdb` (outbox + delivery log).
- **Edge:** `src/JobBoard.Gateway/` — YARP reverse proxy. The **only** public entry point; the Angular app talks to nothing else.
- **Shared cross-cutting code:** `src/JobBoard.Shared/` — the class library every `.Core` builds on: the base `DbContext` (owns the `OutboxMessages` / `InboxMessages` sets), the base repository + `ExecuteInTransactionAsync`, `IOutbox` / `IInbox` and their implementations, the generic outbox **dispatcher** and Service Bus **processor host**, the global exception handler, the cache abstraction, and the shared error shape. Cross-cutting *mechanism* only — no service's domain, business, or data ever lives here.
- **Shared contracts:** `src/JobBoard.Contracts/` — integration-event record types (each an `IIntegrationEvent` with an `Id`). No domain logic, no EF, no DTOs that aren't events. It's a leaf: it references nothing, and everything else can reference it.
- **Messaging:** **Azure Service Bus** for integration events, run locally as an **emulator** container via Aspire (`AddAzureServiceBus(...).RunAsEmulator(...)` — the Service Bus twin of the Azurite example in `rules/aspire.md`; it's a local container, so it stays in bounds). Reliability is a **hand-rolled transactional outbox** (in `JobBoard.Shared`, per-service tables) — the integration event is written to that service's own `OutboxMessages` table in the *same transaction* as the domain write, and a background dispatcher relays it to Service Bus afterward. No MassTransit, no third-party outbox.
- **Frontend:** Angular (standalone components, strict TS) — `src/web/`, run via `AddJavaScriptApp`, and it calls **only** the gateway.
- **Data/infra (local containers via Aspire):** one PostgreSQL server with a database *per service*, the Azure Service Bus emulator, and a cache where a service needs one.

**Layout**

```
src/
├── JobBoard.AppHost/            # Aspire orchestrator — declares every resource + wiring
├── JobBoard.ServiceDefaults/    # Aspire cross-cutting: telemetry, health, resilience, discovery
├── JobBoard.Contracts/          # integration-event records (leaf — references nothing)
├── JobBoard.Shared/             # shared cross-cutting CODE: base DbContext, base repo +
│                                #   ExecuteInTransactionAsync, IOutbox/IInbox + impls, outbox
│                                #   dispatcher + Service Bus processor host, exception handler,
│                                #   cache abstraction, error shape
├── JobBoard.Gateway/            # YARP — the ONLY public door
│
│   # each service = a thin host + a facade→repository library (shown for Jobs; same for the rest)
├── JobBoard.Jobs/               # HOST: Controllers/, Consumers/, Program.cs (composition root)
├── JobBoard.Jobs.Core/          # LIBRARY: Facade/ Business/ Data/ Managers/  ── jobsdb
├── JobBoard.Identity/  + .Core/         ── identitydb
├── JobBoard.Applications/ + .Core/      ── applicationsdb
├── JobBoard.Profiles/  + .Core/         ── profilesdb
├── JobBoard.Notifications/ + .Core/     ── notificationsdb (no public HTTP)
└── web/                         # Angular app (AddJavaScriptApp target)
```

**Reference direction is one-way and acyclic** — `Contracts` ← `Shared` ← `<Service>.Core` ← `<Service>` (host) ← `AppHost`; hosts and the gateway also reference `ServiceDefaults`. A host references *its own* `.Core` and never another service's. If a reference would point the other way, or sideways between two services, the design is wrong.

**Architecture conventions** — area detail auto-loads from `.claude/rules/` (`aspire.md`, `backend.md`, `frontend.md`, `messaging.md`, `gateway.md`). The essentials:

- **Aspire:** every resource — each database, the Service Bus emulator, every service host, the gateway, the web app — is declared in the AppHost. Services find each other and their infra through `WithReference` / service discovery, never strings.
- **Per-service internals:** the host holds only entry points (Controllers, Consumers) and the composition root; the whole **facade → business → data layer → repository** stack lives in `<Service>.Core`, built on `JobBoard.Shared`. Only ViewModels in, only ServiceModels out; never expose EF entities; everything async; input validated at the edge. The `add-endpoint` skill is the full playbook.
- **Between services:** talk over **Service Bus** (integration events, published via each service's outbox), not by reaching into another service's database (there is no way to — each owns its own) and not by chatty synchronous calls. Duplicate the little reference data you need; don't couple.
- **Frontend:** standalone components, typed models mirroring a service's ServiceModels, HTTP only through services, all requests to the **gateway** base URL, `async` pipe (no leaked subscriptions).

**Canonical commands** (use these verbatim)

- Whole system (repo root or the AppHost folder): run everything + dashboard `aspire run` · add a resource package `aspire add <resource>`
- A service: `dotnet test` runs that service's test project. Migrations live in `<Service>.Core` (where the `DbContext` is) but need the host as startup project (where DI + config resolve) — run from the host folder: `dotnet ef migrations add <Name> --project ../JobBoard.<Service>.Core --startup-project . --context <Service>DbContext` · `dotnet ef database update --project ../JobBoard.<Service>.Core --startup-project . --context <Service>DbContext`
- Frontend (`src/web/`): `npm install` · `ng test` · `ng build`

## Restrictions

- **No shared database, ever.** A service reads and writes **only** its own database. Needing another service's data is a signal to consume its event and keep a local copy, or to route a query through the gateway — never a second connection string. If you're tempted to add one, stop and say so.
- **The gateway is the only public door.** Individual services are not exposed to the browser. New public routes are added to the gateway; a service endpoint with no gateway route is unreachable by design, and that's fine for internal-only endpoints.
- **`Contracts` holds events and nothing else.** No entities, no ServiceModels, no helpers (only the `IIntegrationEvent` marker the events implement). If two services need the same *domain* type, they don't — that's a boundary telling you something.
- **`JobBoard.Shared` holds cross-cutting *mechanism* only.** Base context, base repository, outbox/inbox, dispatcher, processor host, exception handler, cache, error shape — code every service needs *the same way*. No service's domain, business rules, ViewModels, or ServiceModels ever land here; when something feels shared but is really one service's logic, it belongs in that service's `.Core`.
- **The host stays thin.** `JobBoard.<Service>` contains only entry points (Controllers, Consumers) and the composition root (`Program.cs` calling the `.Core` and `Shared` registration extensions). Facade, business, data-layer, repository, and models live in `.Core`; if logic is creeping into a controller or `Program.cs`, it's in the wrong project.
- **Publish through the outbox, in the same transaction as the write.** An integration event is written to the `OutboxMessages` table alongside the domain change, in one transaction — never sent to Service Bus inline from business or data code. The **outbox dispatcher** (a background service) is the *only* thing that talks to Service Bus on the send side; a write that commits without its outbox row, or a send that isn't relayed from the outbox, is the bug this rule exists to prevent.
- **Consumers are idempotent.** Service Bus (and the outbox relay) deliver **at least once**, so every consumer dedupes — record handled message IDs in an `InboxMessages` table, in the same transaction as the side effect. A handler that isn't safe to run twice is a bug, not an edge case.
- **Wire through Aspire.** Don't hardcode connection strings, broker addresses, or `localhost:port` — everything comes from the AppHost via `WithReference` / injected config. **One sanctioned exception:** the gateway's YARP `Clusters` may name services by their **Aspire resource name** (e.g. `http://jobs`) because service discovery resolves those names — that's using the model, not hardcoding an address. A literal host:port anywhere is a violation.
- Don't put business logic in the AppHost or the gateway; both stay declarative.
- Don't run `ng serve` by hand — Aspire launches the client via `AddJavaScriptApp`.
- Don't hand-edit generated EF migrations except to review them, and never point `dotnet ef` at the wrong service's context.
- Don't commit `bin/`, `obj/`, `node_modules/`, or any secrets.

## Usage

- The world is **local**: Aspire's AppHost orchestrates every service, the gateway, the Angular app, and all backing resources (a Postgres server with a database per service, the Service Bus emulator, cache) as local containers — no cloud dependencies. The dashboard is the front door for logs, traces, and health across all of them; a request's trace should be followable from the gateway through the owning service and onto the bus.
- Services find each other and their infra through service discovery / Aspire-injected config — never hardcoded addresses.
- The Angular app is the primary consumer, and it consumes **the gateway** — keep that contract stable; the service boundaries behind it can move.
- Available tooling in `.claude/`: rules auto-load from `.claude/rules/`; task skills live in `.claude/skills/` (`add-endpoint`, `add-component`, `add-aspire-resource`); subagents are available but run **read-only**.

## Behavior

- Plan before any change touching more than one file — and **always** name which service(s) a change lands in before touching code. A change that spans two services is a design conversation first.
- Use the matching skill in `.claude/skills/` instead of freelancing.
- Run the relevant service's tests before calling a task done; for anything that crosses the bus, run both the publisher's and the consumer's tests.
- Make edits in the main session so I can approve them — subagents stay read-only.
