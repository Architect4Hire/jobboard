# Frontend ↔ Gateway Integration

*The Angular app talks to exactly one address, through typed services, with one interceptor owning the
JWT. It never knows a service exists behind the gateway.*

## The problem this solves

Without a single seam, "call the API" ends up scattered as raw `HttpClient` calls sprinkled through
components, each one juggling the token and hardcoding an address. That makes the service topology a
frontend concern (exactly what the gateway exists to hide — see
[API Gateway & Edge Concerns](./api-gateway-edge.md)) and makes auth a per-call chore instead of a
cross-cutting one. The Angular app instead has one base URL, one interceptor, and one typed service per
backend resource.

## How it works here

### One base URL, resolved through Aspire — not hardcoded

[`API_BASE_URL`](../../../src/web/src/app/core/api/api-base-url.ts) is an injection token for a
same-origin relative mount, `/api` — deliberately *not* the gateway's actual address:

```ts
export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL');
```

The browser bundle can't read the environment variables Aspire injects, so the translation happens one
layer down, in the Node process running the dev server —
[`proxy.conf.js`](../../../src/web/proxy.conf.js):

```js
const target = process.env['services__gateway__https__0'] || process.env['services__gateway__http__0'];
module.exports = [{ context: ['/api'], target, changeOrigin: true, pathRewrite: { '^/api': '' } }];
```

Aspire's `AddJavaScriptApp("web", "../web", "start")` (see
[Aspire Orchestration](./aspire-orchestration.md)) injects `services__gateway__https__0` when it starts
the app, so this file — not the Angular code — is the only place the gateway's real address is read,
and it's still never a literal. `/api/jobs` from the browser becomes `/jobs` at the gateway.

### One interceptor owns the token

[`auth.interceptor.ts`](../../../src/web/src/app/core/auth/auth.interceptor.ts) is a functional
`HttpInterceptorFn`:

```ts
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const accessToken = tokenStore.accessToken;
  const isGatewayRequest = request.url.startsWith(baseUrl);
  const attach = accessToken !== null && isGatewayRequest;

  const outgoing = attach
    ? request.clone({ setHeaders: { Authorization: `Bearer ${accessToken}` } })
    : request;

  return next(outgoing).pipe(
    catchError((error) => {
      if (attach && error instanceof HttpErrorResponse && error.status === 401) {
        tokenStore.clear();
        void router.navigate(['/login']);
      }
      return throwError(() => error);
    }),
  );
};
```

It attaches the bearer token **only** to requests under `API_BASE_URL` — so the JWT never rides along to
a third-party URL — and only clears the session on a 401 for a request it *did* authorize (a failed
login's own 401 is left for the login component to show, not treated as a session expiry).
[`TokenStore`](../../../src/web/src/app/core/auth/token-store.ts) is the single place the token lives
(a signal, persisted to `localStorage`); [`Session`](../../../src/web/src/app/core/auth/session.ts)
derives the current principal by decoding the JWT client-side — explicitly **not** an authorization
boundary (the gateway/services validate the token; this only drives what the UI *offers*, e.g. hiding
"Post a job" from a candidate).

### One typed service per backend resource

Components never call `HttpClient` directly. [`JobService`](../../../src/web/src/app/core/api/job.service.ts):

```ts
@Injectable({ providedIn: 'root' })
export class JobService {
  private readonly baseUrl = `${inject(API_BASE_URL)}/jobs`;
  list(category?: string): Observable<readonly JobSummary[]> { /* GET */ }
  post(body: PostJobRequest): Observable<Job> { /* POST */ }
}
```

[`job.model.ts`](../../../src/web/src/app/core/models/job.model.ts) mirrors Jobs' ServiceModels/ViewModels
field-for-field, including the wire details that matter — enum values cross as numbers (`JobStatus.Open
= 1`), matching the C# enum, because no `JsonStringEnumConverter` is registered on either side. A model
drifting from its service's actual shape is exactly the failure mode the
[`api-contract-checker`](../../../.claude/agents/api-contract-checker.md) subagent exists to catch.

Components consume these services with signals, not manual subscriptions —
[`job-list.ts`](../../../src/web/src/app/features/job-list/job-list.ts) uses `toSignal` so the
subscription's lifecycle is owned by the framework:

```ts
private readonly jobs = toSignal(
  this.jobService.list().pipe(catchError(() => { this.loadError.set(true); return of([]); })),
);
```

### The identity gap, visible from this side too

[`Session.userId`](../../../src/web/src/app/core/auth/session.ts) is documented as "the account id to
send as employerId/candidateId" — and [`PostJobRequest`](../../../src/web/src/app/core/models/job.model.ts)
does exactly that, carrying `employerId` in the request body. This is the frontend half of the same gap
described in [Authentication & Identity Propagation](./authentication-and-identity-propagation.md#the-gap-this-doesnt-close-yet):
the id is client-supplied today because the service still expects it there, not because the frontend
couldn't derive it more safely. Closing [ADR-0011](../../adr/0011-token-derived-identity-propagation.md)
means removing this field from the model and the request body, not just trusting the header on the
backend.

## Why

`.claude/rules/frontend.md` is the standing convention this doc grounds; there's no dedicated ADR for
the frontend shape itself (the gateway-only-address decision is
[ADR-0006](../../adr/0006-single-api-gateway-yarp.md)).

## Pitfalls / rules to respect

- **Never target a service directly.** Every request goes through `API_BASE_URL` → the gateway; there
  is no code path that should know a service's own address.
- **Data access through a typed service, never raw `HttpClient` in a component.** One service per
  backend resource (`JobService`, `ApplicationService`, `ProfileService`, `AuthService`).
- **The token lives in exactly one place** (`TokenStore`); everything else — the interceptor, `Session`
  — reads it, none of them own a second copy.
- **Prefer the `async` pipe or `toSignal`.** If you must subscribe manually, clean up with
  `takeUntilDestroyed`/`DestroyRef`.
- **A model mirrors its service's ServiceModel exactly**, including wire-format details (numeric enums,
  field casing) — a silent mismatch is a runtime bug, not a compile error, exactly as with
  [Integration Event Contracts](./integration-event-contracts.md)'s status-as-string tradeoff.

## Reference map

| Concern | Real file |
| --- | --- |
| Gateway base URL token | [`api-base-url.ts`](../../../src/web/src/app/core/api/api-base-url.ts) |
| Dev-server proxy to the injected gateway address | [`proxy.conf.js`](../../../src/web/proxy.conf.js) |
| Auth interceptor | [`auth.interceptor.ts`](../../../src/web/src/app/core/auth/auth.interceptor.ts) |
| Token storage + session/principal | [`token-store.ts`](../../../src/web/src/app/core/auth/token-store.ts) · [`session.ts`](../../../src/web/src/app/core/auth/session.ts) |
| Typed service example | [`job.service.ts`](../../../src/web/src/app/core/api/job.service.ts) |
| Model mirroring a ServiceModel | [`job.model.ts`](../../../src/web/src/app/core/models/job.model.ts) |
| Standalone component example | [`job-list.ts`](../../../src/web/src/app/features/job-list/job-list.ts) |
| Standing rules | [`.claude/rules/frontend.md`](../../../.claude/rules/frontend.md) |
