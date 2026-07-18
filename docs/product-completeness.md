# JobBoard — Product Completeness: Feature Gaps to a Fuller App

*What NEEDS to be built for JobBoard to be a **fully-featured job board**, as a product — not as an architecture. A capability-by-capability inventory of what a candidate and an employer can actually do today versus what a real hiring platform is expected to do, with each gap named to the service(s) it lands in.*

**Scope of this document:** product functionality. **Status:** spike / first pass · **Date:** 2026-07-18 · **Branch:** `main`
**Grounded in:** every controller/endpoint across the five services, every integration event, and the full Angular app under `src/web`.

---

## 0. How this differs from the 30/60/90 plan

The [Ongoing Architecture Plan](./ongoing-architecture-plan.md) answers *"is the thing we built engineered correctly, secure, and operable?"* — it is about the **seams**: the broken identity/BOLA seam, missing CI/deploy, traces that don't reach the bus, scale-out-safe outbox. Read it first if you haven't; several items here **depend on** its 30-day security work (you cannot safely wire an employer's "review applicants" screen until identity actually flows from the token — Architecture Plan §5.1).

**This document answers a different question:** *"as a job board, what can't a user do that they'd expect to?"* It is about **product surface** — the features, screens, and flows a real hiring platform has that this one does not yet. The two are complementary and deliberately non-overlapping: where the architecture plan says "enforce that a candidate can only act as themselves," this one says "give the employer a screen to move an application from Reviewed to Offered." Neither is a superset of the other.

> **A note on discipline.** Everything below is a *product* proposal, not a licence to break the architecture. Per [`CLAUDE.md`](../CLAUDE.md): a new **service**, **bus**, or **shared library** is an architectural decision to *propose*, not scaffold — §7 flags exactly which of these features cross that line. Everything else fits an existing bounded context and should be built with the [`add-endpoint`](../.claude/skills/) / [`add-component`](../.claude/skills/) skills.

---

## 1. The completeness verdict

The **write model of the core hiring loop is largely real** — jobs get posted, applications get submitted, statuses advance, events fan out, notifications get logged. What's missing to be "fuller" clusters into four honest buckets:

1. **Built but unreachable.** Several backend capabilities exist with **no UI to drive them** — closing a job, advancing an application through the funnel, employer company profiles. The cheapest, highest-value work in this whole document is *wiring up what already ships*.
2. **The employer side of the funnel is a dead end.** An employer can post a job but cannot see who applied to it, because Applications can only be listed *by candidate*, never *by job*. The entire "review applicants → interview → offer/reject" workflow — the reason an employer uses a job board — is not reachable end-to-end.
3. **Discovery is a toy.** There is no server-side search, no pagination, no real filtering, no sorting anywhere. The job board "search" is a single client-side filter over one full fetch of open jobs.
4. **Account & engagement lifecycle is absent.** No password reset, no email verification, no saved jobs, no alerts, no readable notifications, no talent search. These are table stakes users notice immediately.

### Completeness scorecard (product, not engineering)

| Capability area | State | One-line |
|---|---|---|
| Auth & account lifecycle | 🟡 Partial | Register + login work; **no reset, verify, change, or logout-server-side**. |
| Job posting (employer) | 🟡 Partial | Post works; **no edit, no delete, no reopen, no drafts, no "my postings"**. |
| Job discovery (candidate) | 🔴 Thin | Client-side filter only; **no server search / pagination / sort / real filters**. |
| Applying (candidate) | 🟡 Partial | Apply + withdraw work; **no résumé attach, no cover letter, no dupe guard, no accept-offer**. |
| Applicant review (employer) | 🔴 Missing | **Cannot list applicants for a job at all** — the funnel's employer half is unreachable. |
| Profiles — candidate | 🟢 Good | Full self-service profile + résumé upload/download exists. |
| Profiles — employer / company | 🔴 Missing UI | Backend exists; **no route, no component, no public company page**. |
| Talent discovery (employer) | 🔴 Missing | **No candidate search / browse** — profiles fetch only by exact GUID. |
| Notifications as a product | 🔴 Sink only | Logs to a table; **no delivery, no in-app center, no preferences, unreadable by users**. |
| Engagement (saved jobs, alerts, dashboards) | 🔴 Missing | None of it exists. |
| Trust & safety / admin | 🔴 Missing | No moderation, reporting, admin, or analytics. |

---

## 2. Wire up what already exists — the cheapest wins first

These need **no new backend** — the endpoint or service method already ships and simply has no caller. Every one is a frontend-only (or frontend-plus-tiny-endpoint) task and should be picked up before anything greenfield.

| # | Gap | What ships today | What's missing | Lands in |
|---|---|---|---|---|
| 2.1 | **Close a job from the UI** | `POST /jobs/{id}/close` + `JobService.close()` both exist | No button anywhere calls it | `web` (job-detail / a new employer postings screen) |
| 2.2 | **Advance an application** | `POST /applications/{id}/advance` + `ApplicationService.advance()` exist | No screen calls it — the employer can't move Submitted→Reviewed→Offered/Rejected | `web` (needs the applicant-review screen, §3) |
| 2.3 | **Employer / company profile** | `GET/PUT /profiles/employers/{id}` + `ProfileService.getEmployer/upsertEmployer` exist | No route, no component | `web` (new `/company` route + component) |
| 2.4 | **View a single application** | `GET /applications/{id}` + `ApplicationService.get()` exist | No application-detail screen (status timeline, résumé link) | `web` |

> **Do 2.1–2.4 after** the Architecture Plan's 30-day identity + role work — wiring an employer action to a route that isn't role-gated and doesn't derive the actor from the token would ship the BOLA seam into the UI.

---

## 3. Close the hiring loop — the core product gap

A job board exists to connect an application to the employer who posted the job. **Today that connection is severed on the read side.** This is the single most important *feature* gap (distinct from the most important *security* gap in the architecture plan).

### 3.1 Employer can't see who applied 🔴 — the headline gap

- **What's missing:** Applications can be listed **only** by `candidateId` (`GET /applications?candidateId=…`). There is **no list-by-job and no list-by-employer** endpoint, so an employer literally has no way to enumerate applicants for their posting.
- **Build:** a `GET /applications?jobId={id}` (and/or `?employerId=`) endpoint in **Applications** (`add-endpoint`), then an **employer applicant-review screen** in `web` that lists applicants and drives the existing `advance` action (2.2).
- **Cross-service seam:** the review screen needs the *candidate's* name/headline (owned by **Profiles**) next to each application (owned by **Applications**). This forces the [cross-service read-model decision](./adr/0012-cross-service-read-model-strategy.md) that ADR-0012 and Architecture Plan §1 flag as pending — API composition at the gateway vs. a denormalized read model. **Name the strategy before building the screen.**

### 3.2 Applying is incomplete

- **No résumé attached at apply time.** The submit flow always sends `resumeReference: null`; the candidate's uploaded profile résumé is never linked to the application. Build résumé selection into the apply flow and populate `ResumeReference`. *(web + a small contract tightening in **Applications**.)*
- **No cover letter / message** on apply — no field exists in the ViewModel or the form. *(Applications + web.)*
- **No duplicate-application guard.** Nothing stops a candidate applying to the same job twice — there's no uniqueness on `(CandidateId, JobId)`. Add the constraint + a friendly 409. *(Applications.)*
- **No "accept offer" / hired state.** The lifecycle ends at `Offered`/`Rejected`/`Withdrawn`; a candidate cannot **accept** an offer, and there is no `Hired`/`Accepted` terminal state. Add the transition + event. *(Applications; likely a new `ApplicationStatusChanged` value, no new contract.)*

### 3.3 Job posting is one-shot

- **No edit** — a posted job is immutable (title, description, salary, tags). Add `PUT /jobs/{id}`. *(Jobs + web.)*
- **No delete / archive**, **no reopen** (only Open→Closed). *(Jobs.)*
- **No "my postings" list** — no list-by-employer endpoint, so an employer can't see or manage their own jobs. *(Jobs + web.)*
- **Drafts are dead code** — `JobStatus.Draft` exists in the enum but is unreachable (no draft workflow, drafts aren't listable). Either implement save-as-draft or remove the value. *(Jobs + web.)*

---

## 4. Discovery & search — make the board usable at scale

Today: `GET /jobs` returns **all** open jobs, newest-first, with an optional single-category filter; the frontend fetches that whole set once and filters in the browser. That breaks the moment there are more than a screenful of jobs.

- **Server-side keyword search** over title/description/location — absent entirely. *(Jobs.)*
- **Pagination** on every list endpoint (`/jobs`, `/applications`) — none exists; lists are always full-set. *(Jobs, Applications + web.)*
- **Real filters as query params:** location, salary range, remote, tags, employer, status — only a single category slug is honored today. *(Jobs + web.)*
- **Sorting options** (relevance, date, salary) — always hardwired to newest-first. *(Jobs + web.)*
- **Browse-by-category / company directory landing** — no discovery surface beyond the flat list. *(web, + list endpoints.)*
- **Featured / promoted jobs** — no concept of ranking or promotion. *(Jobs.)*

> **Scale note, not a build note:** a *dedicated search service* (Elastic/OpenSearch) is a **new service** — that's §7, propose-don't-scaffold. Start by adding query params and paging to the existing **Jobs** service; only extract search when the data volume actually demands it.

---

## 5. Account & identity lifecycle

Register and login are real and well-built (PBKDF2, HMAC JWT, uniform 401). The *lifecycle around* an account is missing — and users notice these on day one.

| Gap | Lands in | Notes |
|---|---|---|
| **Password reset / forgot password** | Identity (+ Notifications for the email, + web) | Needs a token flow and a real email channel (§6). |
| **Email verification on register** | Identity (+ Notifications, + web) | Currently anyone registers with any email, unverified. |
| **Change password / change email** | Identity + web | No self-service account management at all. |
| **Server-side logout / token revocation** | Identity | `jti` is issued but there's no deny-list; logout is client-side only. (Also Architecture Plan §4 — refresh tokens.) |
| **"Get current account" endpoint** | Identity + web | Accounts are not retrievable after creation; the UI decodes the JWT client-side for everything. |
| **Account settings / delete account** | Identity + web | No settings page, no deletion (GDPR-relevant). |

> These overlap the Architecture Plan's 60-day auth-hardening (refresh tokens, move off `localStorage`, RS256). Treat the *security* mechanics there and the *product* surface (the reset screen, the verify email, the settings page) here.

---

## 6. Notifications as a product, not a sink

Notifications is architecturally sound but is a **write-only audit log**: three consumers record `NotificationLog` rows and nothing else. As a product feature it does not yet exist.

- **No actual delivery.** No email/SMS/push is ever sent — the body is rendered and dropped in a table. This is also the Architecture Plan's §4 "Notifications sends no email" ship-decision; the *product* form of the fix is a working email channel candidates and employers actually receive. *(Notifications — SMTP client + delivery outbox.)*
- **Users can't read their notifications.** There's no HTTP surface at all — no list, no mark-as-read, no unread count. An **in-app notification center** needs a read API (the one deliberate exception to "Notifications has no public door" — decide consciously) or a different owner. *(Architectural decision — see §7.)*
- **No preferences.** No per-user control over which events notify, or channel choice. *(Notifications + web + Identity/Profiles for the preference store.)*
- **No templating** beyond hardcoded strings; no localization. *(Notifications.)*
- **Coverage gap:** Notifications does **not** consume `JobClosed` directly — a candidate is only notified via the downstream per-application `ApplicationStatusChanged`. Confirm that's intended. *(Notifications.)*

---

## 7. Features that require an architectural decision (propose, do not scaffold)

Per [`CLAUDE.md`](../CLAUDE.md), these introduce a **new bounded context / service / bus** and must be a design conversation *before* any code. Listed so the roadmap is honest about them — not as a to-do to start unprompted.

| Feature | Why it's a new boundary | Suggested owner |
|---|---|---|
| **Candidate ↔ employer messaging / chat** | A genuinely new capability with its own lifecycle, storage, and real-time concerns — not a fit for any existing context. | New `Messaging` service (propose). |
| **Interview scheduling** | Calendars, availability, invites — a distinct domain. | New service, or an extension of the hiring context — decide. |
| **In-app notification read-model / center** | Requires giving Notifications a public door (it has none by design) *or* a new read-side. | Decide: relax the Notifications boundary vs. a new read service. |
| **Full-text / faceted search at scale** | A search index is a new infrastructure resource + likely a new service. | New `Search` service + `add-aspire-resource` (propose). |
| **Saved jobs / bookmarks & job alerts** | Candidate-owned activity data — could extend **Profiles**, or be its own small context. A boundary call, not obvious. | Decide: Profiles vs. new context. |
| **Payments / billing** (promoted posts, subscriptions) | Money is always its own bounded context. | New `Billing` service (propose). |
| **Admin / moderation / analytics** | Cross-cutting read + governance surface spanning every service. | Decide: admin BFF vs. per-service admin endpoints. |

---

## 8. Trust, safety, and the long tail

Lower priority for a reference build, listed for completeness:

- **Moderation:** report a job/profile, flag spam, takedown workflow. *(New surface — §7.)*
- **Public candidate profiles** for employers to browse talent — profiles are self-edit only today, fetchable only by exact GUID; **no candidate search/browse** exists. *(Profiles — a query surface + `web`.)*
- **Profile richness:** avatar/photo upload (only résumé upload exists), work-history/education as structured data (only free-text summary + skills today). *(Profiles + web.)*
- **Onboarding:** guided first-run for candidate vs. employer; terms/privacy acceptance at register. *(web + Identity.)*
- **Analytics / reporting:** views-per-job, applicant funnel metrics, time-to-hire. *(§7.)*
- **Internationalization / accessibility polish:** the frontend is already strong on a11y (Architecture Plan §6); form-error `aria` wiring and i18n remain. *(web.)*

---

## 9. Suggested build order (by product value ÷ effort — *not* a 30/60/90)

Deliberately framed as value tiers, not calendar buckets, to stay distinct from the [Architecture Plan's 30/60/90](./ongoing-architecture-plan.md#9-the-30--60--90-day-plan). Sequence assumes the architecture plan's **30-day identity + role work lands first** — it's a hard prerequisite for anything employer-facing.

**Tier 1 — Wire up what exists + close the loop (highest value, lowest effort).**
Employer applicant-review screen driving `advance` (§3.1, §2.2) · close-job button (§2.1) · "my postings" list (§3.3) · attach résumé + cover letter on apply (§3.2) · duplicate-application guard (§3.2). *This is the difference between "a demo of posting" and "a working hiring loop."*

**Tier 2 — Make discovery and accounts real.**
Server-side search + pagination + real filters/sort (§4) · password reset + email verification (§5, needs §6's email channel) · employer/company profile UI (§2.3).

**Tier 3 — Notifications as a product + engagement.**
Real email delivery (§6) · in-app notification center (§6/§7 decision) · saved jobs + job alerts (§7 decision) · candidate/talent search (§8).

**Tier 4 — New-boundary features (each its own design doc + ADR first).**
Messaging · interview scheduling · billing · admin/moderation/analytics (§7).

---

## 10. Relationship to the rest of the docs

- **Engineering correctness / security / operability** → [Ongoing Architecture Plan](./ongoing-architecture-plan.md) (do its 30-day items first; they gate the employer features here).
- **Target architecture & boundaries** → [High-Level Design](./high-level-design.md).
- **Pending design calls this roadmap forces** → [ADR-0011 (identity propagation)](./adr/0011-token-derived-identity-propagation.md) and [ADR-0012 (read-model strategy)](./adr/0012-cross-service-read-model-strategy.md) — §3.1 can't be built well until ADR-0012 is decided.
- **How to actually build a slice** → [Adding an Endpoint by Hand](./adding-an-endpoint-manually.md) and the [`add-endpoint`](../.claude/skills/) / [`add-component`](../.claude/skills/) skills.

### Bottom line

The hiring loop's **write path is real; its read path and its whole engagement surface are not.** The fastest route to a "fuller app" isn't greenfield — it's **wiring up the endpoints that already ship** (§2) and **giving the employer a way to see and act on applicants** (§3). Do Tier 1 and JobBoard stops being a posting demo and becomes a job board someone could actually hire through.
