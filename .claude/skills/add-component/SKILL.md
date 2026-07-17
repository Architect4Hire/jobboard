---
name: add-component
description: >
  Scaffold a new Angular component in the JobBoard frontend. Use when creating UI — e.g. "add a
  job-list component", "make an application-status badge", "build the post-job form". Produces a
  standalone component wired to a typed service that calls the GATEWAY (never a service directly),
  following this repo's frontend conventions.
---

# Add an Angular component

Work in `src/web/`. The frontend talks to exactly one backend address — the **gateway** — and never
knows the services exist behind it. That's the rule that keeps the UI decoupled from the service
topology, so it's the one to get right.

1. **Generate.** `ng generate component <feature>/<name>` — standalone, kebab-case files.
2. **Types.** Define/reuse a model interface that mirrors the owning service's **ServiceModels**
   exactly (a `Job` model mirrors Jobs' `JobDetailServiceModel`). No `any`. If the shape isn't a
   ServiceModel the API actually returns, it's wrong.
3. **Data access.** Never call the API from the component. Use (or create) a typed service on
   `HttpClient`, **one per backend resource** (`JobService`, `ApplicationService`, `ProfileService`).
   Read the **gateway** base URL from Aspire-injected config, not a hardcoded value, and build paths
   from the **gateway's** public routes — not a service's internal path. The service never targets
   `http://jobs` or any port; that resolution is the gateway's job.
4. **Auth.** Protected calls carry the JWT from Identity. Attach it with the shared
   `HttpInterceptor`, not by hand in each service; if the token's missing/expired, the interceptor
   handles the redirect. Don't put tokens in component code.
5. **Template.** Render async data with the `async` pipe. If you must subscribe, clean up with
   `takeUntilDestroyed` / `DestroyRef`.
6. **Tests.** Update `.spec.ts` with a render test and one behavior test; mock the typed service
   (assert it was called with the gateway path), don't hit the network. Run `ng test`.

## JobBoard UI notes
Typical components: `job-list` (cards + filter), `job-detail` (description + apply button),
`post-job-form` (employer create/edit), `application-list` (a candidate's applications + status),
`application-status` (badge), `login`/`register`. Keep presentational and data concerns separate
where it helps testability. A component that shows data from two services still calls two typed
services (each hitting the gateway) — it never reaches past the gateway to stitch them itself.

## Checklist before done
- [ ] Standalone component, kebab-case filenames
- [ ] Model interface mirrors the owning service's ServiceModels
- [ ] API access through a typed service; **gateway** base URL from injected config; paths are the
      gateway's public routes, never a service's internal address or port
- [ ] Auth via the shared interceptor, not hand-rolled per call
- [ ] `async` pipe used (or subscriptions cleaned up)
- [ ] Tests pass, service mocked (`ng test`)
