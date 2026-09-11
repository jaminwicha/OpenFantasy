# Shared project entrypoint

For the successor game, read [implementation instructions](Beta/AGENTS.md) and the [current agent handoff](Beta/implementation/AGENT_HANDOFF.md) before choosing work. They apply equally to Codex, Claude and other authorized workers. Read only the task-relevant contracts after that; do not reload the whole research archive for every assignment.

The predecessor remains a preservation and migration source. Successor implementation lives in `Beta/Runtime/`, with stable `OpenFantasy` runtime names. Do not infer permission to change predecessor files from a successor assignment.

The central task owns integration. Receive explicit file ownership and acceptance criteria before delegated edits; inspect the current working tree and preserve others' changes. Do not stage, reset or commit unrelated work. Record durable decisions and evidence in shared project documents rather than model-private memory. Model-specific entrypoints route here and must not become competing specifications.

Owner steering, 2026-09-10: Astra works directly by default, using its native tools and
capabilities without routine inter-agent handoffs. Claude/Fable is an optional exception:
first prepare a concrete bounded task, expected benefit and usage limit, then request the
owner's explicit approval for that assignment before any model call. Earlier authorization
and a request to continue implementation do not authorize a new delegation. When approved,
use exactly `fable[1m]`, with no silent substitution or automatic paid retry. Follow the shared
handoff's local skill/context synchronization hook; keep review and results compact and
report measured friction honestly. Do not create relay agents to perform this process.

Commit coherent implementation increments along the way. At verified major progress,
create a preserved milestone branch without switching the checkout. Follow the shared
[commit and milestone doctrine](Beta/implementation/GIT_MILESTONES.md). The integration
lead owns commits/checkpoints while workers share a checkout.
