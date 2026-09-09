# Implementation instructions

The owner's 8 September 2026 implementation authorization overrides the earlier naming decision only: do not put the planning codename in source identifiers, namespaces, assemblies, packages, project metadata, executable names, comments or runtime strings. Use stable `OpenFantasy` names. Preserve historical document names and the existing planning-folder path. This is not a scope reduction or a request to rename archived source.

The reviewed `spec-v1_1` documents remain the design baseline. `implementation/` records current work, owner overrides, measured evidence and pending exits. Preserve reviewed snapshots and hashes; do not rewrite an unrun qualification into a pass merely because a scaffold builds.

Implementation lives in `Runtime/`. Keep one portable C# core, an in-process host and a Godot C# presentation adapter. Use retained tests and real state whenever behavior is claimed. Core code must not reference Godot or depend on client timing. Do not introduce an IPC bridge or another authority without the recorded qualification evidence.

Preserve unrelated staged predecessor changes. The predecessor project compiles only its `src/` tree; this directory's `.gdignore` prevents its editor from importing nested projects. Validate those boundaries after build changes. Original source archives remain immutable; record any deliberate integration-only predecessor configuration changes separately.

The central task owns integration. Assign bounded file ownership to workers and independent critics. Artists own native asset production separately from engine builders. Codex and Claude may use the same contracts and evidence. Do not invoke a heavyweight game-generation workflow by default.

## Runtime application style

The owner requires modular, human-readable, object-oriented application and engine code. This preference applies to the runtime C# files; supporting scripts and tools are outside this style-refactor scope.

- Give each type a cohesive domain or application responsibility. Keep entry points and engine callbacks short, delegating named operations to the appropriate owner.
- Encapsulate mutable state and enforce invariants through behavior. Use immutable value objects for identities, instants, quantities and snapshots. Preserve explicit deterministic ordering and byte contracts during refactors.
- Prefer composition and concrete types. Introduce interfaces for a real replaceable boundary or required test seam, and inheritance only for a valid substitutable relationship. Pure arithmetic/encoding helpers may remain static.
- Keep host diagnostics and presentation outside the portable authority. Own editable layout in scenes and semantic behavior in C#; use explicit dependencies rather than global service lookup.
- Use descriptive names, consistent private field naming, visible guard blocks and named constants for format structure. Comments explain units, invariants and reasons. Avoid compressed control flow, giant managers and speculative abstraction layers.
