# Job Board

A **reference-architecture** build: a job-board platform where employers post jobs, candidates apply, and everyone is notified — built to demonstrate an event-driven **microservice** system on **Aspire + ASP.NET Core + Angular (.NET 10)**.

JobBoard does two things at once. It's a genuinely functional job board, and it's a public, end-to-end demonstration of driving a _multi-service_ stack with Claude Code using my **SCRUB-driven framework**. The application is the proof; the framework is the point.

## What "reference architecture" means here

This is a prototypical build, not a product. It exists to prove one thing: that an event-driven microservice system can be designed to behave correctly when things go wrong — not just when the demo goes right — and that the whole thing can be driven agentically without the architecture drifting.

Concretely, that means it's:

- **Prototypical, not bespoke.** Five bounded services behind a single gateway, each a thin ASP.NET Core host plus a `.Core` library, each owning its own PostgreSQL database. The patterns are meant to be lifted and reused, not admired once.
- **Correct under failure by design.** A hand-rolled transactional outbox, an idempotent inbox, at-least-once delivery, retries, and dead-lettering are first-class parts of the build — not features bolted on later.
- **Local by default, honest about it.** The whole system runs on Aspire-orchestrated local containers at zero cloud spend, but talks to the real Azure Service Bus SDK through an emulator. Going live is a configuration and DevOps change, not a rewrite.
- **Boundary-safe.** One-way, acyclic references (`Contracts ← Shared ← .Core ← host ← AppHost`); no shared database, ever; the gateway is the only public door.

## Built with the SCRUB framework

The whole system was scaffolded and is maintained through **SCRUB** — a structured prompt skeleton that keeps Claude Code inside the architecture instead of improvising around it. Every non-trivial instruction is shaped as:

```
SCOPE:        what to build/change + which service/project it touches
CONSTRAINT:   the rules to honor (stack, conventions, plan-first)
RESTRICTION:  explicit "do NOT" guardrails
USAGE:        which skills / subagents / tools to use
BEHAVIOR:     how to proceed — plan, approve, small steps, test, report
```

SCRUB isn't just a prompt style — it's wired into the repo. The reusable toolkit lives in `.claude/`:

- **Rules** (`aspire.md`, `backend.md`, `frontend.md`, `messaging.md`, `gateway.md`) — the standing constraints every session loads.
- **Skills** (`add-endpoint`, `add-component`, `add-aspire-resource`) — encoded procedures for the recurring moves, so common work needs no bespoke prompt.
- **Subagents** (`code-reviewer`, `test-gap-analyzer`, `api-contract-checker`) — automated guardrails on the high-stakes checks.
- **Hooks** — enforcement at the seams.
- **`CLAUDE.md`** — project memory, itself written as a SCRUB prompt, loaded every session, so every rule has one obvious home and every misstep is diagnosable by section.

The build sequence follows the same discipline. Because this is a _microservice_ system, the sequence stands up the shared spine first, proves **one** service and the **whole messaging loop** end-to-end, and only then fans the proven pattern out to the rest. The golden rule: don't fan out to five services until one service and the full event loop actually work.

Once the system is scaffolded, most day-to-day work needs no bespoke prompt — the rules, skills, subagents, and hooks carry the structure, so short instructions are enough. Reach for a full SCRUB prompt only when a task is non-trivial or crosses the bus; and when you reuse one two or three times, promote it to a skill so you stop needing the prompt at all.

## Architecture at a glance

- **Gateway** (YARP) — the only public door; the Angular app talks to nothing else.
- **Services** — `Identity`, `Jobs`, `Applications`, `Profiles`, `Notifications`. Each is a **thin host** + a **`.Core`** class library (facade → business → data → repository), with **its own database**. No shared database, ever.
- **Shared** — `JobBoard.Shared` (cross-cutting mechanism: base context, base repository, outbox/inbox, dispatcher, processor host, exception handler, cache) and `JobBoard.Contracts` (integration-event records — the only shared _contract_).
- **Messaging** — Azure Service Bus (emulator in dev) with a **hand-rolled transactional outbox**; consumers are idempotent via an inbox.

The full ruleset is in `CLAUDE.md` and `.claude/rules/`.

## Architecture diagrams

