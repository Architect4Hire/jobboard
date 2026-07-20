# JobBoard — Audit Trail SCRUB Prompts

Prompts for driving Claude Code to build the **support audit trail** on JobBoard (Aspire + ASP.NET
Core microservices + Angular), wired to the skills, rules, subagents, and ADRs in your `.claude/` and
`docs/` folders. Read alongside [`scrub-prompts.md`](./scrub-prompts.md) — same SCRUB skeleton, same
golden rule.

The feature is a durable, queryable record of *what happened to a request or entity, end to end,
across every service* — for support, not for engineers. It is **not** OpenTelemetry tracing. The
design is fixed by three ADRs: [ADR-0013](../adr/0013-correlation-causation-identifiers-on-events.md)
(correlation/causation ids on events), [ADR-0014](../adr/0014-audit-bounded-context-bus-fed-support-trail.md)
(the `JobBoard.Audit` bounded context), and [ADR-0011](../adr/0011-token-derived-identity-propagation.md)
(the trustworthy actor). The enforceable rules live in [`.claude/rules/audit.md`](../../.claude/rules/audit.md).

## The reusable SCRUB skeleton

```
SCOPE:        what to build/change + which service/project it touches
CONSTRAINT:   the rules to honor (stack, conventions, plan-first)
RESTRICTION:  explicit "do NOT" guardrails
USAGE:        which skills / subagents / tools to use
BEHAVIOR:     how to proceed — plan, approve, small steps, test, report
```

## How to use these

- Run the Part 1 prompts **in order**, one at a time — don't paste the whole file.
- Every prompt asks Claude to **plan first and wait for approval** before editing. For this feature
  that matters twice over: the Contracts change (Prompt A1) touches every publisher and consumer, and
  Prompt A4 stands up a **new service** — both are design conversations before code.
- **The golden rule of the sequence:** don't chase full cradle-to-grave *coverage* until **one**
  lifecycle flows end to end. Prompts A1–A5 build and prove the spine on the events that already
  exist; Prompt A7 fans coverage out to the actions that don't emit yet.
- Use `/clear` between big steps to keep context lean; rules and skills reload on their own.

---

# Part 1 — Build the audit trail (run once, in order)

## Prompt A0 — Ratify the design (no code)

```
SCOPE: Review the three ADRs behind the audit trail — ADR-0013 (correlation/causation ids on events),
ADR-0014 (the JobBoard.Audit bounded context, Postgres auditdb + jsonb), and ADR-0011 (token-derived
identity, the actor source) — plus .claude/rules/audit.md. Confirm the boundaries and the build order
before any code exists.

CONSTRAINT: Follow CLAUDE.md. Adding a service and changing an event contract are architectural
decisions — this step is where they're agreed.

RESTRICTION: Do NOT write any product code, migration, or wiring in this step. Do NOT expand scope
beyond these three ADRs and the rule.

USAGE: Read docs/adr/0011, 0013, 0014 and .claude/rules/audit.md. Use plan mode.

BEHAVIOR: Summarize the target: the id fields, where each is minted/stamped, the new service's shape,
the store, and the query surface. Flag anything you'd change. On my nod, promote ADR-0011, 0013, 0014
from Proposed to Accepted and update docs/adr/README.md, then stop.
```

## Prompt A1 — The spine: correlation & causation on the event contract

```
SCOPE: Implement ADR-0013 in JobBoard.Contracts. Add CorrelationId (Guid), CausationId (Guid), and the
acting identity to IIntegrationEvent and to every event record (JobPosted, JobClosed,
ApplicationSubmitted, ApplicationStatusChanged). Update every publisher's event-building/mapper code
and every consumer to carry the new fields through unchanged for now.

CONSTRAINT: Follow .claude/rules/messaging.md, .claude/rules/audit.md, and ADR-0010 (Contracts stays a
leaf — plain fields, no behavior, no EF). This is a contract change affecting every service.

RESTRICTION: Do NOT add behavior to Contracts. Do NOT wire the gateway or the Audit service yet — this
step only widens the contract and threads the fields where events are already built and consumed. Do
NOT invent a value for actor at the publish site yet if the identity isn't available; leave a clearly
marked seam that Prompt A2 fills.

USAGE: Use the add-endpoint skill's model/mapper conventions. Delegate review to the code-reviewer +
api-contract-checker.

BEHAVIOR: Plan the field additions and every touched publisher/consumer/mapper, and show me before
writing. Wait for approval. Implement, run EVERY service's tests (this crosses all of them), run the
api-contract-checker, and summarize the blast radius you actually touched.
```

## Prompt A2 — Mint at the edge + the trustworthy actor (Gateway)

