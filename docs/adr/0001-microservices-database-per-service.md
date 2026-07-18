# ADR-0001: Microservices with database-per-service

- **Status:** Accepted
- **Date:** 2026-07-18
- **Deciders:** Robert Felkins
- **Related:** ADR-0002 (event-driven integration), ADR-0005 (layering & references), `docs/high-level-design.md` §4–5, `CLAUDE.md`

## Context

JobBoard exists to demonstrate a *microservice* architecture that stays correct and independently evolvable — not a modular monolith wearing microservice clothes. The domain splits cleanly into capabilities (identity, jobs, applications, profiles, notifications) that different actors drive and that change at different rates. The primary risk in any first microservices attempt is **coupling through shared data**: the moment two services share a table or a schema, they can no longer be deployed, reasoned about, or scaled independently, and the "microservices" become a distributed monolith with all the cost and none of the benefit.

## Decision

**We will decompose the system into five bounded-context services, each owning its own PostgreSQL database, with no shared database — ever.**

- Each service is the sole reader and writer of its own store: `identitydb`, `jobsdb`, `applicationsdb`, `profilesdb`, `notificationsdb` (declared in `AppHost.cs`).
- Decomposition is by **business capability**, not by entity or by layer.
- A service that needs another service's data either **consumes that service's event and keeps a local copy**, or routes a query through the gateway — never a second connection string.
- **Adding a service is an architectural decision, not a feature.** New services are proposed, not scaffolded on impulse (`CLAUDE.md` scope rule).

## Consequences

**Positive**
- True independence: each service deploys, migrates, and scales on its own.
- Boundaries are enforced by the strongest possible mechanism — there is physically no way to reach another service's data.
- Blast radius of a schema change is contained to one service.

**Negative**
- Cross-service queries ("my applications, with job title and employer name") are not a JOIN; they require either event-carried denormalization or query composition (deferred to ADR-0012).
- No cross-service ACID transaction; consistency across services is eventual (accepted via ADR-0003/0004).
- Some reference data is duplicated (e.g. a job's title travels inside `JobPosted`). This is a deliberate trade against coupling.

**Neutral**
- One physical PostgreSQL *server* hosts the five logical databases locally; that's a deployment convenience, not a shared schema — the isolation boundary is the database, and nothing crosses it.

## Alternatives considered

- **Shared database, service code split.** Rejected: it re-introduces the coupling the whole exercise is meant to avoid; a shared table is a hidden contract that silently breaks independent evolution.
- **Modular monolith (one database, enforced module boundaries).** A legitimate and often *better* choice for a small team — but it doesn't demonstrate the thing JobBoard exists to demonstrate, and it side-steps the failure-handling patterns (outbox/inbox) that are the point.
- **Schema-per-service in one database.** Rejected: cheaper isolation, but still a single failure/lock domain and an easy path back to accidental cross-schema joins.
