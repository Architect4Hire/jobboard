# Aspire Orchestration

*One declarative model — `AppHost.cs` — is the single source of truth for every resource, every
dependency edge, and every piece of config a service or the frontend needs. `aspire run` is the only
command; nothing invents infrastructure at runtime.*

## The problem this solves

Five services, six databases, a message bus, a cache, and a frontend each need to find each other and
get configured consistently, in dev, without a developer hand-assembling connection strings or a
docker-compose file that drifts from what's actually declared in code. Aspire's AppHost makes the
*topology itself* the artifact under version control — resources, references, and startup ordering are
one C# file, and `WithReference` is how a dependency edge becomes injected config instead of a string
someone has to keep in sync by hand.

## How it works here

[`AppHost.cs`](../../../src/JobBoard.AppHost/AppHost.cs) declares, in order:

**Backing resources**, each a local container:

```csharp
var postgres = builder.AddPostgres("postgres");
var jobsDb = postgres.AddDatabase("jobsdb");          // ...one .AddDatabase per service
var cache = builder.AddRedis("cache");
var resumeBlobs = builder.AddAzureStorage("storage").RunAsEmulator().AddBlobs("blobs");
var serviceBus = builder.AddAzureServiceBus("servicebus").RunAsEmulator();
```

`RunAsEmulator()` is the load-bearing detail: an Azure Storage or Service Bus resource run this way is a
**local container**, not a cloud dependency — the same category as `AddPostgres` or `AddRedis`. What
would put a resource out of bounds is reaching it with `AsExisting` or provisioning it for real; nothing
in this AppHost does that.

**Topics and subscriptions, declared per event**:

```csharp
var jobClosedTopic = serviceBus.AddServiceBusTopic("JobClosed");
jobClosedTopic.AddServiceBusSubscription("applications-jobclosed");
jobClosedTopic.AddServiceBusSubscription("audit-jobclosed");
```

The topic name matches the event-type name, which matches the outbox row's `Destination` (see
[Transactional Outbox & Inbox](./transactional-outbox-and-inbox.md)) — that's how the dispatcher's
sender finds the right topic without any lookup table. Every business event carries an `audit-*`
subscription alongside its "real" consumer's, because `JobBoard.Audit` is a second, independent
subscriber on the same topic — adding audit coverage for an event is a new subscription, never a new
publish path. Subscription names are globally unique across the whole Aspire model (not scoped per
topic), which is why they're written `<service>-<event>` rather than just `<service>`.

**Each service, wired to only what it needs**:

```csharp
var jobs = builder.AddProject<Projects.JobBoard_Jobs>("jobs")
    .WithReference(jobsDb).WithReference(serviceBus).WithReference(cache)
    .WaitFor(jobsDb).WaitFor(serviceBus).WaitFor(cache);
```

`WithReference` injects connection info (as environment variables / service-discovery entries) —
nothing a service reads is a literal. `WaitFor` orders startup so a service doesn't come up racing its
own database. Notice **Notifications never gets a gateway reference** and the gateway never
`WithReference`s Notifications either — that absence is what makes it internal-only by construction
(see [API Gateway & Edge Concerns](./api-gateway-edge.md)); only services proxied by the gateway are
wired to it.

**The gateway, referencing every proxied service by its resource name** — `jobs`, `applications`,
`identity`, `profiles`, `audit` — because that's the exact string YARP's clusters resolve through
service discovery (`http://jobs`). The shared JWT signing key is generated once, here, and handed to
both Identity (issuer) and the gateway (validator) via `WithEnvironment`, so it's identical on both
sides without ever being a literal in either project:

```csharp
var jwtSigningKey = builder.Configuration["Jwt:SigningKey"]
    ?? Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
```

**The frontend, as a resource like any other**:

```csharp
builder.AddJavaScriptApp("web", "../web", "start")
    .WithHttpEndpoint(env: "PORT")
    .WithReference(gateway)
    .WaitFor(gateway);
```

Aspire launches `npm start` itself and injects the gateway's resolved address as
`services__gateway__https__0` — [`proxy.conf.js`](../../../src/web/proxy.conf.js) (the Angular dev
server's proxy config, which runs in Node and *can* read that env var, unlike the browser bundle) reads
it and forwards the SPA's `/api/*` calls there. See
[Frontend ↔ Gateway Integration](./frontend-gateway-integration.md) for the browser side of that same
seam.

## Why

[ADR-0008](../../adr/0008-aspire-local-first-servicebus-emulator.md) is the decision to run everything
local-first, including the emulator-backed resources.

## Pitfalls / rules to respect

- **Declare every resource in the AppHost.** Nothing outside it invents infrastructure — a new database,
  topic, or cache is an AppHost change first (use the `add-aspire-resource` skill).
- **`WithReference`/`WaitFor`, never a hardcoded connection string, namespace, or `localhost:port`.**
  Aspire injects endpoints and connection info; reading a literal anywhere is a violation.
- **Wire a service to only what it actually uses.** A service that doesn't consume events (yet) doesn't
  reference the bus; one with no public HTTP surface doesn't get a gateway reference.
- **The AppHost stays declarative.** No business logic, no conditional wiring based on domain state —
  it orchestrates, it doesn't compute.
- **Cross-cutting config (telemetry, health, resilience, discovery) lives in `ServiceDefaults`**, called
  once by every host and the gateway — not repeated per service.

See `.claude/rules/aspire.md` for the full standing-rule list.

## Reference map

| Concern | Real file |
| --- | --- |
| The whole resource model | [`AppHost.cs`](../../../src/JobBoard.AppHost/AppHost.cs) |
| Cross-cutting service config | `JobBoard.ServiceDefaults` |
| Frontend's read of the injected gateway address | [`proxy.conf.js`](../../../src/web/proxy.conf.js) |
| Standing rules | [`.claude/rules/aspire.md`](../../../.claude/rules/aspire.md) |
