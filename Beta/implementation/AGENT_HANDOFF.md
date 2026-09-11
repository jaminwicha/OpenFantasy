# Current shared agent handoff

Updated: 2026-09-10. This is a navigation and current-state index for any authorized lead or worker. The project files below, not a model's private memory, carry the specification and implementation evidence. Update this index when its current-state claims change; preserve earlier reviewed evidence and design editions.

Current orchestration steering: Astra drives this project while Claude performs suitable bounded work using the owner's Claude capacity. Preserve interchangeable capabilities, but avoid spending another Astra subagent merely to relay a Claude task. Before every external delegation, the ecosystem's local hook must synchronize approved skill updates and verify the exact current instructions, handoff, specs and owner steering; halt on unexpected drift. Recheck inputs after return, then update shared knowledge from reviewed results. The check itself must not consume another model conversation. Measure recurring lead/worker usage and accepted work before claiming token or credit savings; setup effort is a separate one-time cost.

## Authority and reading order

1. Read [project implementation instructions](../AGENTS.md). The owner's later naming instruction governs source/project identifiers: use `OpenFantasy`, with no planning codename in runtime names, comments or strings. Preserve the existing planning directory and historical documents.
2. Read the [implementation overview](README.md), then the [current construction packet](p01/construction/README.md) for the latest connected prototype. The [progress ledger](p00/PROGRESS.md) retains older increments; older pending/completed statements are scoped to their checkpoints.
3. Read the [living player-interface supplement](PLAYER_INTERFACE_AND_NAVIGATION.md) alongside the preserved [specification baseline 1.1](../spec-v1_1/README.md). The supplement is current scope. Its archived predecessor is not the active UI design.
4. For the assigned work, follow the baseline [GDD](../spec-v1_1/GDD.md), [technical design](../spec-v1_1/TECHNICAL_DESIGN.md), [decision register](../spec-v1_1/DECISIONS.md), [full plan](../spec-v1_1/IMPLEMENTATION_PLAN.md), [requirements](../spec-v1_1/REQUIREMENTS.json) and [coverage](../spec-v1_1/COVERAGE.json). The preserved [original implementation handoff](../spec-v1_1/IMPLEMENTATION_HANDOFF.md) supplies the full reading map; its old naming and preimplementation status are historical where later instructions supersede them.
5. Use [runtime setup and boundaries](../Runtime/README.md) for exact build/launch procedures and the pinned toolchain. Use the [research index](../research-v0_9/README.md), [local reuse decisions](../spec-v1_1/LOCAL_REUSE_DECISIONS.md), [presentation contracts](../spec-v1_1/PRESENTATION_CONTRACTS.md) and [archive continuity audit](../spec-v1_1/ARCHIVE_CONTINUITY_AUDIT.md) when the task needs those details.

## Current implemented behavior and its limits

The retained runtime combines a portable deterministic C# core, an in-process durable host, a headless executable and a Godot C# client. There is one authoritative world; Godot presents outcomes rather than deciding them. Runtime application code must remain modular, human-readable and object-oriented. Prefer cohesive responsibilities and composition; do not introduce an interface, assembly, alternate authority, Rust rewrite or IPC layer merely because future scope is large. Supporting scripts are outside the owner's application-style refactor scope.

The current local farm connects player movement, Wren's source dialogue, finite gathering, ration crafting, a funded six-wood quest, progression/reward and Wren's routed construction of a small physical seedling guard. Wren performs 200 authoritative work actions using owned materials and a kit. Save/reopen, interruption and preserved older-save copy upgrades have retained checks. This is a specific authored loop, not a general autonomous NPC society or general construction system.

The [final integrated qualification](p01/construction/qualification/final-verification.json) records 544 Core plus 261 Hosting tests (805 total), zero skips, 22 independent fixtures, 68 host inspections and native journeys bound to 655 unchanged runtime inputs. [Local Release evidence](p01/construction/qualification/release/runs.json) records the same test total. These are existing checkpoint results, not tests rerun by this handoff or automatic evidence for subsequent edits. The [rendered review](p01/construction/FINAL_VISUAL_REVIEW.md) and [readiness checklist](p01/construction/READINESS.md) state their bounded technical claims. A build directory named `ExportRelease` does not establish a packaged export or Windows/Linux execution.

At this handoff, repository history includes:

- `7ab355b`: first farm feedback build qualification and review evidence.
- `38b0375`: layered action UI and capability-aware navigation planning.
- `10985aa`: icon-led interfaces and contextual inspection planning.

These commits are provenance anchors, not an instruction to reset to them. Inspect the actual current revision and working tree before work.

## Owner feedback and presentation direction

The owner has now tried the game and described it as very basic with terrible graphics. That later feedback supersedes the older checkpoint wording that human feedback had not happened. It does not establish a completed usability study, enjoyment acceptance or rejection of the retained architecture. The visual and experience concerns remain open; automated technical success cannot close them.

