# Beta OpenFantasy — direction addendum v0.3

8 September 2026. Applies alongside the frozen v0.2 GDD and requirements baseline. This is an additive direction and decision record, not a claim of implemented features or a replacement for the full GDD.

## Established direction

These preferences are established by the owner and supersede conflicting workflow/art language in v0.2 or older ecosystem files:

1. **Codex and Claude are interchangeable.** Either can own research, GDD/TDD work, difficult simulation, broad implementation, tools, verification and review. Use task context and demonstrated results rather than fixed vendor specialties. An implementation handoff is not required just because the design was prepared by a different model.
2. **Antigravity defaults to mundane maintenance.** Assign bounded formatting, documentation, asset organization and inventory tasks. Larger responsibility requires a changed assignment.
3. **Classic lineage is a spiritual homage.** FFVI and Chrono Trigger inform data-driven authoring, field/encounter continuity, expressive scenes and readable systems. Their machine limits, data formats, fixed maps and single-player assumptions do not constrain the new architecture.
4. **Complete original OpenFantasy assimilation is required.** Preserve and port all original concepts, assets, entities, quests, classes, monsters, NPCs, narratives, places and systems into Beta's fabric. Account for planned and superseded concepts explicitly. Implementation can be reworked; no content is silently discarded. Deepen selected RogueFiction contributions to narrative, NPCs and questing in the larger society, daily-life and building simulation.
5. **Presentation targets high-quality pixel art.** Chrono Trigger is the initial style calibration reference, with Sea of Stars and Stardew Valley informing the expected finish and detail. Close emulation of the pixel-art style may be used for an original baseline specimen; existing named characters, copied sprites or ROM assets are not required.
6. **Dedicated artist agents own art generation.** Their work is separate from engine building. They select tools based on authentic output, practical MCP/API/CLI coverage, editing and export capabilities. Model brand does not determine who may occupy either role.
7. **The user accepts the art style.** People, creatures, objects, terrain and other categories calibrate to the accepted reference set. Record actual acceptance and its scope. Restrained v06 is now endorsed as the visual direction for five additional scenes based on original OpenFantasy content. Earlier options remain preserved. Conforming work reuses that acceptance; material style drift or a new visual requirement reopens the relevant decision. Native asset and motion validation remain separate.
8. **Useful reusable preferences persist.** Maintain shared instructions in the GameDev AI ecosystem and synchronized skill sources. Keep game-specific references and design choices here in Beta.
9. **Preserve deterministic, seeded simulation as the foundation.** Carry suitable original algorithms and tests forward; extend the contract to durable world changes and accepted history rather than replacing simulation with unrecorded narration.
10. **Keep the core engine independent.** Support later optimization, alternate engine hosts, other games and platforms through clear data/action boundaries without adding speculative language or IPC layers. Core event/data authoring should spiritually evoke FF6/Chrono Trigger using original implementations and content, without inherited hardware limits or copied proprietary material.
11. **History should be visible in the art.** The world carries deep time; objects and places may show decades or centuries of use, repair, inheritance and adaptation. Additional detail should produce magic and atmosphere through meaningful materials and history, while retaining readable, inviting pixel art. See the [history and materials brief](/Users/alucinor/Workspace/Projects/OpenFantasy/Beta/art-style-calibration/history-and-materials.md).
    The v05 review confirms the ideas while requesting slightly less visual density. Apply magic, wear and ornament selectively; preserve quiet surfaces and a clear hierarchy rather than increasing detail everywhere.
12. **Time and exposure change the world and its art.** Days, sunlight, overcast/clouds, night, wind, storms and regional seasons affect presentation and relevant simulation. Accumulated exposure and use wear objects over days and years; material-appropriate repair/upkeep changes their condition and leaves history. See [environment, aging and maintenance](/Users/alucinor/Workspace/Projects/OpenFantasy/Beta/environment-aging-and-maintenance.md).
13. **Art and gameplay are integrated.** Changing appearance communicates actual world conditions, consequences and opportunities for action. Simulation, design and artist roles share this contract: condition, history, care and repair must be meaningful in play, not only decorative treatments.

14. **Godot with C# is selected for multiplatform implementation.** Retain the engine-independent core. Pin and validate the actual toolchain/export targets; do not interpret multiplatform as proof of every device or browser export.
15. **Early implementation is retained.** Build the real modules, content contracts and regression fixtures incrementally. No disposable engine or competing implementation is planned; evidence can still require improving a design.
16. **Combat uses on-field command/timeline play with Wait-style decisions.** The owner selected the Chrono-like direction without real-time mode. Cooperative techniques remain part of that direction. Exact encounter/world pause and multiplayer boundaries require a concrete contract.
17. **Reduce conspicuous sprite repetition.** All five reference scenes are positively received. Vary incidental silhouettes, construction, poses and placement within/across scenes while retaining the accepted detail budget and recognizable identities.
18. **Characters, animals and objects must feel alive and interactive.** Support personality quirks, idles, specific actions, emotions, gestures, reactions and interactions. Consider puppetry for events, narration, conversations and encounters; see [expressive animation and choreography](expressive-animation-and-choreography.md).
19. **Persistent relationships and deep stories belong to the full simulation.** NPC-to-NPC relationships and affinities matter alongside player relationships. Authored and emergent arcs retain participants, memories, obligations and consequences across time, absence and saves.

20. **Archive prior GDD versions.** Preserve prior editions and their companion registers/handoffs before consolidation; publish the next synthesis as a new version. See [version archives](archive/README.md).

## Current art acceptance

