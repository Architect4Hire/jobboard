# ADR-0005: Thin host + `.Core` layered library; one-way acyclic references

- **Status:** Accepted
- **Date:** 2026-07-18
- **Deciders:** Robert Felkins
- **Related:** ADR-0001 (services), ADR-0010 (contracts leaf), `docs/design/high-level-design.md` §5, `CLAUDE.md`, `.claude/rules/backend.md`

## Context

Each service needs an internal structure that is (a) consistent across all five services, (b) legible enough that an agent can extend it without a bespoke prompt, and (c) strict enough that logic can't drift into the wrong place. Two failure modes are common: fat controllers/`Program.cs` that accrete business logic, and tangled project references that let any layer depend on any other, making boundaries unenforceable. The structure must also make the outbox discipline (ADR-0003) mechanical — there must be one obvious place where the domain write and the outbox enqueue share a transaction.

## Decision

**We will build each service as a thin host project plus a `.Core` class library, with a fixed layer stack and strictly one-way, acyclic project references.**

- **Two projects per service.** `JobBoard.<Service>` (host: Controllers, Consumers, `Program.cs` composition root — entry points only) and `JobBoard.<Service>.Core` (the whole `facade → business → data layer → repository` stack plus models/validators/mappers). EF Core / Npgsql live only in `.Core`.
- **Layer responsibilities are fixed:** Facade = validate (in) + cache (out); Business = rules, build the event, map; Data layer = compose repo calls + enqueue the outbox atomically; Repository = EF queries only (inherits `BaseRepository<TContext>`).
- **Boundary types:** only **ViewModels** enter, only **ServiceModels** leave; EF entities never cross a controller; a service's Domain type never crosses a *service* boundary.
- **Reference direction is one-way and acyclic:** `Contracts ← Shared ← <Service>.Core ← <Service> (host) ← AppHost`; hosts and the gateway also reference `ServiceDefaults`. A host references *its own* `.Core` and never another service's. A reference pointing the other way, or sideways between two services, means the design is wrong.
- **`JobBoard.Shared` holds cross-cutting *mechanism* only** (base context/repository, outbox/inbox, dispatcher/processor host, exception handler, cache, error shape) — never any service's domain, business, or data.

## Consequences

**Positive**
- Every service looks the same, so a fix or feature in one is transferable to all, and an agent can navigate by convention.
- Boundaries are enforced by the compiler (project references) and by type discipline (ViewModel in / ServiceModel out), not by good intentions.
- The outbox atomicity has one obvious home (the data layer's `ExecuteInTransactionAsync` block), which makes the ADR-0003 guarantee reviewable at a glance.
- Recurring moves are encodable as skills (`add-endpoint`), so common work needs no bespoke prompt.

**Negative**
- More projects and more mapping (ViewModel ↔ Domain ↔ ServiceModel) than a flat service — deliberate ceremony traded for boundary safety.
- The layering is a standing discipline; a shortcut (EF entity returned from a controller, a cache call in business) is a violation that must be caught in review.

**Neutral**
- The split mirrors a classic hexagonal/clean intent (host = adapters/composition; `.Core` = application + domain) without importing a specific framework's vocabulary.

## Alternatives considered

- **Single project per service (flat).** Rejected: nothing stops logic leaking into controllers, and there's no compiler-enforced boundary between entry points and domain.
- **Shared "common domain" library across services.** Rejected: it becomes a coupling magnet — a change to shared domain code ripples across services, defeating ADR-0001. `Shared` is *mechanism only*; anything domain-shaped belongs in a service's `.Core`.
- **Vertical-slice / MediatR handlers instead of the layered stack.** A reasonable alternative, but the explicit layer names make the outbox seam and the ViewModel/ServiceModel boundary unmistakable, which serves the agentic-legibility goal better here.