```
SCOPE: Make the gateway the origin of the request thread and the identity. Mint a CorrelationId when a
request arrives without one, strip any client-supplied copy, forward it inward as a trusted header, and
echo it on the response. Implement ADR-0015's identity-projection MECHANISM in the same pass: project
the validated sub/role into X-User-Id/X-User-Role, stripping client copies. Read both on the service
side into an ambient request context the publish path can reach.

CONSTRAINT: Follow .claude/rules/gateway.md, .claude/rules/audit.md, ADR-0015. Edge cross-cutting only;
keep the gateway declarative.

RESTRICTION: Do NOT trust a client-supplied CorrelationId or identity header past the edge — strip and
re-mint/re-project. Do NOT put business logic in the gateway. No hardcoded addresses. Do NOT do the
broader BOLA/IDOR remediation here (removing EmployerId/CandidateId from ViewModels, role-authorization
policies) — that is ADR-0011, a separate security effort out of scope for this sequence.

USAGE: Verify YARP transform syntax against https://aspire.dev and the YARP docs before wiring. Use
plan mode. Delegate review to the code-reviewer + /security-review (this is a spoofing surface).

BEHAVIOR: Plan the transforms, the strip/mint/project order, and the ambient-context read on the
service side, and show me before writing. Wait for approval. Implement, prove with a request that a
client-supplied X-User-Id/CorrelationId is ignored and a fresh one appears on the response, run the
security review, and summarize.
```

## Prompt A3 — Stamp the thread at every publish site

```
SCOPE: Fill the seam from A1: at each existing publish site, stamp CorrelationId, CausationId, and
actor onto the event when business builds it, read from the ambient request context (A2). For a
request-initiated event, CausationId is the request's own id; for a follow-on event built while
consuming another (e.g. a consumer that emits ApplicationStatusChanged after JobClosed), CausationId is
the consumed event's Id and CorrelationId is inherited from it.

CONSTRAINT: Follow .claude/rules/audit.md and .claude/rules/messaging.md. Business builds and stamps the
event; the outbox/dispatcher path is unchanged. Actor comes from the propagated identity, never a
body-supplied id.

RESTRICTION: Do NOT change the outbox, dispatcher, or transaction seam. Do NOT read identity from the
request body. Do NOT touch the Audit service yet.

USAGE: Use the add-endpoint skill's business/mapper conventions. Delegate review to the code-reviewer.

BEHAVIOR: Plan where the ambient context is read and how causation is derived on both the
request-initiated and consumer-initiated paths, and show me before writing. Wait for approval.
Implement, run each touched service's tests, and summarize which events now carry a full thread.
```

## Prompt A4 — Stand up the Audit service (wired, no consumers)

```
SCOPE: Scaffold the JobBoard.Audit bounded context (ADR-0014): a thin host JobBoard.Audit and a
library JobBoard.Audit.Core, following the Jobs/Applications template. Create AuditDbContext in
JobBoard.Audit.Core deriving from the Shared base context (inherits Outbox/Inbox sets); the audit row
carries event type, CorrelationId, CausationId, actor, entity ids, occurred-at, and the event as a
jsonb payload. In the AppHost add auditdb to the Postgres server, register the Audit host with
WithReference to auditdb + Service Bus and WaitFor the database, and declare its subscriptions on the
existing event topics.

CONSTRAINT: Follow .claude/rules/backend.md, .claude/rules/aspire.md, .claude/rules/audit.md. References:
Audit.Core -> Shared + Contracts; Audit -> Audit.Core + ServiceDefaults. Consumer-only service — no
public mutations. Postgres + jsonb, local-first (ADR-0008).

RESTRICTION: Do NOT add any consumer logic or query endpoint yet — this proves the two-project wiring,
the auditdb, and the subscriptions only. Do NOT use Cosmos. Do NOT expose auditdb directly. No hardcoded
strings.

USAGE: Use the add-aspire-resource skill for auditdb, the service registration, and the subscriptions.
Use plan mode. Delegate review to the code-reviewer.

BEHAVIOR: Plan the two projects, the AuditDbContext + row shape (jsonb column), the AppHost wiring, and
the subscription names (keep AppHost subscription == consumer registration string), and show me before
writing. Generate the initial migration but STOP before database update. Then aspire run and confirm
Audit shows healthy with auditdb connected. Report.
```

## Prompt A5 — Collect the trail + prove one lifecycle end to end

```
SCOPE: Add the audit consumer(s) that subscribe to every existing business event (JobPosted, JobClosed,
ApplicationSubmitted, ApplicationStatusChanged) and append one immutable row to auditdb, idempotent via
the inbox in the same transaction as the append. Then prove the whole spine on ONE lifecycle.

CONSTRAINT: Follow .claude/rules/audit.md, .claude/rules/messaging.md, .claude/rules/backend.md.
Append-only — no updates/deletes. Dedupe on the event Id via the inbox (ADR-0004).

RESTRICTION: The Audit service writes ONLY auditdb. It MUST be idempotent. Do NOT call back into any
service and do NOT drive any domain behavior — it only records. Do NOT add the query endpoint yet.

USAGE: Use the add-audit-event skill (consumer side). Apply the pending migration on my approval.
Delegate review to the code-reviewer.

BEHAVIOR: Plan the consumer(s) and the append path, and show me before writing. Wait for approval.
Implement, run the Audit tests plus each publisher's, then aspire run and DEMONSTRATE: post a job ->
apply -> advance the application's status, and show the audit rows landing — all sharing one
CorrelationId, the causal tree correct (CausationId chain), and a redelivery adding no duplicate row.
Summarize.
```

