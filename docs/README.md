# docs

Planning and prompts for building JobBoard with Claude Code.

- **`scrub-prompts.md`** — the SCRUB prompts that stand the system up and keep it consistent.
  - *Part 1 — Scaffolding:* a one-time, ordered sequence. Run the prompts in order, one at a time, to create `src/` — the shared spine first, then one proven service + the full event loop, then the rest.
  - *Part 2 — Operational templates:* reusable prompts for the recurring, high-stakes moments (feature slices, cross-service events, migrations, refactors, debugging).
- **`ongoing-architecture-plan.md`** — a grounded architecture review of the current spike (physical, logical, conceptual — service by service, seam by seam, backend and UI), benchmarked against Microsoft and industry practice, with a ranked risk table and a 30/60/90 day plan.

Everything the prompts rely on — the constitution (`../CLAUDE.md`) and the toolkit (`../.claude/`: rules, skills, subagents, hooks) — is already in the repo. Start a Claude Code session at the repo root and begin with Part 1, Prompt 0.
