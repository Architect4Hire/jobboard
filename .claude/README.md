# The `.claude/` folder

This folder is the reusable Claude Code toolkit for **JobBoard** — a job-board platform built as
**Aspire + ASP.NET Core microservices + Angular**, all orchestrated locally by Aspire. The `.claude/`
toolkit is as much the point of the repo as the app itself.

> **Important:** the project's main memory file, `CLAUDE.md`, lives at the **repo root**, one level
> *above* this folder — not inside it. Claude Code auto-discovers `CLAUDE.md` by walking up from your
> working directory, and the root copy survives `/compact`.

## What each piece is, and when it loads

| Path | What it is | When it enters context |
|---|---|---|
| `settings.json` | Shared project settings (incl. hook wiring). Committed. | Read at session start. |
| `rules/aspire.md` | AppHost/orchestration conventions (per-service DBs, Service Bus emulator, gateway). Path-scoped to AppHost/ServiceDefaults. | Loads when Claude touches the AppHost. |
| `rules/backend.md` | Per-service layered conventions + the host/`.Core` split. Path-scoped to the services. | Loads when Claude touches a service. |
| `rules/messaging.md` | Outbox/inbox, dispatcher, idempotency, event contracts. Path-scoped to `Shared`, `Contracts`, and consumers. | Loads when Claude touches messaging. |
| `rules/gateway.md` | YARP conventions — the only public door. Path-scoped to the gateway. | Loads when Claude touches the gateway. |
| `rules/frontend.md` | Angular conventions (talks only to the gateway). Path-scoped to `src/web/`. | Loads when Claude touches the web app. |
| `skills/add-endpoint/` | Playbook for adding an API endpoint or event consumer to a service. | On demand, when the task matches. |
| `skills/add-component/` | Playbook for adding an Angular component. | On demand, when the task matches. |
| `skills/add-aspire-resource/` | Playbook for declaring a new locally-orchestrated resource (database, Service Bus topic, cache, or a whole service) in the AppHost. | On demand, when the task matches. |
| `skills/VENDORED.md` | Documents the third-party skills (`aspire*` family, `playwright-cli`) to vendor in as-shipped — not authored here. | Reference; read when you want to vendor them. |
| `agents/code-reviewer.md` | Read-only reviewer subagent (microservice- and boundary-aware). | When delegated, or `@code-reviewer`. |
| `agents/test-gap-analyzer.md` | Read-only test-gap subagent (flags missing idempotency/rollback tests). | When delegated, or `@test-gap-analyzer`. |
| `agents/api-contract-checker.md` | Read-only subagent: ServiceModel↔Angular drift *and* event↔consumer drift. | When delegated, or `@api-contract-checker`. |
| `hooks/format.sh` | Formats the edited file after each edit. | Runs via the `PostToolUse` hook. |
| `hooks/secret-guard.sh` | Blocks edits that would commit anything credential-shaped (DB/Redis/Service Bus). | Runs via the `PreToolUse` hook. |

Rule of thumb:
- **Rule / CLAUDE.md** = something Claude should *know*.
- **Skill** = a procedure Claude should *follow* when a task matches.
- **Subagent** = work Claude should *delegate* to keep the main context clean.
- **Hook** = something that must happen *no matter what Claude decides*.

## Not committed (personal / local)
- `settings.local.json` — personal overrides, git-ignored on purpose.
- Anything ending in `.local.*`.

## After cloning
```bash
chmod +x .claude/hooks/*.sh
```
Then open a session: `/memory` confirms the rules load, `/agents` shows the subagents. The scaffolding
sequence is in `docs/scrub-prompts.md` — run Part 1 in order to stand the system up.

## The mismatch to expect on a fresh repo
This toolkit ships *before* `src/`. The skills and hooks call `dotnet`/`ng`/`aspire`; they're harmless
no-ops until those tools and the projects exist. `docs/scrub-prompts.md` is how you create `src/`.

## Verify before trusting
Aspire, the Azure Service Bus emulator, YARP, and Claude Code all ship fast. The `settings.json` hook
syntax, subagent frontmatter, and exact Aspire/Service Bus API names (`AddAzureServiceBus`,
`RunAsEmulator`, `AddAzureServiceBusClient`, `AddJavaScriptApp`, YARP wiring) are the most likely
things to drift — confirm against https://code.claude.com/docs and https://aspire.dev.
