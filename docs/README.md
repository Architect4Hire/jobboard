# docs

Planning and prompts for building JobBoard with Claude Code.

- **`scrub-prompts.md`** — the SCRUB prompts that stand the system up and keep it consistent.
  - *Part 1 — Scaffolding:* a one-time, ordered sequence. Run the prompts in order, one at a time, to create `src/` — the shared spine first, then one proven service + the full event loop, then the rest.
  - *Part 2 — Operational templates:* reusable prompts for the recurring, high-stakes moments (feature slices, cross-service events, migrations, refactors, debugging).

Everything the prompts rely on — the constitution (`../CLAUDE.md`) and the toolkit (`../.claude/`: rules, skills, subagents, hooks) — is already in the repo. Start a Claude Code session at the repo root and begin with Part 1, Prompt 0.
