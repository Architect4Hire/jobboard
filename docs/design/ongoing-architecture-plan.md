# JobBoard — Architecture Review & 30/60/90 Plan

*A brutal, grounded first-pass review of the current spike. Physical, logical, and conceptual — service by service, seam by seam, backend and UI. Benchmarked against Microsoft guidance (Aspire docs, the .NET microservices / Cloud-Native eBook, the eShop reference app) and industry practice (Richardson / microservices.io patterns, Newman's *Building Microservices*, OWASP API Security Top 10).*

**Reviewed:** every `.cs` under `src/` (hosts + `.Core` + Shared + Contracts + AppHost + Gateway), the YARP config, ServiceDefaults, the full Angular app under `src/web`, and all six test projects.
**Date:** 2026-07-18 · **Status:** spike / first pass · **Branch:** `main`

---

## 0. Verdict up front

This is **not a scaffold** — it is a genuinely well-engineered spike that most teams would be happy to call a v1. The layering is followed strictly and consistently across all five services, the transactional-outbox / idempotent-inbox mechanism is *correctly* implemented (not cargo-culted), contracts are drift-free, and the frontend is idiomatic modern Angular with real accessibility and security thought. The scaffolding-toolkit thesis in `CLAUDE.md` is validated by the output.

So the "brutal" part is not "this is bad." It is: **the code is good enough that the gaps which remain are now the important ones**, and they are not cosmetic — they are a security seam (broken object-level authorization), an unimplemented core capability (Notifications sends no email), and the entire "how does this leave a laptop" story (deployment, CI, scale-out, edge hardening). A spike earns the right to be judged as a product, and by that bar it has real holes.

### Scorecard

| Area | Grade | One-line |
|---|---|---|
| Conceptual (bounded contexts, event model) | **A−** | Boundaries are right; event set is coherent; one soft boundary in Notifications. |
| Logical (layering, contracts, messaging) | **A** | Strict, consistent, correct. The strongest dimension. |
| Physical (Aspire topology, data, deploy) | **B−** | Local topology exemplary; **no deployment/CI/scale-out story at all**. |
| Backend services | **A−** | Four of five fully real and sound; Notifications logs but never emails. |
| Frontend / UI | **A−** | Idiomatic, strict, secure-by-default; token-in-localStorage + role-guard gaps. |
| Security | **C+** | Good crypto primitives, **but a real BOLA/IDOR seam and coarse authz**. |
| Observability | **B** | Aspire OTel wired; **traces don't reach the bus or the DB**. |
| Testing | **B+** | The hard guarantees are genuinely tested; plumbing has gaps. |
| **Overall** | **B+ / A−** | Excellent bones; ship-blockers are security + operability, not design. |

---

## 1. Conceptual architecture — are the boundaries right?

**Mostly yes, and this is the part hardest to fix later, so it matters most.**

The five bounded contexts map cleanly to real business capabilities and each owns its own data (`identitydb`, `jobsdb`, `applicationsdb`, `profilesdb`, `notificationsdb` in `AppHost.cs:7-11`). This is textbook *database-per-service* (Richardson) and *bounded-context-per-service* (Newman/Evans). Cross-context communication is exclusively via past-tense integration events (`JobPosted`, `JobClosed`, `ApplicationSubmitted`, `ApplicationStatusChanged`) — facts, not commands — which is the correct event-driven posture. Reference data is duplicated rather than shared (a candidate/employer id travels *in* the event), which is the right call and matches the eShop guidance against chatty synchronous callbacks.

**What's good conceptually:**
- The decomposition is by *capability*, not by entity or by layer — the failure mode of most "microservices" first attempts. Identity, Jobs, Applications, Profiles, Notifications are each independently deployable and independently reasoned-about.
- `JobBoard.Contracts` is a true leaf (references nothing) and holds *only* events. Statuses cross as **strings**, not shared enums (`ApplicationMappers.cs:47-58`), so no service's domain type leaks into the shared contract. This is a subtle, correct discipline that most teams get wrong.
- No shared domain code, no shared database, no synchronous service-to-service calls. The one-way acyclic reference rule (`Contracts ← Shared ← .Core ← host ← AppHost`) holds with zero violations.

**Where the concept is soft:**
- **Notifications is a boundary that doesn't yet do its job.** Conceptually it "consumes events and sends email." Today it consumes events and writes a `NotificationLog` row — the email is rendered (`NotificationMappers.cs`) and then *never sent*. There is no email client, no delivery attempt, no retry/outbox-for-delivery. `notificationsdb` was specified as "outbox + delivery log"; only the log half exists. So the context is real as an *audit log of intended notifications*, but the capability it names is unimplemented. This is the single biggest spec-vs-reality gap.
- **Applications owns two related-but-distinct lifecycles** (submission and the status state-machine) plus a reactive close-cascade. That's fine at this size, but the `JobClosed` cascade (below) is where the one real concurrency bug lives — a sign that the reactive part of this context deserves the most design care as it grows.
- **No read-model / query-composition strategy yet.** The moment a screen needs "my applications, with the job title and the employer name," you have three services' data. Today the frontend stitches it client-side (it already carries denormalized ids). That's acceptable now, but there is no articulated position on API composition vs. CQRS read models — and that decision shapes the next six months. Name it before it's forced.

---

## 2. Logical architecture — layering, contracts, messaging

**This is the strongest dimension of the whole codebase. Grade: A.**

The mandated stack — Controller/Consumer → Facade (validate + cache) → Business (translate, rules, build event, map) → Data layer (compose repo calls + enqueue outbox atomically) → Repository (EF only) — is followed **strictly and consistently across all five services**, with the boundary discipline intact: only ViewModels enter, only ServiceModels leave, no EF entity crosses a controller, no domain entity crosses a *service* boundary. The audit found **no** hard layer-leakage violations (no EF in a facade, no cache in business, no bus-send outside the outbox, no logic in a controller).

### The transactional outbox / idempotent inbox — verified correct

This is the load-bearing pattern for a microservice system, and it is one of the most-frequently-botched. Here it is right:

- **Atomicity is real, not aspirational.** `BaseRepository.ExecuteInTransactionAsync` (`BaseRepository.cs:25-45`) runs the domain write and the outbox insert on the *same scoped `BaseDbContext`* inside one execution-strategy-owned transaction. The critical bridge — `TryAddScoped<BaseDbContext>(sp => sp.GetRequiredService<TContext>())` in `SharedServiceCollectionExtensions.cs:38` — is what makes the outbox row enlist in the same transaction as the domain rows. Miss that one line and the whole guarantee silently evaporates; it's present and correct. Verified in `JobDataLayer.AddAsync`/`CloseAsync` (`JobDataLayer.cs:32-75`) and `ApplicationDataLayer` (`:39-101`).
- **At-least-once is honest.** `OutboxRelay` (`OutboxRelay.cs:41-81`) is the *only* Service Bus sender, polls oldest-first, sends-then-stamps, and `break`s on first failure to preserve ordering. Send-before-stamp means a crash re-sends the same `MessageId` — which the consumer's inbox dedupes.
- **Idempotency is transactional.** Every consumer does inbox-check + side-effect + mark-processed in **one** transaction (`ApplicationDataLayer.CloseOpenApplicationsForJobAsync:103-141`, `NotificationDataLayer.RecordAsync:18-31`). Replays no-op. This is the Richardson *idempotent consumer* pattern implemented correctly.
- **Replay-safety on the write side too:** `Outbox.EnqueueAsync` and `Inbox.MarkProcessedAsync` both `FindAsync` by deterministic id first, so an execution-strategy retry re-stages rather than duplicates.

Async hygiene is clean: **zero** `.Result` / `.Wait()` / `.GetAwaiter().GetResult()` across `src`.

### Where the logical model has warts (all narrow)

1. **One real concurrency bug — spurious `ApplicationStatusChanged`.** In the `JobClosed` cascade, `CloseOpenApplicationsForJobAsync` (`ApplicationDataLayer.cs:119-136`) snapshots active applications, runs a conditional close, then re-queries which snapshot ids are now `Rejected` and emits an event per id. If a snapshot app was *concurrently* moved to `Rejected` by its own normal advance (which already emitted its own event) in the window between snapshot and read, this handler emits a **second, spurious** `ApplicationStatusChanged` with a `FromStatus` from the stale snapshot. It carries a fresh event id, so the inbox does *not* dedupe it — a genuine duplicate/incorrect notification. Very narrow, but it's a correctness wart. Fix by emitting only for rows *this* transaction's `ExecuteUpdate` actually flipped (use the affected-row set, not a re-query).
2. **Event-construction location is inconsistent.** Most flows build the event once in business and capture it as a parameter (stable id across retries). The Applications cascade instead invokes a `buildEvent` delegate *inside* the data layer (`ApplicationDataLayer.cs:134`), rebuilding per call — safe only because a replayed attempt rolled back first. Honors the rule in spirit; violates it in mechanism. Normalize to "business builds once, data layer persists."
3. **Poison messages rely on the broker default.** An unknown `Subject` or a persistently-throwing consumer leaves the message unsettled forever (`ServiceBusProcessorHost.cs:45-49` completes only on success), falling through to Service Bus's default `MaxDeliveryCount` → DLQ. No custom dead-letter handling, no alert. Acceptable default; know that it's the default.
4. **Silent subscription failure.** If a processor fails to open, the host logs and continues (`ServiceBusProcessorHost.cs:62-66`) — that consumer *silently never runs*. A subscription-name mismatch between a host and `AppHost.cs:33-48` would manifest as "events just quietly don't get handled." Names currently all match; add a startup assertion so a future mismatch is loud.

---

## 3. Physical architecture — topology, data, and the missing deployment story

**Local topology: exemplary. Everything beyond localhost: absent. Grade: B−.**

### What's right

- The AppHost (`AppHost.cs`) is the single declarative source of truth: one Postgres server with a database per service, the Service Bus emulator (`RunAsEmulator()`), a Redis cache, five service hosts, the gateway, and the Angular app — all wired with `WithReference`/`WaitFor`, **zero hardcoded connection strings or `localhost:port`**. This is exactly the Aspire model as Microsoft intends it.
- The JWT signing key is injected via env, generated per-run when not supplied (`AppHost.cs:17-18`) — no secret in source.
- Central Package Management (`Directory.Packages.props`), `Directory.Build.props` with `Nullable`, `TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`, an `.editorconfig`, a pinned SDK (`global.json`), and the new `.slnx` solution format. The build hygiene is better than most production repos.

### What's missing — and this is the real physical-architecture gap

- **There is no deployment story whatsoever.** No `Dockerfile`s, no Aspire manifest generation / `azd` / Bicep, no Kubernetes/Container Apps target, no environment promotion. Aspire orchestrates *local dev* beautifully; nothing describes how any of this runs anywhere else. For a demonstration repo that's a defensible line to draw — but it must be a *stated* line, not an omission.
- **No CI/CD.** No `.github/workflows`. `dotnet test` + `ng test` are never run automatically; `TreatWarningsAsErrors` is only enforced on whoever builds locally. First 30-day item.
- **Single-instance assumptions are baked in.** `OutboxRelay.RelayAsync` selects pending rows with **no `FOR UPDATE SKIP LOCKED`** (`OutboxRelay.cs:43-47`). Correct for one replica per service (how the AppHost runs it); scale any publisher to >1 replica and both dispatchers select and send the same rows. At-least-once already tolerates the resulting duplicate sends (same `MessageId`), so it's duplicate-*amplification*, not corruption — but it's the first thing that breaks under horizontal scale.
- **Health checks are dev-only.** `MapDefaultEndpoints` maps `/health` and `/alive` only in Development (`ServiceDefaults/Extensions.cs:113-123`, the stock template). Any real orchestrator needs readiness/liveness probes in every environment.
- **Observability doesn't reach the two places a microservice trace most needs to go.** OTel is wired (`ConfigureOpenTelemetry`) with ASP.NET Core + HttpClient + runtime instrumentation — but **no EF Core / Npgsql instrumentation and no Service Bus / messaging instrumentation**. The stated goal is "follow a request from the gateway through the owning service and *onto the bus*." Today the trace dies at the outbox: you cannot correlate a publish to its consume. Add Npgsql and Service Bus OTel sources, and propagate trace context through the outbox message (a `Diagnostic-Id` / `traceparent` application property). This is the highest-leverage observability fix.

---

## 4. Service-by-service

| Service | Real? | Layering | Events | Verdict |
|---|---|---|---|---|
| **Identity** | ✅ Full | ✅ Clean | none (correct) | Solid. PBKDF2 + HMAC JWT + uniform-401. |
| **Jobs** | ✅ Full | ✅ Clean | publishes `JobPosted`, `JobClosed` | Best service. Correct outbox + cache invalidation. |
| **Applications** | ✅ Full | ✅ Clean | pub 2, consumes `JobClosed` | Solid; one narrow cascade race (§2.1). |
| **Profiles** | ✅ Full | ✅ Clean | none (correct) | Simplest; last-write-wins upsert (fine). |
| **Notifications** | ⚠️ Partial | ✅ Clean | consumes 3 | **Logs but never sends email.** |

**Identity** — Register + login fully real. PBKDF2 via ASP.NET Core `PasswordHasher<T>` (`PasswordHasher.cs`), HMAC-SHA256 JWT (`JwtTokenIssuer.cs`), constant "invalid credentials" for both unknown-email and wrong-password so it doesn't leak which (`AccountBusiness.cs:39-46`), email normalized before the unique lookup. JWT options fail-fast at boot (`ValidateOnStart`). No refresh tokens and no revocation (`jti` is issued but there's no deny-list) — fine for a spike, a 60-day item.

