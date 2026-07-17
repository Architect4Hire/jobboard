---
name: add-aspire-resource
description: >
  Declare a new locally-orchestrated resource in the JobBoard AppHost — a per-service database, a
  Service Bus topic/subscription, a cache, or a whole new service host — wiring it with the model
  (WithReference / WaitFor) and no hardcoded addresses. Use when asked to "add a database for
  X", "add a topic for the Y event", "add a Redis cache to Z", or "stand up a new service".
  Keeps the AppHost declarative and local-first; verifies fast-moving Aspire/Service Bus API names
  against the docs.
---

# Add an Aspire resource

Everything the system runs is declared **once, in `JobBoard.AppHost`**. This skill adds a resource
there and wires it to whoever needs it — nothing outside the AppHost invents infrastructure, and
nothing hardcodes an address. Read `.claude/rules/aspire.md` first; for messaging resources also read
`.claude/rules/messaging.md`, and for a new service also `.claude/rules/backend.md`.

**Local-first is the invariant.** Backing resources are local containers. An *emulator-backed* Azure
resource is in bounds (it's a local container) — `AddAzureServiceBus(...).RunAsEmulator(...)`,
`AddAzureStorage(...).RunAsEmulator(...)`. A resource needing a real subscription (`AsExisting`,
real provisioning) is **not**; stop and ask before adding one.

## Pick the resource type

| You need… | Add in AppHost | Referenced by | Consumed via (client integration) |
|---|---|---|---|
| A database for a service | `pg.AddDatabase("<svc>db")` on the shared Postgres server | that one service host (`WithReference` + `WaitFor`) | Aspire Npgsql integration → `<Service>DbContext` |
| A topic/subscription for an event | a topic + per-consumer subscription on the `servicebus` resource | publisher + each consuming host | `AddAzureServiceBusClient` (already wired) |
| A cache for a hot read | `AddRedis("cache")` (or `.AddDatabase`-style keyed cache) | the service(s) that cache | Aspire Redis client integration → facade cache |
| A whole new service | `AddProject<Projects.JobBoard_<Svc>>("<svc>")` | the gateway (route) + its own DB/bus refs | n/a (it's the consumer) |

## Steps

### A new database (per-service — the common case)
1. On the shared Postgres server resource, add the database: `var xdb = pg.AddDatabase("<svc>db");`.
   **One database per service** — never point two services at the same one.
2. Reference it from **only** that service's host and order startup:
   `AddProject<...>("<svc>").WithReference(xdb).WaitFor(xdb)`.
3. In that service's `.Core`, register `<Service>DbContext` through the Aspire Npgsql integration keyed
   to `"<svc>db"` (no raw connection string). The context derives from the Shared base context.
4. The first migration creates the schema (incl. the Outbox/Inbox tables) — see the `add-endpoint`
   skill's migration step (`--project ../JobBoard.<Service>.Core --startup-project .`).

### A new Service Bus topic/subscription (for a new event)
1. On the `servicebus` resource, declare the **topic** for the event and a **subscription** per
   consuming service (the emulator needs these in its entity-config — verify the format against the
   docs). Name the topic after the event or a stable channel, not a service.
2. Ensure the publisher host and every consumer host `WithReference(servicebus)`.
3. No client change beyond mapping the event's destination in the dispatcher and registering the
   `IIntegrationEventConsumer<TEvent>` — that's `add-endpoint` territory. The dispatcher/processor
   plumbing in `JobBoard.Shared` is reused unchanged.

### A cache
1. `var cache = builder.AddRedis("cache");` (local container).
2. `WithReference(cache)` from the service(s) that need it; consume via the Aspire Redis client
   integration. Caching lives only in the **facade** and caches ServiceModels (read-through +
   invalidate) — never in business or data.

### A new service host
1. Scaffold it as **two projects** following the template (thin host + `.Core`), per `backend.md`.
2. In the AppHost: `AddProject<Projects.JobBoard_<Svc>>("<svc>")`, `WithReference` its own database and
   `servicebus`, `WaitFor` the database.
3. If it's client-facing, add its gateway route (by resource name) per `gateway.md`. If it's
   event-only (like Notifications), it gets no route.

## Verify before trusting
Confirm exact API names and the emulator entity-config format against https://aspire.dev before
running: `AddDatabase`, `AddAzureServiceBus(...).RunAsEmulator(...)`, topic/subscription declaration,
`AddRedis`, `AddProject`, `WithReference`/`WaitFor`, and the client-integration methods. These move
between versions.

## Checklist before done
- [ ] The resource is declared in the AppHost only; the AppHost stays declarative (no logic)
- [ ] Local container / emulator — no resource needing a real subscription
- [ ] Wired with `WithReference` / `WaitFor`; **no** hardcoded connection string, namespace, or
      `localhost:port`
- [ ] A database is referenced by exactly one service; no shared database
- [ ] A new event topic has a subscription per consumer; the dispatcher/processor plumbing is reused
- [ ] A cache is consumed only in the facade (ServiceModels)
- [ ] A new client-facing service has a gateway route by resource name; an event-only service doesn't
- [ ] `aspire run` shows the new resource healthy in the dashboard
