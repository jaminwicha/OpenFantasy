# Implementation instructions

The owner's 8 September 2026 implementation authorization overrides the earlier naming decision only: do not put the planning codename in source identifiers, namespaces, assemblies, packages, project metadata, executable names, comments or runtime strings. Use stable `OpenFantasy` names. Preserve historical document names and the existing planning-folder path. This is not a scope reduction or a request to rename archived source.

The reviewed `spec-v1_1` documents remain the design baseline. `implementation/` records current work, owner overrides, measured evidence and pending exits. Preserve reviewed snapshots and hashes; do not rewrite an unrun qualification into a pass merely because a scaffold builds.

Implementation lives in `Runtime/`. Keep one portable C# core, an in-process host and a Godot C# presentation adapter. Use retained tests and real state whenever behavior is claimed. Core code must not reference Godot or depend on client timing. Do not introduce an IPC bridge or another authority without the recorded qualification evidence.

Preserve unrelated staged predecessor changes. The predecessor project compiles only its `src/` tree; this directory's `.gdignore` prevents its editor from importing nested projects. Validate those boundaries after build changes. Original source archives remain immutable; record any deliberate integration-only predecessor configuration changes separately.

The central task owns integration. Assign bounded file ownership to workers and independent critics. Artists own native asset production separately from engine builders. Codex and Claude may use the same contracts and evidence. Do not invoke a heavyweight game-generation workflow by default.
