---
paths:
  - src/web/**
---
# Frontend rules — Angular + TypeScript (Aspire)

The Angular app talks to exactly one backend address — the **gateway** — and never knows the services
behind it. That decoupling is the rule to protect.

- **Standalone components** (no NgModules). One feature per folder.
- **Strict TypeScript.** No `any`. Model interfaces mirror the owning service's **ServiceModels**
  exactly (a `Job` model mirrors Jobs' `JobDetailServiceModel`).
- **Gateway base URL from Aspire.** The app is launched by `AddJavaScriptApp`, so read the gateway
  endpoint from injected environment/config — don't hardcode `localhost:port`, and never target a
  service directly.
- **Data access through services.** Components never call `HttpClient` directly; use a typed service,
  one per backend resource (`JobService`, `ApplicationService`, `ProfileService`). All requests hit
  the gateway's public routes.
- **Auth via an interceptor.** A shared `HttpInterceptor` attaches the Identity JWT and handles 401 —
  not per-call token juggling in each service.
- **Subscriptions.** Prefer the `async` pipe. If you must subscribe, clean up with
  `takeUntilDestroyed` / `DestroyRef`.
- **Scaffolding.** Generate with `ng generate component <feature>/<name>` (or the `add-component`
  skill) so structure stays consistent.
- **Naming.** kebab-case filenames, PascalCase classes, camelCase members.

Starting UI shape: `job-list`, `job-detail`, `post-job-form`, `application-list`,
`application-status`, plus `login`/`register`.
