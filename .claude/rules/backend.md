---
paths:
  - src/JobBoard.*/**
  - "!src/JobBoard.AppHost/**"
  - "!src/JobBoard.ServiceDefaults/**"
  - "!src/JobBoard.Gateway/**"
  - "!src/JobBoard.Contracts/**"
---
# Backend rules — ASP.NET Core + EF Core (Aspire microservices)

Every service is **two projects**: a thin host (`JobBoard.<Service>`) and a class library
(`JobBoard.<Service>.Core`). Both build on `JobBoard.Shared`. The full playbook for adding a route or
consumer is the `add-endpoint` skill; these are the standing rules.

- **The host is thin.** `JobBoard.<Service>` holds only entry points — `Controllers/`, `Consumers/`
  — and the composition root (`Program.cs`). No business logic, EF, or data access in the host.
- **`.Core` holds the stack.** Facade → business → data layer → repository, plus models, validators,
  and mappers, all live in `JobBoard.<Service>.Core`. EF Core / Npgsql lives here, never in the host.
- **The layers, and their strict responsibilities:**
  - **Controller / Consumer** — bind a ViewModel (or map an integration event) and call the facade;
    return an `ActionResult<ServiceModel>`. A consumer is idempotent (inbox). No logic.
  - **Facade** — validates the ViewModel and owns caching of ServiceModels. No mapping, EF, or bus.
  - **Business** — translates ViewModel → Domain, applies domain rules, *builds* the integration
    event when a change warrants one, maps Domain → ServiceModel. No validation, cache, EF, or send.
  - **Data layer** — composes repository calls into whole operations and enqueues the outbox row in
    the same transaction via `IOutbox`. No rules, mapping, cache, validation, or `DbContext`.
  - **Repository** — EF queries against `<Service>DbContext`, plus `ExecuteInTransactionAsync` from
    the Shared base repository. Data only.
- **Three model types at the boundary.** Only **ViewModels** enter and only **ServiceModels** leave;
  **Domain** entities are the internal shape. Never expose an EF entity across the controller, and
  never let a Domain entity cross the *service* boundary — that's what integration events are for.
- **DbContext via Aspire.** `<Service>DbContext` (in `.Core`, deriving from the Shared base context)
  is registered through the Aspire Npgsql integration keyed to the service's database resource (e.g.
  `jobsdb`), not by reading a raw connection string.
- **Registration is owned by `.Core`.** Expose `Add<Service>Core()` from the library (registers every
  layer + validators from its own assembly); the host's `Program.cs` calls it plus the Shared
  extensions. No per-layer wiring scattered through the host.
- **Async all the way.** `async Task<...>` with `await`; never `.Result` or `.Wait()`.
- **Validate at the edge.** FluentValidation in `.Core/Managers/Validators/`; on failure the shared
  exception handler returns the shared error shape, not a raw exception.
- **Service defaults.** Every host's `Program.cs` calls `AddServiceDefaults()`.
- **EF Core workflow (split project).** The `DbContext` is in `.Core`; the host is the startup
  project. Run from the host folder:
  `dotnet ef migrations add <Name> --project ../JobBoard.<Service>.Core --startup-project . --context <Service>DbContext`,
  review, then `database update`. Commit the migration.
- **Naming.** PascalCase types/methods, `_camelCase` private fields, camelCase locals.
- **One type per file.** Each public type lives in its own file, named for the type — records, enums,
  and interfaces included (an interface and its implementation are two files, e.g. `IOutbox.cs` +
  `Outbox.cs`). The only companions allowed to share a file are ones that have no meaning apart from
  their parent (a private nested type).

Domain lives in each service's `.Core` (starting shapes): **Jobs** — `Job`, `Category`, `Tag`;
**Applications** — `Application` (status lifecycle); **Profiles** — `CandidateProfile`,
`EmployerProfile`; **Identity** — `Account`, roles.