The production target is Sea of Stars-level pixel finish, lively expressive animation and responsive world interaction, with restrained detail, visible accumulated history and variation that avoids obvious repeated sprites. Native sprites, motion, lighting, weather, wear and audio must follow actual gameplay state. Accepted concept/reference scenes are style options, not approval of every native sprite, animation or a bulk asset-production run. Consult [art calibration](../art-style-calibration/README.md) and presentation contracts before asset work. Dedicated artists own asset production separately from engine builders; human style acceptance remains required.

For UI, favor icons with rich hover, controller-focus and explicit inspection details. Explanations must remain reachable without a mouse, and essential costs/risks must not be hidden only in tooltips. Use contextual actions, stable curated quick wheels, a browsable action library and capability-relevant management screens. Inventory, stats, skills, crafting, journal, local/world maps, calendar and atlas belong to the full design; irrelevant inert screens should not clutter early play. Capability exposure, target/confirm behavior, input capture, layers/realms/history and knowledge-filtered details share semantic contracts. The [UI coverage register](ui-management/COVERAGE.json) has 15 supplemental contracts and 14 unrun acceptance cases. The supplement assigns these to existing phases; the owner did not reprioritize the implementation campaign. Current farm hotkeys and HUD do not implement that full design.

## Retained full campaign

All 181 baseline requirements remain mandatory; the living UI supplement adds refinements without replacing that denominator. Full P00/P01/P02 exits remain open. Windows, macOS and Linux must support 1–4 players per hosted production world; mobile follows through the same core. Actual device, export, concurrency, performance, art/audio throughput and human experience qualification are still required.

Use the full plan's dependencies and exits rather than treating this abbreviated map as a replacement:

| Phases | Retained purpose |
|---|---|
| P00–P03 | Executable contracts/inventory, portable durable authority, a complete inhabited legacy loop, clocks/branches/packages and required early proofs. |
| P04–P06 | Domestic/environmental production, character/class/magic breadth, persistent transport/mounts and frontier residency. |
| P07–P10 | Settlements/trade/competition, historical societies and world events, intersecting procedural narratives, full planes/time/creator capabilities. |
| P11–P14 | Complete original-content import/catalog, full-scope endurance/quality, desktop production/support and mobile completion. |

The full causal world remains the destination: player-equivalent NPC capabilities; individual relationships, motives and life/death; cultural species/craft/equipment/architecture; evolving geology/ecology/liquids/weather; stacked excavation and building; trade and politics; classes/magic/skills; mysteries, puzzles and narrative; arbitrary historical access, planes and almost-infinite-feeling frontier. Full original OpenFantasy content assimilation and selected RogueFiction/other-project lessons remain required as specified. These families must connect through actual state, actions, knowledge, presentation and consequences. Inventorying a family or adding a data type does not complete it.

## Work packets, evidence and ecosystem knowledge

The central task owns integration and phase sequencing. Codex and Claude can exchange lead/worker roles under the same project contracts. Antigravity is suitable for bounded maintenance. Each delegated packet must identify objective, requirement/phase, exact starting revision, owned files, relevant contracts, invariants, permitted tools/actions, acceptance checks, limits and stop conditions. Workers are not alone; preserve other edits and do not expand scope or dispatch additional workers without the packet's authority. A returned patch or commit needs integration review; a model's success statement is not verification.

Use isolated worktrees or explicitly disjoint ownership. Before starting, record `git status --short` and inspect the index. This checkout had 19 unrelated staged predecessor files at the 2026-09-10 handoff; treat that as a preservation warning, not an invariant to recreate. Do not stage all files, reset the index or include those changes in successor commits. Commit completed owned increments along the way through the integration owner.

Return exact files, commands and results, toolchain/revision, evidence paths, critic disposition and unresolved limitations. Distinguish design criticism, executed automated/native checks and human acceptance. A virtual playtester must observe an actual build to claim a playtest. Do not launch, interrupt or close an owner's active game solely to prepare a review; coordinate any required interactive test through the lead.

Reusable workflow lessons belong in the sibling [GameDev ecosystem](../../../GameDevAIEcosystem/AGENTS.md), its maintained `skills/` and `skill-overlays/`, and its native agent mappings. Keep shared concepts portable while preserving model-specific configuration formats, permissions and callable-tool differences. Project facts stay here or in the linked contracts; universal skills should route to the local project handoff rather than embedding its entire GDD. Installation or synchronization alone does not prove that a delegated model can use a skill, authenticate or produce acceptable work; retain a bounded behavioral probe when qualifying the integration.

The [delegation context recipe](DELEGATION_CONTEXT.json) identifies the shared inputs for the
local preflight hook. The ecosystem's `prepare_context.py` fills hashes after the lead updates
steering; it does not require an additional model conversation. Supply resolved external roots
when using an isolated worktree. Context hashing does not grant worker file access: pass only
needed external skill/document directories explicitly. A contract-editing task must preserve
immutable input snapshots and identify editable outputs separately.