**Jobs** — The most complete service and the reference implementation for everyone else. Outbox-in-transaction verified for both post and close; close publishes *only when a row actually flips* (double-close is a no-op). The **generation-token cache invalidation** (`JobFacade.cs:112-139`) is genuinely clever: one token namespaces every `?category=` variant, so dropping it orphans the whole family at once, and I traced the read/write-back interleaving — a stale write-back lands under an orphaned generation no later read will mint, so **stale data is never served**. Cache is fail-open. This is better than most production caching code.

**Applications** — Submit/withdraw/advance + the `JobClosed` consumer, all real, all with conditional `ExecuteUpdate` guards so a concurrent transition can't be lost. Consumer idempotency is correct. The one real bug in the whole backend lives here (§2.1), plus the event-construction-in-data-layer inconsistency (§2.2).

**Profiles** — Candidate + employer get/upsert, both aggregates, no events (correct). Upsert races map to a retryable 409. Update path is last-write-wins with no rowversion — acceptable for profiles, but the duplicate-key classifier is broad (`IsDuplicateKeyViolation` treats *any* unique violation as the PK conflict, `CandidateProfileRepository.cs:47`); it would misclassify if a second unique index were ever added. A latent `NullReferenceException` exists in the Skills value comparer (`GetHashCode` on a null skill, `CandidateProfileConfiguration.cs:39`) — the validator is currently the only thing keeping nulls out.

