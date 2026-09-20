# Implementation instructions

The owner's 8 September 2026 implementation authorization overrides the earlier naming decision only: do not put the planning codename in source identifiers, namespaces, assemblies, packages, project metadata, executable names, comments or runtime strings. Use stable `OpenFantasy` names. Preserve historical document names and the existing planning-folder path. This is not a scope reduction or a request to rename archived source.

The reviewed `spec-v1_1` documents remain the design baseline. `implementation/` records current work, owner overrides, measured evidence and pending exits. Preserve reviewed snapshots and hashes; do not rewrite an unrun qualification into a pass merely because a scaffold builds.

## Architecture reference and maintenance hook

[Runtime/docs/architecture/](Runtime/docs/architecture/README.md) is the maintained encyclopedic
reference. Read the affected subject before changing its architecture; use source links rather than
reloading the research archive. Backfill newly discovered undocumented behavior as part of that work.

For each coherent implementation change, check whether it changes ownership, interfaces, data flow,
canonical state/bytes, invariants, timing, persistence, generation, input or presentation contracts.
Update the affected reference pages and dependent explanations in the same change before committing.
When no architectural contract changes, a brief no-impact note in the existing work record suffices;
do not manufacture a documentation edit or separate report for every small fix.

Write present-tense, timeless explanations: responsibilities, rationale, rules, boundaries and extension
contracts. Separate concrete runtime bindings, standalone domain contracts and design obligations.
Do not describe a planned feature as implemented because its types or tests exist. Keep phase names,
dates, progress, temporary gaps, measurements and validation history in `implementation/`; do not turn
the reference into a changelog. Preserve frozen specs and archives. Reconcile discrepancies explicitly
in the work record; a narrower binding must not silently reduce the full design.

At a phase handoff or substantial architectural adaptation, review affected source-to-reference
bindings and cross-system dependencies together. Check links and named APIs; use focused factual
critique when the change warrants it. Documentation-only changes need document checks, not unrelated
runtime regressions. This hook supplements, and does not weaken, any required acceptance gate.

For player interfaces and input, also read the living `implementation/PLAYER_INTERFACE_AND_NAVIGATION.md` supplement and its phase/evidence mappings. It refines the baseline with the owner's wheel, capability-dependent menu, map/calendar and navigation direction; it does not reprioritize phases or claim those interfaces are implemented.

Implementation lives in `Runtime/`. Keep one portable C# core, an in-process host and a Godot C# presentation adapter. Use retained tests and real state whenever behavior is claimed. Core code must not reference Godot or depend on client timing. Do not introduce an IPC bridge or another authority without the recorded qualification evidence.

Owner decision, 2026-09-20: retain C# for the core and application to keep development streamlined and
simple. The [C++ assessment](implementation/CORE_LANGUAGE_CPP_ASSESSMENT.md) is retained research, not
an active migration or native-kernel backlog. Keep the portable core decoupled; address measured costs
through C# algorithms, data layout, allocation and scheduling while preserving all acceptance gates.
Do not add a C++/Rust core, custom native calculation layer or IPC worker without a new explicit owner
decision. Existing Godot/.NET/storage dependencies are unaffected by this application-language choice.

Preserve unrelated staged predecessor changes. The predecessor project compiles only its `src/` tree; this directory's `.gdignore` prevents its editor from importing nested projects. Validate those boundaries after build changes. Original source archives remain immutable; record any deliberate integration-only predecessor configuration changes separately.

The central task owns integration. Assign bounded file ownership to workers and independent critics. Artists own native asset production separately from engine builders. Codex and Claude may use the same contracts and evidence. Do not invoke a heavyweight game-generation workflow by default.

## Runtime application style

The owner requires modular, human-readable, object-oriented application and engine code. This preference applies to the runtime C# files; supporting scripts and tools are outside this style-refactor scope.

- Give each type a cohesive domain or application responsibility. Keep entry points and engine callbacks short, delegating named operations to the appropriate owner.
- Encapsulate mutable state and enforce invariants through behavior. Use immutable value objects for identities, instants, quantities and snapshots. Preserve explicit deterministic ordering and byte contracts during refactors.
- Prefer composition and concrete types. Introduce interfaces for a real replaceable boundary or required test seam, and inheritance only for a valid substitutable relationship. Pure arithmetic/encoding helpers may remain static.
- Keep host diagnostics and presentation outside the portable authority. Own editable layout in scenes and semantic behavior in C#; use explicit dependencies rather than global service lookup.
- Use descriptive names, consistent private field naming, visible guard blocks and named constants for format structure. Comments explain units, invariants and reasons. Avoid compressed control flow, giant managers and speculative abstraction layers.

## Verification and milestones

Use proportionate verification: focused checks and relevant native behavior during development;
broader regression at coherent integration checkpoints or shared-behavior changes. Prefer useful
slices over repeated microtask ceremonies. Preserve every feasibility/acceptance gate and its
thresholds; independent review remains mandatory wherever specified. Retain failures, distinguish
local review from independent/human acceptance, and keep one compact current handoff linking
evidence. Do not reload or duplicate historical qualification detail for routine work.

Native fixtures follow the shared [verification skill](../../GameDevAIEcosystem/skills/game-verification-loop/SKILL.md):
synchronize live state with presented offers and settled input, enforce deadlines on successful waits,
and prove the intended rejection and complete entity identities. Strengthening a probe never authorizes
automatic command retries, relaxed deadlines or changes to game rules merely to make it pass.

The owner's 2026-09-10 direction makes incremental commits and milestone branches project
doctrine. Follow [GIT_MILESTONES.md](implementation/GIT_MILESTONES.md): commit verified coherent
increments, preserve major checkpoints with named branches, and never include unrelated
staged work. A milestone records the exact accepted scope; it does not automatically close a phase.

When diagnostic or counterfactual calculations enter live authority, retain separate permission checks.
Proposed holes, unearned resources and alternate policy choices may be valid analysis inputs but invalid
persisted state. Keep an explicit authority entrypoint and a regression where diagnostics accept an
otherwise valid proposal that complete world admission must reject.

## Shared desktop

Owner clarification, 2026-09-11: other conversations run Godot games on this desktop and can take
foreground focus. Headless checks do not need desktop focus, but all runs share CPU/memory.
Use explicit background rendering fixtures for controlled UI/image evidence; normal gameplay
keeps its native focus/cancellation/readiness policy. Foreground OS/device acceptance requires
a coordinated interval. Do not repeatedly grab focus, close another task's applications, extend
deadlines or convert interrupted runs to passes. Record observed window/Control focus and host
readiness; keep failures and their original assertions. Continue independent work when foreground
access is unavailable. Controlled-focus rendering never substitutes for physical/OS acceptance.

For deadline-sensitive validation, avoid overlapping this task's heavy builds, full regressions and
resource-intensive probes. Continue lightweight independent work. Preserve timing failures and repeat
a diagnostic with the same command/threshold after removing this self-imposed overlap; a later pass
alone neither establishes contention as the cause nor closes a production latency/scale gate.