The [calibration package](/Users/alucinor/Workspace/Projects/OpenFantasy/Beta/art-style-calibration/README.md) contains six preserved style concepts, measured file limitations, tool research and an [acceptance record](/Users/alucinor/Workspace/Projects/OpenFantasy/Beta/art-style-calibration/acceptance.json). Status: **v06 endorsed for original-content reference scene expansion**. The [v06 image](/Users/alucinor/Workspace/Projects/OpenFantasy/Beta/art-style-calibration/restrained-history-proposal-v06.png) keeps layered construction, wear, repairs and localized magic while reducing decorative overload. It is a still concept, not proof of weather, seasons, aging or gameplay integration.

No concept has passed native pixel-asset validation. These are not production sprite sheets or verified animation sets. Style approval and production validation are separate: the user may approve a visual direction while its native sprites still require precise artist work. Mass production waits for acceptance; independent architecture/prototype work can use labelled placeholders.

The initial shortlist is PixelLab for a specialist generator trial plus Aseprite for exact editing and export. These are candidates supported by primary documentation, not locally proven winners. Neither integration was callable in the reviewed session, and no paid service or MCP installation was made. See [tooling research](/Users/alucinor/Workspace/Projects/OpenFantasy/Beta/art-style-calibration/pipeline-research.md).

## Proposed decisions requiring product/technical adoption

The following are recommendations from the review. They are **not silently promoted to owner-approved requirements**:

| Decision | Proposed direction | Evidence/revisit trigger |
|---|---|---|
| Product identity | An adventuring company in a beautiful world that remembers actions; home/community reinforces adventure | Playtest the connected expedition/recruitment/making loop |
| Runtime strategy | Independently bounded Beta runtime with selective module extraction and complete content migration | Preserve seeded deterministic philosophy; rebuild persistence/state contracts; pure simulation extraction already compiles |
| Engine/toolchain validation | Godot/C# selected by owner; pin versions and validate supported exports | Platform matrix and actual exported-build evidence |
| World profile | Adapt Aldermere's reconstruction/Aethertech identity into generated cultures, landmark roles and art kits | Do not replace the generated-world requirement with one fixed map |
| Combat clock details | Wait-style on-field command combat selected; specify encounter/world clock and co-op interactions | Retained control/readability/co-op tests; no competing real-time-action build |
| Persistence | Stable world/branch/entity identity; checkpoints plus commands, accepted decisions and atomic receipts | Test crashes, duplicate requests, generator/package changes and fresh-process restore |
| Storage | SQLite for a bounded initial host; active simulation in memory | Benchmark real mutation/checkpoint loads; revisit for scaling requirements |
| Time travel | Supported checkpoints and explicit branch-on-intervention; no implicit branch merging | Prove artifact identity and co-op isolation before campaign production |
| Extensibility | Typed declarative packages and small effect/process vocabulary first | One meaningful behavior extension, not only a reskin; introduce sandboxed code only if needed |
| Simulation resolution | Detailed local simulation plus explicitly bounded coarse distant work | Test identity/resource conservation under repeated activation changes |
| Release scope | Preserve the full vision; assign domain proofs and release priorities | No credible release date until second-region and accepted-art throughput are measured |

## Required original content and deeper RogueFiction opportunities

The following highlights are not the full import scope. The [complete migration plan and inventory](/Users/alucinor/Workspace/Projects/OpenFantasy/Beta/content-migration/README.md) govern all original content. The [narrative simulation proposal](/Users/alucinor/Workspace/Projects/OpenFantasy/Beta/narrative-simulation-design.md) develops the RogueFiction opportunities in detail.

| Source | Element | Integration constraint |
|---|---|---|
| OpenFantasy | Hopeful postwar reconstruction and Aethertech identity | World-profile content rather than universal engine law |
| OpenFantasy | Consequential ships, canoes, airships and regional travel | Connect routes, cargo and expedition preparation; avoid travel existing only as a menu |
| OpenFantasy | Seeded frontier settlements, actual-destination rumors, varied site families | Immutable definitions, durable instances and bounded residency replace mutable global tables |
| OpenFantasy | Topology, locks/secrets and deterministic validators | Extract tested algorithms with new stable addresses/version contracts |
| RogueFiction | Listen/smell/examine/search and atmospheric feedback | Observations come from scoped state and do not consume authoritative RNG |
| RogueFiction | Noisy prying and enemy memory/response | Shared action/effect/sensing rules, not separate scripted copies |
| RogueFiction | Narrative predicates/effects, vows and consequential choices | Persistent typed story instances replace a global flags-only model |
| RogueFiction | Alternating focused and exploratory dungeon topology, meaningful return trips | Current topology inheritance is counted once; session snapshots do not substitute for disk persistence |

## First decision package

Use the [decision review](/Users/alucinor/Workspace/Projects/OpenFantasy/Beta/architecture-decision-review-2026-09-08.md) for the migration plan, feasibility gates and concrete code evidence. Use the [architecture research](/Users/alucinor/Workspace/Projects/OpenFantasy/Beta/reference-architecture-research-2026-09-08.md) for documented lessons, inference boundaries and experiments. Use the [requirements delta](/Users/alucinor/Workspace/Projects/OpenFantasy/Beta/requirements-delta-v0_3.json) for machine-readable changes without losing the 79-requirement baseline.

Before implementation starts, consolidate the latest decisions and close the contracts identified by the [readiness audit](implementation-readiness-audit-2026-09-08.md). Godot/C#, retained code and Wait-style combat are selected; the new persistence/runtime organization and remaining policies still need technical design. Preserve the staged original milestone work. Establish nested C#/Godot project exclusions before adding executable source under Beta. A full production GDD revision should consolidate decisions only as they are actually adopted.
