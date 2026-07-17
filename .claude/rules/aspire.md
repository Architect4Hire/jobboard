---
paths:
  - src/JobBoard.AppHost/**
  - src/JobBoard.ServiceDefaults/**
---
# Aspire rules — AppHost & ServiceDefaults

The AppHost is the single source of truth for the application model. Keep it declarative.

- **Declare every resource here.** The Postgres server and each per-service database, the Azure
  Service Bus emulator, every service host, the gateway, and the Angular app are all added in the
  AppHost — e.g. `AddPostgres("pg")` then `.AddDatabase("jobsdb")` / `.AddDatabase("applicationsdb")`
  (one database per service), `AddAzureServiceBus("servicebus").RunAsEmulator(...)`,
  `AddProject<Projects.JobBoard_Jobs>("jobs")`, `AddProject<Projects.JobBoard_Gateway>("gateway")`,
  and `AddJavaScriptApp("web", "../web", "start")`. Nothing outside the AppHost invents infrastructure.
- **Database per service — no sharing.** Each service gets its own database resource and references
  only that one. Never wire two services to the same database; cross-service data moves over Service
  Bus, not a shared connection.
- **Local-first.** Backing resources run as local containers for development — no cloud/Azure
  resources in this PoC. An *emulator-backed* Azure resource is in bounds because it is a local
  container: `AddAzureServiceBus("servicebus").RunAsEmulator(...)` runs the Service Bus emulator, and
  `AddAzureStorage("storage").RunAsEmulator(...)` runs Azurite — exactly as `AddPostgres` runs
  Postgres. The test is where it runs, not what the API is called. What stays out is a resource that
  needs a real subscription — anything reached with `AsExisting`, or provisioned for real.
- **Wire with the model, not with strings.** Connect services using `WithReference(...)` (their
  database, the Service Bus, the cache) and order startup with `WaitFor(...)`. Never hardcode
  connection strings, a Service Bus namespace, or `localhost:port`; Aspire injects endpoints and
  connection info via environment/service discovery.
- **The gateway is a first-class resource.** It's the only public entry point; give services stable
  Aspire resource names (`jobs`, `applications`, …) because the gateway resolves routes by those
  names through service discovery.
- **Cross-cutting config lives in ServiceDefaults.** OpenTelemetry, health checks, resilience, and
  service discovery are configured once there; every service host and the gateway call
  `AddServiceDefaults()`. A request's trace should be followable from the gateway through the owning
  service and onto the bus.
- **No business logic in the AppHost.** It orchestrates; it doesn't compute.
- **The Angular app is an `AddJavaScriptApp` resource.** Aspire runs it and injects the gateway
  endpoint — the frontend reads the gateway base URL from injected config, not a hardcoded value.

When adding a new resource (a database, a topic, a cache), use the `add-aspire-resource` skill.
Verify exact API names (`AddAzureServiceBus`, `RunAsEmulator`, the emulator's entity-config format,
`AddJavaScriptApp`, client-integration methods, package names) against https://aspire.dev — these
move between versions.
