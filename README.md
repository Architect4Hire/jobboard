# JobBoard

A job-board platform built as **Aspire + ASP.NET Core microservices + Angular**, developed hand-in-hand with **Claude Code**. Everything runs locally: Aspire's AppHost orchestrates every service, the gateway, the Angular app, and all backing resources (PostgreSQL with a database per service, the Azure Service Bus emulator, a cache) as local containers — no cloud dependencies.

This repo is two things at once: a genuinely good microservice app, and a reproducible demonstration of driving a multi-service .NET stack agentically with Claude Code.

## What's in the box (right now)

This is the **toolkit and plan**, ready to generate `src/`:

```
.
├── CLAUDE.md               # project constitution (SCRUB) — auto-loaded by Claude Code every session
├── .claude/                # the Claude Code toolkit: skills, subagents, hooks, rules, settings
├── docs/
│   ├── README.md           # docs index
│   └── scrub-prompts.md    # the ordered SCRUB prompts that scaffold the whole system
├── Directory.Build.props   # shared build settings (net10.0, nullable, warnings-as-errors)
├── Directory.Packages.props# central package management (versions added at scaffold time)
├── global.json             # SDK band pin (roll-forward friendly)
├── .editorconfig           # style + naming, mirrors .claude/rules/backend.md
├── .vscode/                # recommended extensions + workspace settings
├── .gitignore
└── README.md
```

`src/` does not exist yet — you create it by running the prompts in `docs/scrub-prompts.md`. New projects scaffolded under `src/` automatically inherit `Directory.Build.props` and central package management.

## Architecture at a glance

- **Gateway** (YARP) — the only public door; the Angular app talks to nothing else.
- **Services** — `Identity`, `Jobs`, `Applications`, `Profiles`, `Notifications`. Each is a **thin host** + a **`.Core`** class library (facade → business → data → repository), with **its own database**. No shared database, ever.
- **Shared** — `JobBoard.Shared` (cross-cutting mechanism: base context, base repository, outbox/inbox, dispatcher, processor host, exception handler, cache) and `JobBoard.Contracts` (integration-event records — the only shared *contract*).
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