> Rendered with [Mermaid](https://mermaid.js.org/) — GitHub renders these inline. The C4 views
> (Context and Container) are drawn as **styled flowcharts** rather than Mermaid's native `C4Context`/ `C4Container` types, which are experimental and don't render on GitHub — the flowchart versions carry
> the same C4 semantics (people, systems, containers, boundary) and render everywhere. The diagrams
> describe the target system the SCRUB prompts build, not the current (toolkit-only) state of `src/`.

### C4 — System Context

Who uses JobBoard and what it depends on beyond its own boundary.

```mermaid
flowchart TB
    candidate["CandidatePersonSearches and applies for jobs"]:::person
    employer["EmployerPersonPosts jobs, reviews applicants"]:::person
    jobboard["JobBoardSoftware SystemPost jobs, apply, get notified"]:::system
    email["Email deliveryExternal SystemSMTP / provider (local in dev)"]:::external

    candidate -->|"Browses, applies, tracks status · HTTPS"| jobboard
    employer -->|"Posts & manages jobs · HTTPS"| jobboard
    jobboard -->|"Sends notifications · SMTP"| email

    classDef person fill:#08427b,stroke:#052e56,color:#ffffff
    classDef system fill:#1168bd,stroke:#0b4884,color:#ffffff
    classDef external fill:#999999,stroke:#6b6b6b,color:#ffffff
```

### C4 — Container

The runtime pieces inside the boundary. Each service is a thin ASP.NET Core host plus its `.Core` library, and owns its own PostgreSQL database. The gateway is the only public door; services talk to
each other only over Service Bus.

```mermaid
flowchart TB
    candidate["CandidatePerson"]:::person
    employer["EmployerPerson"]:::person
    email["Email deliveryExternal · SMTP / provider"]:::external

    subgraph JB["JobBoard"]
        spa["Web AppAngular SPATalks only to the gateway"]:::container
        gw["API GatewayYARPOnly public door · validates JWT · routes by resource name"]:::container

        identity["Identity ServiceASP.NET Core host + .CoreAccounts, JWT issuance"]:::container
        identitydb[("identitydbPostgreSQL")]:::db
        jobs["Jobs ServiceASP.NET Core host + .CoreJob postings, search"]:::container
        jobsdb[("jobsdbPostgreSQL")]:::db
        apps["Applications ServiceASP.NET Core host + .CoreApplications + status"]:::container
        appsdb[("applicationsdbPostgreSQL")]:::db
        profiles["Profiles ServiceASP.NET Core host + .CoreCandidate & employer profiles"]:::container
        profilesdb[("profilesdbPostgreSQL")]:::db
        notif["Notifications ServiceASP.NET Core · event-onlyConsumes events, sends email"]:::container
        notifdb[("notificationsdbPostgreSQL")]:::db

        bus["Azure Service BusEmulator (local container)Integration events"]:::queue
        cache["CacheRedisRead-through cache"]:::container
    end

    candidate -->|HTTPS| spa
    employer -->|HTTPS| spa
    spa -->|"HTTPS / JSON"| gw
    gw -->|routes| identity
    gw -->|routes| jobs
    gw -->|routes| apps
    gw -->|routes| profiles

    identity --> identitydb
    jobs --> jobsdb
    apps --> appsdb
    profiles --> profilesdb
    notif --> notifdb

    jobs -->|"JobPosted / JobClosed · via outbox"| bus
    apps -->|"pub/sub; consumes JobClosed"| bus
    bus --> notif
    jobs -->|caches lists| cache
    notif -->|SMTP| email

    classDef person fill:#08427b,stroke:#052e56,color:#ffffff
    classDef container fill:#438dd5,stroke:#2e6295,color:#ffffff
    classDef db fill:#438dd5,stroke:#2e6295,color:#ffffff
    classDef queue fill:#438dd5,stroke:#2e6295,color:#ffffff
    classDef external fill:#999999,stroke:#6b6b6b,color:#ffffff
    style JB fill:#ffffff,stroke:#2e6295,stroke-width:2px,stroke-dasharray:6 4,color:#333333
```

### Sequence — Submit an application (write + outbox + async notify)

The synchronous request path commits the domain row **and** the outbox row in one transaction; the
dispatcher relays to Service Bus afterward; the consumer dedupes via its inbox.

```mermaid
sequenceDiagram
    autonumber
    actor C as Candidate
    participant SPA as Angular SPA
    participant GW as Gateway (YARP)
    participant CT as Applications · Controller
    participant FA as Facade
    participant BU as Business
    participant DA as DataLayer
    participant RE as Repository + applicationsdb
    participant OB as OutboxMessages
    participant DI as OutboxDispatcher
    participant SB as Service Bus
    participant NO as Notifications · Processor
    participant IN as Inbox + notificationsdb

    C->>SPA: Apply to a job
    SPA->>GW: POST /applications (JWT)
    GW->>CT: forward (token valid)
    CT->>FA: SubmitApplication(viewModel)
    FA->>FA: validate
    FA->>BU: submit
    BU->>BU: apply rules, build ApplicationSubmitted
    BU->>DA: persist(app, event)
    rect rgb(230,244,255)
    Note over DA,OB: single transaction
    DA->>RE: insert application
    DA->>OB: enqueue event (IOutbox)
    end
    RE-->>BU: saved entity
    BU-->>FA: ServiceModel
    FA-->>CT: ServiceModel
    CT-->>SPA: 201 Created
    Note over DI,SB: asynchronous, after commit
    DI->>OB: poll unprocessed
    DI->>SB: send (MessageId = row id)
    DI->>OB: mark processed
    SB->>NO: deliver ApplicationSubmitted
    NO->>IN: dedupe via inbox, record id, send email
```

### Sequence — Close a job (cross-service cascade)

One event, two databases, no shared table. The consumer writes only its own store and is idempotent.

```mermaid
sequenceDiagram
    autonumber
    actor E as Employer
    participant GW as Gateway
    participant J as Jobs Service
    participant SB as Service Bus
    participant AC as Applications · JobClosedConsumer
    participant DB as Inbox + applicationsdb

    E->>GW: POST /jobs/{id}/close (JWT)
    GW->>J: forward
    J->>J: close job + enqueue JobClosed (same tx)
    J-->>GW: 200 OK
    Note over J,SB: dispatcher relays after commit
    J->>SB: JobClosed
    SB->>AC: deliver JobClosed
    AC->>DB: check inbox (dedupe)
    alt first delivery
        AC->>DB: close open applications + record message id
        AC->>SB: ApplicationStatusChanged (via its own outbox → dispatcher)
    else duplicate delivery
        AC->>AC: no-op (already processed)
    end
```

### Conceptual — System layers

A logical view of the concerns, independent of deployment. Each application service is a bounded
context; integration is always through the bus, never a shared database.

```mermaid
flowchart TB
    subgraph P["Presentation"]
        SPA["Angular SPA — talks only to the gateway"]
    end
    subgraph E["Edge"]
        GW["API Gateway (YARP) — JWT validation, routing"]
    end
    subgraph S["Application services (bounded contexts)"]
        ID["Identity"]
        JB["Jobs"]
        AP["Applications"]
        PR["Profiles"]
        NT["Notifications — event-only"]
    end
    subgraph I["Integration"]
        OI["Outbox / Inbox — per service"]
        BUS["Azure Service Bus (emulator)"]
    end
    subgraph D["Persistence — database per service"]
        DBS["identitydb · jobsdb · applicationsdb · profilesdb · notificationsdb"]
        CACHE["Redis cache"]
    end
    subgraph X["Cross-cutting"]
        SD["ServiceDefaults — telemetry, health, resilience, discovery"]
        SH["JobBoard.Shared (mechanism) · JobBoard.Contracts (events)"]
    end

    SPA --> GW --> S
    S --> OI --> BUS
    S --> DBS
    JB --> CACHE
    S -. uses .-> SD
    S -. builds on .-> SH
```

### Conceptual — Per-service layering & project boundaries

Inside one service: a thin host (entry points + composition root) over a `.Core` library (facade →
repository), both built on `JobBoard.Shared`. References point one way: `Contracts ← Shared ← .Core ← host`.

```mermaid
flowchart LR
    subgraph HOST["Service host — thin"]
        direction TB
        CTL["Controllers"]
        CON["Consumers — IIntegrationEventConsumer"]
        PROG["Program.cs — composition root"]
    end
    subgraph CORE["Service .Core — facade to repository"]
        direction TB
        FAC["Facade — validate + cache"]
        BSN["Business — rules + build event + map"]
        DLR["DataLayer — compose + enqueue outbox (1 tx)"]
        REP["Repository — EF queries"]
        MOD["Models: ViewModel / Domain / ServiceModel + Validators + Mappers"]
    end
    subgraph SHARED["JobBoard.Shared — mechanism"]
        direction TB
        BCTX["Base DbContext + base Repository (ExecuteInTransactionAsync)"]
        OBX["IOutbox / IInbox"]
        DPX["OutboxDispatcher + Processor host"]
        XC["Exception handler · Cache · Error shape"]
    end
    CTR[["JobBoard.Contracts — integration events"]]
    DB[("service database")]
    SBUS(("Service Bus"))

    CTL --> FAC
    CON --> FAC
    FAC --> BSN --> DLR --> REP
    REP --> DB
    REP -. inherits .-> BCTX
    DLR -. writes .-> OBX
    BSN -. builds .-> CTR
    OBX --> DPX --> SBUS
    HOST --> CORE --> SHARED --> CTR
    CORE --> CTR
```

## Getting started

1. Prerequisites: the .NET 10 SDK, the Aspire CLI, Node.js, and a container runtime (Docker/Podman).
2. Make the hooks executable: `chmod +x .claude/hooks/*.sh`
3. Open Claude Code and run the prompts in `docs/scrub-prompts.md` **in order**, one at a time — they stand up the shared spine, prove one service and the full event loop, then fan out.
4. Once `src/` exists, run the whole system with the dashboard:

```
aspire run
```

> Verify exact Aspire and Azure Service Bus emulator commands/API names against [https://aspire.dev](https://aspire.dev) — the framework ships fast and some surface moves between versions.

## License

MIT.