**Notifications** — Plumbing is real and idempotent; the *product* is not. Three consumers wired and deduped correctly, but the terminal action is a `NotificationLog` insert — **no email is ever sent** (`NotificationMappers.cs:8-10` is explicit: "in a fuller system"). Against its own charter this is the headline gap.

---

## 5. Seam-by-seam

The interesting failures in microservices live *between* services, so this is where the review bites hardest.

### 5.1 Frontend ↔ Gateway ↔ Service — the identity seam is **broken** 🔴

This is the most important finding in the document.

- The gateway validates the JWT and enforces an `authenticated` policy on protected routes (`Gateway/Program.cs:34-35`), then proxies. **But its YARP config adds no transform to forward the `sub`/`role` claims** to the downstream service — there is no header injection anywhere in the config.
- Meanwhile the write ViewModels carry the actor's own identity **in the request body**: `PostJobViewModel.EmployerId`, `SubmitApplicationViewModel.CandidateId`. The Angular client fills these from its decoded token (`post-job-form.ts:56`, `job-detail.ts:80`) — and `Session` itself notes it "is not an authorization boundary" (`session.ts:18`).
- Net: **a service has no way to know who the caller actually is, and trusts the id in the body.** A candidate can submit an application as any `candidateId`; an authenticated user can post a job as any `employerId`. This is **OWASP API #1 — Broken Object-Level Authorization (BOLA/IDOR)**. The frontend being honest doesn't matter; the API is directly reachable through the gateway with an arbitrary body.
- **Fix:** the gateway must project the validated `sub` (and `role`) into a signed/trusted header via a YARP transform, and each service must derive the actor from that header — never from the body. Remove `EmployerId`/`CandidateId` from inbound ViewModels entirely. This is a 30-day, ship-blocking item.

