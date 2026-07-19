---
name: trace-a-request
description: >
  Reconstruct what happened to a JobBoard request or entity, cradle-to-grave, from the support audit
  trail. Use for support/investigation questions — e.g. "what happened to application X", "why did this
  candidate get two emails", "show everything user Y did today", "trace correlation id Z end to end".
  Queries auditdb (through the gateway) by CorrelationId, entity id, actor, or time window and returns
  an ordered, causal timeline. Read-only — it reads the trail, it never changes anything.
---

# Trace a request

The support audit trail ([ADR-0014](../../../docs/adr/0014-audit-bounded-context-bus-fed-support-trail.md))
is a durable record every business event lands in, threaded by the ids from
[ADR-0013](../../../docs/adr/0013-correlation-causation-identifiers-on-events.md). This skill uses it to
answer "what happened to X" — for a human operator or an agent — **without** touching any service's
own database. It is **read-only**; it reconstructs, it never repairs.

Read [`.claude/rules/audit.md`](../../rules/audit.md) first. The one rule that shapes everything here:
**query through the gateway's audit route, never `auditdb` directly.**

## Pick the entry point

Start from whatever the question gives you:

| You have | Query by | Gets you |
|----------|----------|----------|
| A request/trace id | **`CorrelationId`** | the *whole fan-out* of one originating request across every service |
| An application / job / account | **entity id** | that entity's *entire life* (every event that touched it) |
| A user / employer | **actor** | everything *that person did* (optionally within a time window) |
| "Around 14:00 yesterday" | **time window** | everything that happened in a span, to narrow to a correlation |

Most investigations **funnel**: start broad (actor or time), find the `CorrelationId` that matters, then
pull that request's full trail and read its causal structure.

## Steps

1. **Choose the axis** from the table above and hit the **gateway audit route** (auth required) with the
   corresponding filter. Never query `auditdb` directly and never join across a service's own tables —
   the trail is the single source for "what happened."

2. **Order the rows** by occurred-at to get the timeline, then **read the causal tree** via
   `CausationId`: the row whose `CausationId` equals the originating request is the root; each other
   row hangs off its parent (`CausationId` → parent's event `Id`). This turns a flat list into
   "A caused B caused C" — e.g. *close job* → `JobClosed` → `ApplicationStatusChanged` (auto-closed) →
   a notification recorded.

3. **Read the actor and payload** on each row: *who* did it (the propagated identity, ADR-0011) and the
   event detail in the `jsonb` payload. Support-facing fields only surface here — secrets/PII were kept
   out at write time (audit.md), so if something you need isn't recorded, that's a coverage gap, not a
   query you should work around by reaching into a service's DB.

4. **Report the story, not the rows.** Summarize as an ordered, attributed timeline: at T, actor did
   action on entity, which caused …, ending in …. Call out the `CorrelationId` you traced so it can be
   re-queried.

## When the trail comes up short

- **A step you expected is missing** → the action likely isn't audited yet. That's a **coverage gap** —
  fix it with the [`add-audit-event`](../add-audit-event/SKILL.md) skill (and the audit-coverage-checker
  subagent finds them), don't paper over it by querying the owning service's database.
- **Two traces that should be one** → a `CorrelationId` wasn't propagated across a hop; check the
  gateway mint/forward and the publish-site stamping (`.claude/rules/audit.md`).
- **Need live latency/spans, not history** → that's engineering telemetry (the Aspire dashboard), a
  *different* use case; the trail answers "what happened," not "how slow" (ADR-0013 alternatives).

## Checklist before trusting the answer
- [ ] Queried the **gateway audit route**, not `auditdb` or any service's database
- [ ] Anchored on a single **`CorrelationId`** for the request-level story (or a clearly stated entity/
      actor/time filter)
- [ ] Ordered by time **and** reconstructed the **`CausationId`** tree — not just a flat list
- [ ] Attributed each step to an **actor** and cited the `CorrelationId` traced
- [ ] Any missing step flagged as a **coverage gap** to fix via `add-audit-event`, not routed around
