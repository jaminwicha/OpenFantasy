# Shared project entrypoint

Owner update, 2026-09-25: for this project's credit-constrained implementation, delegate substantial
coding/testing and independent review units to Claude `fable[1m]` with Max effort (`--effort max`).
Astra integrates and reviews. This supersedes the older implementation-only-by-Astra default below;
follow the detailed delegation doctrine in `Beta/AGENTS.md`, with explicit scope and finite limits.

For the successor game, read [implementation instructions](Beta/AGENTS.md) and the [current agent handoff](Beta/implementation/AGENT_HANDOFF.md) before choosing work. They apply equally to Codex, Claude and other authorized workers. Read only the task-relevant contracts after that; do not reload the whole research archive for every assignment.

The predecessor remains a preservation and migration source. Successor implementation lives in `Beta/Runtime/`, with stable `OpenFantasy` runtime names. Do not infer permission to change predecessor files from a successor assignment.

The central task owns integration. Receive explicit file ownership and acceptance criteria before delegated edits; inspect the current working tree and preserve others' changes. Do not stage, reset or commit unrelated work. Record durable decisions and evidence in shared project documents rather than model-private memory. Model-specific entrypoints route here and must not become competing specifications.

Owner steering, 2026-09-19: Astra implements directly by default. The owner gives standing
approval and recommends Claude Fable for bounded review/critic assignments, using exactly
`fable[1m]`; these reviews do not need a new approval question each time. Prepare a concrete
scope, expected benefit and finite time/usage limit, and use read-only tools by default.
Implementation or other non-review delegation still requires explicit approval per assignment.
No silent model substitution, automatic paid retry or relay agent. Keep review proportional:
one useful independent critic at a coherent checkpoint, not duplicate ceremonial reviews.
Report failures and friction honestly; unchanged correctness and acceptance gates still apply.
Approved work uses the maintained skill/context synchronization hook; shared project contracts remain authoritative.

Commit coherent implementation increments along the way. At verified major progress,
create a preserved milestone branch without switching the checkout. Follow the shared
[commit and milestone doctrine](Beta/implementation/GIT_MILESTONES.md). The integration
lead owns commits/checkpoints while workers share a checkout.

Owner direction, 2026-09-11: apply the risk-based verification cadence in
[implementation instructions](Beta/AGENTS.md). Build coherent useful increments, keep the current
handoff compact, and use targeted evidence/critics proportionately without weakening any gate.