### 5.2 Gateway authorization is coarse 🟠

Every protected route uses one policy: `authenticated` (`Gateway/Program.cs:34`, `appsettings.json` routes). Roles are *issued* (`role` claim in the JWT, `JwtTokenIssuer.cs:40`) but **never authorized against** — there is no employer-only or candidate-only policy. So:
- `POST /jobs/**` has **no auth policy at all** (`appsettings.json` `jobs` route) — posting a job is currently *public*. Anyone can create a job with no token.
- An authenticated candidate can call employer-only routes and vice-versa; the gateway won't stop them (the Angular nav hides the links, but the route is reachable).

Add `employer` / `candidate` authorization policies keyed on the `role` claim and apply them per route. Pair with §5.1 (the role must come from the token, enforced at the edge).

### 5.3 Gateway is missing standard edge cross-cutting 🟠

`gateway.md` names rate limiting and CORS-for-the-Angular-origin as edge concerns — **neither is configured.** No `AddRateLimiter`, no `AddCors`. In dev the Angular `proxy.conf.js` makes it same-origin so CORS is masked; in any split-origin deployment the browser will block every call. Also: the gateway takes a `WithReference(serviceBus)` (`AppHost.cs:102`) it never uses — dead coupling; drop it.

### 5.4 Service ↔ Bus and Bus ↔ Consumer — solid (see §2)

