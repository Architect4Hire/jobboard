# ADR-0009: Fail-open read-through cache with generation-token invalidation

- **Status:** Accepted
- **Date:** 2026-07-18
- **Deciders:** Robert Felkins
- **Related:** ADR-0005 (layering — the facade owns caching), `docs/high-level-design.md` §8.4
- **Implements:** `JobBoard.Jobs.Core/Facade/JobFacade.cs`, `JobBoard.Shared/Caching/{ICache,RedisCache}.cs`

## Context

The job list is the hottest read path and is **filterable by category**, so there is a *family* of cache entries (one per `?category=` value, plus the unfiltered "all"). Two problems follow: (1) invalidating the whole family on a write, given only a simple `Remove(key)` cache primitive, and (2) deciding what happens when the cache itself is unavailable. A cache that becomes a *source of truth* — where a Redis blip fails the read, or stale entries linger with no bound — is worse than no cache.

## Decision

**We will cache the job list with a fail-open, read-through strategy in the Jobs facade, and invalidate the whole entry family at once using a generation token.**

- **Read-through, in the facade.** The facade (the layer that owns the outbound-caching seam, ADR-0005) checks the cache, falls back to the business/data path on a miss, and writes back. Business and data code never touch the cache.
- **Generation-token invalidation.** Every list entry is namespaced under a `jobs:list:<generation>:<category>` key. A write (`PostAsync`/`CloseAsync`) drops the single generation key, which orphans **every** variant at once; the next read mints a fresh generation. This invalidates the whole family with the one `Remove` primitive available.
- **Backstop TTL.** Orphaned entries lapse via a 5-minute TTL; explicit invalidation is primary, the TTL only bounds how long a stale orphan lingers.
- **Fail-open.** The cache is an optimisation, not truth. A read error degrades to serving from source; a write-back or invalidation error is logged and swallowed (the domain write and its outbox row have already committed) — entries lapse via TTL regardless. The **repository query stays authoritative**; the facade never filters ServiceModels itself.

## Consequences

**Positive**
- Fast list reads without making the cache load-bearing: a Redis outage degrades performance, never correctness.
- One cheap operation (drop the generation key) invalidates an unbounded family of filtered entries — no key enumeration needed.
- Caching is isolated to one seam (the facade), so the rest of the stack is cache-oblivious.

**Negative**
- **Brief staleness window** is possible: a fail-open invalidation that errors leaves old entries until the TTL reaps them (bounded to 5 minutes). Acceptable for a job list; would not be for balances or inventory.
- Generation indirection adds a small read cost (resolve the generation, then the entry) and a subtle mental model to maintain.
- Only Jobs is wired to the cache today; extending the pattern to another service means repeating the discipline (or promoting it to a shared helper) deliberately.

**Neutral**
- The `ICache` abstraction keeps Redis swappable; the pattern is storage-agnostic.

## Alternatives considered

- **Cache-aside in the controller or business layer.** Rejected: it spreads cache concerns across layers and violates the "facade owns outbound caching" rule (ADR-0005).
- **Per-entry explicit invalidation (delete each `category` key on write).** Rejected: requires enumerating or tracking the live key set — more state and more ways to miss one; the generation token invalidates the whole family atomically.
- **Make the cache authoritative / fail-closed.** Rejected outright: turns an optimisation into a new single point of failure; fail-open is the deliberate posture.
- **No cache.** Viable at low load, but the list is the hot path and the fail-open design makes the cache a safe, pure win.