## Prompt A6 — The support query surface

```
SCOPE: Add the read-only support-query endpoint to JobBoard.Audit and its gateway route: fetch a
request's full trail by CorrelationId, an entity's life by entity id, and filter by actor and time
window. Returns audit ServiceModels (never raw rows / jsonb internals the caller shouldn't see).

CONSTRAINT: Follow .claude/rules/audit.md, .claude/rules/backend.md, .claude/rules/gateway.md. One
auth-protected gateway route (ADR-0006). Only ServiceModels out.

RESTRICTION: Read-only — no mutation surface on the Audit service. Do NOT expose auditdb directly. Do
NOT leak secrets/PII from the payload; project only what support needs.

USAGE: Use the add-endpoint skill (read path) and the trace-a-request skill to shape the query
ergonomics an agent will use. Delegate review to the code-reviewer + api-contract-checker.

BEHAVIOR: Plan the query methods, the ServiceModels, and the gateway route, and show me before writing.
Wait for approval. Implement, run the tests, then aspire run and reconstruct the Prompt A5 lifecycle
through the gateway by its CorrelationId. Summarize.
```

## Prompt A7 — Close the cradle: audit the actions that don't emit yet

```
SCOPE: Make the trail truly cradle-to-grave by adding audit-worthy events for the mutating actions that
don't publish today — account created and login (Identity), and profile updated (Profiles) — so the
Audit service records them too. One action at a time.

CONSTRAINT: Follow all rules in .claude/rules/. Each new event is a small past-tense Contracts record
carrying the thread (ADR-0013) and only the fields the trail needs — no secrets (never the password or
token).

RESTRICTION: Do NOT log credentials or tokens into the trail. Do NOT invent a new pattern per action —
replicate the publish + audit-consume shape. One action at a time; plan -> approve -> implement -> test
-> report before the next.

USAGE: Use the add-audit-event skill for each action. Delegate review to the code-reviewer +
api-contract-checker per action; run the audit-coverage-checker subagent at the end.

BEHAVIOR: For each action: plan the event and where it's published, wait for approval, implement, run
the publisher's and Audit's tests, and confirm the row lands. After all three, run the
audit-coverage-checker and report any mutating action still unaudited.
```

---

# Part 2 — Operational template (reuse anytime)

## Template — Audit a new action

_Use whenever you add a mutating action and want it in the trail. This is the everyday guardrail that
keeps coverage from rotting._

```
SCOPE: Make <action in <service>> appear in the support audit trail. If it already publishes an event,
ensure that event carries the full thread (CorrelationId, CausationId, actor); if not, add a small
past-tense <Action>ed event and publish it through the outbox, and add/confirm the Audit consumer
records it.

CONSTRAINT: Follow .claude/rules/audit.md and .claude/rules/messaging.md. The event is a Contracts
record with the thread fields; the Audit append is idempotent; the actor comes from the propagated
identity, never the body.

RESTRICTION: Do NOT put secrets/PII in the event or the trail. Do NOT let the Audit consumer call back
into a service or change domain state. Do NOT open a second database.

USAGE: Use the add-audit-event skill (publish side in <service>, consume side in Audit). Delegate
review to the code-reviewer + api-contract-checker; run the audit-coverage-checker.

BEHAVIOR: Plan the event shape, the publish point, and the Audit append, and wait for approval.
Implement, run the publisher's and Audit's tests, then demonstrate the row landing (including a
redelivery to prove idempotency), and summarize.
```

---

## Pro tips

- **Prove one lifecycle before chasing coverage.** A1–A5 make the spine known-good on the events that
  already exist; only then does A7 fan out to the actions that don't. Resist auditing everything at once.
- **The Contracts change is the risky step.** A1 touches every publisher and consumer — approve the
  plan, lean on the api-contract-checker, and run *all* the suites.
- **Actor is only as good as ADR-0011.** Without the edge projecting a trustworthy identity, "who did
  it" is a guess. A2 is a prerequisite, not a nicety — and it's a spoofing surface, so security-review it.
- **The trail is disclosable.** It's durable and queryable by support and agents — keep secrets and
  needless PII out of the jsonb from the start; you can't un-record them.
- **Promote repeats to the skill.** If you fill in "Audit a new action" two or three times, the shape
  is already the `add-audit-event` skill — reach for it and the prompt disappears.
```
