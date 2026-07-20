# Vendored skills — not authored here

This repo's *own* skills (`add-endpoint`, `add-component`, `add-aspire-resource`) are maintained here.
The skills below are **third-party**: they ship from upstream (Microsoft for the Aspire skills,
Playwright for browser automation) and are meant to be **vendored in as-shipped**, then re-vendored
when upstream updates. They are intentionally **not** included in this toolkit — hand-writing our own
copies would fork content that's supposed to track someone else's releases and go stale silently.

Drop them into `.claude/skills/` when you want them. Each is a folder with its own `SKILL.md` (several
also carry a `references/` subfolder). Once present, they load on demand like any other skill; they're
harmless no-ops until the tools they call (`aspire`, `playwright-cli`, `npx`) and `src/` exist.

## The Aspire skill family (Microsoft, MIT)

A router skill plus focused sub-skills for driving the Aspire CLI and AppHost. Vendor the whole family
together — the router hands off to the others.

| Skill | What it does |
|---|---|
| `aspire` | Top-level router for Aspire distributed apps: detects the AppHost, enforces safety guardrails, routes to the sub-skills below. |
| `aspire-init` | First-run flow for adding Aspire to a repo (`aspire new` greenfield vs `aspire init` existing), drops the AppHost skeleton, hands off to `aspireify` for resource wiring. |
| `aspire-orchestration` | AppHost lifecycle (`start`/`stop`/`wait`/`ps`) and recovery from file locks, port conflicts, and orphaned processes. |
| `aspire-deployment` | Publish/deploy an AppHost model to Docker Compose, Kubernetes, Azure, or AWS (out of scope for this local-first PoC, but useful later). |
| `aspire-monitoring` | Observe a running app: logs, traces, metrics, resource state, telemetry export, the standalone dashboard. |

**Why they help here:** the scaffolding prompts (`docs/prompts/scrub-prompts.md`) lean on the Aspire CLI, and
`aspire-init`/`aspireify` are exactly what Prompt 0 mentions for standing the solution up. `aspire`
and `aspire-orchestration` make the everyday `aspire run` / start-stop loop smoother.

**Note on `aspireify`:** the Aspire tooling ships an `aspireify` skill (referenced by `aspire-init`
and by our `.claude/README.md`) that scans the repo and wires the AppHost. It comes from the same
upstream — vendor it alongside the family if your Aspire CLI version provides it.

## Browser automation (Playwright)

| Skill | What it does |
|---|---|
| `playwright-cli` | Automate browser interactions, test web pages, and work with Playwright tests via the `playwright-cli`. Useful for end-to-end checks of the Angular app through the gateway (see Prompt 10). |

## How to vendor

1. Get the upstream skill folder(s) from the source that ships with your installed tooling —
   the Aspire skills travel with the Aspire CLI / its skill package (see https://aspire.dev), and
   `playwright-cli` with the Playwright tooling / the Claude Code skill catalog.
2. Copy each folder **verbatim** into `.claude/skills/` (keep its `SKILL.md` and any `references/`).
3. Don't edit vendored files — if something's wrong, fix it upstream or override behavior in our own
   skills/rules. Keeping them pristine is what lets you re-vendor cleanly on the next update.
4. Record the version/commit you vendored (a line in this file is enough) so you can tell when it's
   stale.

## Verify before trusting
Exact skill names, contents, and their distribution channel move with the tooling. Confirm what your
installed Aspire CLI and Claude Code actually ship — and the current `aspire`/`aspireify` surface —
against https://aspire.dev and https://code.claude.com/docs before relying on any specific command a
vendored skill documents.

---

### Vendored inventory (fill in as you add them)

| Skill | Vendored from | Version / commit | Date |
|---|---|---|---|
| _(none yet)_ | | | |