The outbox (send) and inbox (receive) seams are the best-built part of the system. The only seam-level caveats are the scale-out outbox locking (§3) and poison-message DLQ (§2.3) — both acceptable-for-now, both known.

### 5.5 Service ↔ Database — clean

Database-per-service holds with zero violations; each context derives from `BaseDbContext` and is registered via the Aspire Npgsql integration keyed to its resource. Conflict-handlers depend on unique indexes that all actually exist (`Slug`, `(CandidateId, JobId)`, `Email`, profile PKs).

### 5.6 Contract seam — drift-free ✅

A dedicated drift pass found **zero** mismatches on both axes: every ServiceModel/ViewModel matches its Angular interface field-for-field (names, types, nullability, enum encoding), and every integration event matches both its publisher's construction site and every consumer's field usage. This is a real strength — and it's currently held together by *discipline*, not by a *check* (see §7).

---

## 6. Frontend / UI

**Idiomatic, strict, secure-by-default. Grade: A−.** Angular 22, all standalone, all `OnPush`, signals throughout, HTTP confined entirely to services (zero `HttpClient` in any component), no leaked subscriptions (`toSignal` / `takeUntilDestroyed` everywhere), strict TS with `strictTemplates` and then some, Vitest, lazy routes. The gateway base URL is never hardcoded — it's an injected `API_BASE_URL` token mounted same-origin and forwarded by `proxy.conf.js` from Aspire config, which even fails loud if the injection is missing. Real accessibility (`role="alert"`, `aria-label`, semantic landmarks, skeleton loaders, double-submit guards).

