# ADR-0008: Aspire local-first topology + Service Bus emulator

- **Status:** Accepted
- **Date:** 2026-07-18
- **Deciders:** Robert Felkins
- **Related:** ADR-0002 (Service Bus), ADR-0006 (gateway discovery), `docs/high-level-design.md` §9, `.claude/rules/aspire.md`
- **Implements:** `JobBoard.AppHost/AppHost.cs`, `JobBoard.ServiceDefaults/Extensions.cs`

## Context

The system must be trivially runnable by anyone (including an agent) with **one command and zero cloud spend**, yet it must exercise the *real* messaging and data technologies so that going live is a configuration change, not a rewrite. Hand-wiring connection strings, ports, and broker addresses across seven processes is error-prone and drifts; hardcoded `localhost:port` literals are a classic source of "works on my machine."

## Decision

**We will orchestrate the entire system with Aspire as local containers, declare every resource in the AppHost, and talk to the real Azure Service Bus SDK through the emulator — with all wiring via service discovery, never hardcoded.**

- **One AppHost declares everything:** a PostgreSQL server with a database per service, Redis, the Azure Service Bus emulator (`AddAzureServiceBus(...).RunAsEmulator()`), all five service hosts, the gateway (external endpoint), and the Angular app (`AddJavaScriptApp`).
- **Wiring via the model.** Services find each other and their infra through `WithReference` / service discovery and injected config. **No connection strings, broker addresses, or `host:port` literals in code.** The one sanctioned exception: the gateway names services by Aspire resource (`http://jobs`), which service discovery resolves (ADR-0006).
- **Real SDK, local transport.** Code uses the real `ServiceBusClient` (`AddAzureServiceBusClient`) keyed to the `servicebus` resource; only the *binding* is the emulator. Topics/subscriptions are declared as Aspire resources, not invented at runtime.
- **Cross-cutting via ServiceDefaults:** telemetry (OpenTelemetry), health checks, resilience, and service discovery are added uniformly by `AddServiceDefaults()`.
- **The dashboard is the front door** for logs, traces, and health across every process.

## Consequences

**Positive**
- `aspire run` stands up the whole system, dashboard included — the lowest-friction path for a human or an agent.
- Zero cloud spend during development, while exercising the real Service Bus and PostgreSQL surfaces.
- No hardcoded addresses to drift; resources and wiring are declared in one legible place.
- Going live is a topology/DevOps change (point resources at real Azure/managed services), not a code rewrite.

**Negative**
- **No production deployment artifacts yet** — no Dockerfiles per host, no Aspire manifest / `azd` target, no CI. "How does this leave a laptop" is the largest operability gap (60-day plan).
- **Health checks are currently in a Development-only block** and must be promoted to every environment before deploy.
- The **Service Bus emulator surface drifts** between Aspire versions; the emulator binding (not the outbox/inbox pattern) needs verifying against current docs.
- Local topology (one Postgres server, single-instance services) is not the production topology — scale-out concerns (ADR-0003's outbox claim) are deferred.

**Neutral**
- Choosing Aspire ties the orchestration to the .NET ecosystem, which matches the target stack; the same services could be composed differently (Compose/K8s) without changing service code, since nothing depends on Aspire at runtime beyond injected config.

## Alternatives considered

- **Docker Compose for local orchestration.** Workable, but Aspire adds the dashboard, service discovery, health/telemetry defaults, and typed resource wiring for free, and matches the .NET target; Compose remains a viable *deployment* target later.
- **Point dev at real cloud resources.** Rejected: cost, setup friction, and it breaks the "one command, offline" goal.
- **A different local broker (RabbitMQ container) instead of the Service Bus emulator.** Rejected: it would mean coding to a different SDK than production, undermining the "real SDK, local transport" goal of ADR-0002.
