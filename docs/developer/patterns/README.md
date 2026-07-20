# Pattern Deep Dives

*Twelve mechanisms, each explained once, grounded in the real code that implements it.*

The other developer docs walk a **task** end to end (add an endpoint, seed a database). These walk a
**mechanism** end to end — the thing that recurs across every feature slice, explained in enough depth
to extend or debug it without re-deriving it from scratch. Each doc cites the actual files, links to the
ADR(s) that made the call (the *why* — these docs don't re-argue it), and closes with the standing rules
from `.claude/rules/` that keep the pattern honest.

If you want to *build* a feature, start with
[Adding an Endpoint by Hand](../adding-an-endpoint-manually.md) — it uses most of these patterns in
context. Come here when you want to understand *how one of them actually works*, or you're changing the
mechanism itself rather than using it.

## Index

| Doc | Pattern | Read this when… |
| --- | --- | --- |
| [Layered Service Architecture](./layered-service-architecture.md) | Thin host + `.Core` stack (Facade → Business → Data → Repository), three model types, mappers | You're not sure which layer a piece of logic belongs in, or a ViewModel/Domain/ServiceModel is leaking somewhere it shouldn't. |
| [Transactional Outbox & Inbox](./transactional-outbox-and-inbox.md) | Reliable at-least-once messaging: outbox row in the write transaction, dispatcher relay, inbox dedupe | An event isn't reaching a consumer, a consumer double-applies a side effect, or you're adding a new publish/consume path. |
| [Database-per-Service & Data Ownership](./database-per-service-and-data-ownership.md) | No shared database; reference data crosses as duplicated literals; cross-service reads today vs. the open decision | You're tempted to add a second connection string, or a screen needs data that lives in another service's database. |
| [Read-Through Caching](./read-through-caching.md) | Fail-open read-through cache with generation-token invalidation | You're caching a list/read endpoint, or a cached response is stale after a write. |
| [Concurrency Control](./concurrency-control.md) | Conditional-write state transitions and get-or-create unique-violation retries | Two requests can race on the same row (a status transition, a get-or-create) and you need the write, not the read, to be the guard. |
| [API Gateway & Edge Concerns](./api-gateway-edge.md) | YARP as the only public door: routing by resource name, auth at the edge, correlation minting | You're adding a client-facing route, or something about auth/CORS/correlation looks wrong at the edge. |
| [Authentication & Identity Propagation](./authentication-and-identity-propagation.md) | JWT issuance → edge validation → trusted-header projection → business-layer actor | You're touching login/registration, token validation, or where a service learns "who is calling." |
| [Correlation, Causation & the Audit Trail](./correlation-causation-and-audit-trail.md) | Threading `CorrelationId`/`CausationId`/actor onto events; the bus-fed, append-only support trail | You're adding a new mutating action and need it to show up in `trace-a-request`, or a trail row's actor/causation looks wrong. |
| [Integration Event Contracts](./integration-event-contracts.md) | `Contracts` as a leaf library; past-tense, minimal-field event records; status-as-string | You're defining a new integration event or changing an existing one. |
| [Aspire Orchestration](./aspire-orchestration.md) | The declarative resource model, service discovery, local emulators | You're adding a resource (database, topic, cache) or wondering how a service finds another. |
| [Exception Handling & Error Shape](./exception-handling-and-error-shape.md) | Global exception handler mapping to one shared error shape | You're throwing a new kind of failure and need to know how it reaches the client. |
| [Frontend ↔ Gateway Integration](./frontend-gateway-integration.md) | Standalone components, typed gateway services, the auth interceptor | You're adding an Angular service/component that talks to the API, or debugging auth headers on the client side. |

## Which pattern do I need?

- **"My event never arrived at the consumer."** → [Transactional Outbox & Inbox](./transactional-outbox-and-inbox.md) — check the outbox row committed, then that the dispatcher relayed it.
- **"Two requests both passed my check but only one should have won."** → [Concurrency Control](./concurrency-control.md).
- **"A cached list is showing stale data after a write."** → [Read-Through Caching](./read-through-caching.md).
- **"I need another service's data for this screen."** → [Database-per-Service & Data Ownership](./database-per-service-and-data-ownership.md).
- **"Who is the caller, and can I trust the id in the request body?"** → [Authentication & Identity Propagation](./authentication-and-identity-propagation.md).
- **"Support needs to know what happened to this request/entity."** → [Correlation, Causation & the Audit Trail](./correlation-causation-and-audit-trail.md), and the [`trace-a-request`](../../../.claude/skills/trace-a-request/SKILL.md) skill to query it.
- **"How does the browser reach a service at all?"** → [API Gateway & Edge Concerns](./api-gateway-edge.md), then [Frontend ↔ Gateway Integration](./frontend-gateway-integration.md).