**What to fix:**
- **JWT in `localStorage`** (`token-store.ts:52`) — persistent XSS-token-theft surface. Angular's default escaping keeps the *active* surface small (no `innerHTML`/`bypassSecurityTrust*`), so it's latent, not live — but it should be a *documented, conscious* decision, and ideally move to an httpOnly cookie or in-memory + silent refresh.
- **Route guards are authenticated-only, not role-aware** (`auth.guard.ts`, `app.routes.ts:20,30`) — a candidate can navigate to `/jobs/new`. Add a `roleGuard`. (Belt to §5.2's braces; the server is the real gate.)
- **Silent enum-drift risk** (`job.model.ts:2`) — the models assume *numeric* enums because no `JsonStringEnumConverter` is registered. True today; the day a backend dev adds one (near-ubiquitous), every `=== JobStatus.Open` breaks with no compile error. Make it loud (§7).
- **Form a11y** — reactive-form errors aren't wired via `aria-invalid`/`aria-describedby`; no focus management on navigation.
- **Missing specs** — `ApplicationService`, `ProfileService`, `TokenStore` have no `.spec.ts` (the others do).

---

## 7. Testing posture

**The hard guarantees are genuinely tested; the plumbing around them isn't. Grade: B+.**

The parts of a microservice system that are usually faked in tests are here proven for real: every publishing service (`Jobs`, `Applications`, `Notifications`) has **real SQLite-backed rollback proofs** that a forced outbox/inbox failure leaves *neither* the domain row *nor* the message row committed, and `JobClosedConsumerTests` delivers the same event twice through the full stack and asserts a single effect. This is exactly the guarantee the architecture rests on, and it is tested end-to-end.

**Highest-value gaps, ranked:**
1. **`ServiceBusProcessorHost` has zero tests** — the receive-side host that turns "at-least-once" into a runtime guarantee (complete-only-on-success, one-subscription-failure-doesn't-block-others, clean drain). This is the one piece of the messaging stack never exercised. Close it first.
2. **`RedisCache`** — no test of the JSON round-trip / TTL / null-on-miss adapter (the facade's fail-open *is* covered).
3. **`GlobalExceptionHandler`** — the `ErrorResponse` body shape and the unhandled → 500 fallback are only asserted indirectly.
4. **Contract test / generated client** — the drift-free state (§5.6) and the enum assumption (§6) are held by discipline; a generated OpenAPI client or a contract test would make drift a *build failure* instead of a runtime surprise.
5. Frontend specs for `ApplicationService`/`ProfileService`/`TokenStore`.

---

## 8. Top risks, ranked

| # | Risk | Severity | Where |
|---|---|---|---|
| 1 | **BOLA/IDOR** — services trust body-supplied `employerId`/`candidateId`; identity never flows from the token | 🔴 Critical | §5.1 |
| 2 | **`POST /jobs` is unauthenticated**; roles issued but never enforced | 🔴 Critical | §5.2 |
| 3 | **Notifications sends no email** — core capability unimplemented | 🟠 High | §4 |
| 4 | **No deployment/CI/scale-out story** (no Dockerfiles, no pipeline, single-instance outbox) | 🟠 High | §3 |
| 5 | **Traces don't reach the DB or the bus** — publish→consume uncorrelated | 🟠 High | §3 |
| 6 | **Spurious `ApplicationStatusChanged`** under concurrent close+advance | 🟡 Medium | §2.1 |
| 7 | **JWT in `localStorage`** — latent XSS token theft | 🟡 Medium | §6 |
| 8 | **No CORS / rate limiting at the edge** | 🟡 Medium | §5.3 |
| 9 | **Silent enum contract-drift** risk | 🟡 Medium | §6/§7 |
| 10 | **No poison-message / dead-letter handling** beyond broker default | 🟢 Low | §2.3 |

---

## 9. The 30 / 60 / 90 day plan

Sequenced so that **security and correctness ship first**, operability second, hardening/scale third. Each item names the seam or service it lands in — no change spans two services without a design note first.

### 30 days — close the security seam and stop lying about what's done

*Theme: an authenticated user can only act as themselves, and the system can prove it runs.*

1. **Fix the identity seam (risk #1).** Add a YARP transform at the gateway that projects the validated `sub` + `role` into a trusted header; each service derives the actor from that header. Remove `EmployerId`/`CandidateId` from all inbound ViewModels. *(Gateway + every write path.)* **Ship-blocker.**
2. **Enforce authorization at the edge (risk #2).** Add `employer` / `candidate` policies on the `role` claim; put `POST /jobs` behind `employer`, `/applications` write behind `candidate`. Add the matching Angular `roleGuard`. *(Gateway + web.)*
3. **Stand up CI.** A GitHub Actions workflow: restore, build (`TreatWarningsAsErrors` now enforced for everyone), `dotnet test` across all six projects, `ng build` + `ng test`. This is the cheapest high-leverage item in the plan.
4. **Decide and document the Notifications truth.** Either implement email delivery (SMTP client + a delivery-outbox + retry, honoring the "outbox + delivery log" spec) *or* explicitly rename/reposition it as a notification-*log* service for the spike. Don't leave the charter claiming a capability the code doesn't have.
5. **Fix the spurious-event race (risk #6).** Emit `ApplicationStatusChanged` only for rows the transaction's own `ExecuteUpdate` flipped; normalize event construction to "business builds once."
6. **Add CORS + rate limiting** at the gateway (risk #8). Small, standard, unblocks any non-same-origin deployment.

### 60 days — make it observable and operable

*Theme: you can see a request end-to-end, and you know how it deploys.*

7. **Trace to the bus and the DB (risk #5).** Add Npgsql + Service Bus OTel instrumentation; propagate `traceparent` through the outbox message so publish→consume is one trace. This is what makes the Aspire dashboard tell the truth the `CLAUDE.md` scope promises.
8. **Deployment story.** Container build for each host + an Aspire manifest / `azd` target (or Compose for a simple demo). Move health checks out of the Development-only block into every environment.
9. **Auth hardening.** Refresh tokens + rotation; move the frontend off `localStorage` (httpOnly cookie or in-memory + silent refresh, risk #7). Consider asymmetric (RS256) signing so the gateway *validates* without holding a *signing*-capable key.
10. **Close the top test gaps (§7 #1–3):** `ServiceBusProcessorHost`, `RedisCache`, `GlobalExceptionHandler`. Add a startup assertion that every configured subscription actually opened (kills the silent-subscription-failure class).
11. **Contract test / generated client (risk #9).** Generate the Angular models from the services' OpenAPI (or a contract test) so drift and the enum-serialization assumption become build failures.
12. **Form a11y + missing frontend specs.**

### 90 days — earn the right to scale

*Theme: more than one replica per service is safe, and failures are visible.*

13. **Scale-out-safe outbox (risk #4).** `FOR UPDATE SKIP LOCKED` (or a leased-batch claim) in `OutboxRelay` so multiple replicas don't double-send. Then load-test a publisher at >1 replica.
14. **Poison-message policy (risk #10).** Explicit dead-letter on unknown subject / repeated failure, with an alert — don't rely on the silent broker default.
15. **Name the read-model strategy (§1).** Decide API-composition vs. CQRS read models *before* the first cross-service screen forces the hand. Pick one, write it down, apply it to "my applications with job + employer detail."
16. **Optimistic concurrency where it matters.** Add a rowversion to Profiles (and anywhere last-write-wins becomes a real conflict) if multi-device editing becomes a scenario.

---

## 10. What *not* to do (guarding the spike)

Brutal reviews cause over-correction. Explicitly out of scope until a real requirement demands it — adding any of these now would be the mistake:

- **Don't add MassTransit / a third-party outbox.** The hand-rolled one is correct and is part of the demonstration's point.
- **Don't add a new service, bus, or shared library** on your own initiative — that's an architectural decision, per `CLAUDE.md`.
- **Don't reach for a heavy frontend store (NgRx).** Signals are the right size here.
- **Don't build a full CQRS/event-sourcing rig** to solve a read-model problem you don't have yet — *decide the strategy* (item 15), don't pre-build it.
- **Don't chase 100% coverage.** The guarantees that matter are already tested; spend the budget on the §7 plumbing gaps, not on trivial getters.

---

### Bottom line

The bones are excellent — the boundaries, the layering, and the outbox/inbox mechanism are the parts that are expensive to get wrong later, and they're right. What's left is the unglamorous, ship-defining work: **prove who the caller is (the BOLA seam), enforce it (roles), make it observable (trace the bus), and give it a way off the laptop (deploy + CI).** Do the 30-day list and this stops being a spike and starts being a defensible v1.
