# Claude project entrypoint

Read `AGENTS.md`, then `Beta/AGENTS.md` and `Beta/implementation/AGENT_HANDOFF.md` for successor work. These shared files contain the authoritative project instructions, current evidence and living-design links. Follow the assigned work packet's scope and read its referenced contracts; this file does not duplicate the GDD.

Claude and Codex are interchangeable leads and workers. A delegated task does not transfer integration ownership or authorize further delegation outside its packet. Return exact changed files, checks actually run, result evidence and unresolved concerns. Keep project knowledge in the shared handoff/contracts so the next authorized lead can continue without this session's memory.

Owner steering, 2026-09-19: Astra implements directly by default. The owner gives standing
approval and recommends Claude Fable for bounded review/critic assignments, using exactly
`fable[1m]`; these reviews do not need a new approval question each time. Prepare a concrete
scope, expected benefit and finite time/usage limit, and use read-only tools by default.
Implementation or other non-review delegation still requires explicit approval per assignment.
No silent model substitution, automatic paid retry or relay agent. Keep review proportional:
one useful independent critic at a coherent checkpoint, not duplicate ceremonial reviews.
Report failures and friction honestly; unchanged correctness and acceptance gates still apply.
Every external dispatch uses the local skill-sync/context-freshness hook and compact evidence. Follow the current shared handoff.

Use relevant installed skills selectively. Their maintained source and synchronization policy live in `GameDevAIEcosystem`, a sibling of the canonical checkout. In an isolated worktree, the work packet must supply its resolved path and the required skill roots; report missing references rather than assuming sibling layout or reinstalling tools. An installed skill or a mention of an MCP tool does not prove the capability is callable in this session. Preserve Claude's native permission and configuration formats; do not copy another agent's settings wholesale.
