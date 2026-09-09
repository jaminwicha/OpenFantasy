# Expanded research and design synthesis

The expanded pass examines the full 150-requirement vision, including the interactions that change its feasibility: arbitrary historical travel, living cities, mutable vertical terrain, Wait-style co-op, expressive native animation and progressively revealed mechanics. Seven new dossiers provide approximately 19,000 words of analysis in addition to the [five preceding studies](../research-v0_7/README.md) and the [original source/reuse review](../architecture-decision-review-2026-09-08.md). This is supporting research for the [current specification](../spec-v1_0/README.md), not evidence that Beta has been implemented.

## Research dossiers

| Dossier | Evidence and important application |
|---|---|
| [Terrain, runtime and worlds](terrain-runtime-and-worlds.md) | Dwarf Fortress, Terraria, Minecraft, Factorio and storage/runtime sources inform sparse real 3D terrain, deterministic work, history and persistent frontier constraints. |
| [Society, lifecycles and emergent stories](society-lifecycles-and-emergent-stories.md) | DF/Qud and other primary design material plus contrasting player reports inform motives, relationships, care, economics, mandates and causal story generation. |
| [Presentation, production and player experience](presentation-production-and-player-experience.md) | Sea of Stars, Fields of Mistria, Chained Echoes, Octopath Traveler 0, The Adventures of Elliot, Crystal Project and related examples inform readable interactions, pace, scope, co-op attention and mobile qualification. |
| [Skills, crafting, magic and dreams](skills-crafting-magic-and-dreams.md) | Noita, Atelier, Cataclysm, Factorio and official tabletop rules inform typed composition, resource conservation, practical professions, earned knowledge and dream boundaries. |
| [Places, puzzles and progression](places-puzzles-and-progression.md) | Actual local roguelike generators, Jaquays's design commentary, Ludomotion, Zelda, Obra Dinn, Golden Idol and BattleTech inform realized layout validation, mysteries, alternative solutions and composite difficulty. |
| [C# versus Rust core reassessment](csharp-rust-core-reassessment.md) | Official language/runtime/platform evidence and local C#/Rust code support a conditional C# selection, with a serious whole-Rust alternative and an early retained workload gate. |
| [Ecosystem, packages and production](ecosystem-packages-and-production.md) | Current local ecosystem contracts and official loading guidance support selective skills, interchangeable leads, separate artists, typed extensions, historical compatibility and optional-AI replay. |

The preceding pass retains substantial [FFIV/FFVI/Chrono architecture](../research-v0_7/classic-architecture.md), [systemic-world](../research-v0_7/systemic-worlds.md), [modern JRPG](../research-v0_7/modern-jrpg-production.md), [No Man's Sky/frontier](../research-v0_7/frontier-streaming.md), and [camp/rest](../research-v0_7/camp-and-rest.md) analysis. These papers distinguish primary interviews/documentation from reconstructions, mod tools, observable behavior and architectural inference. Proprietary source code was not represented as available where it was not inspected.

## Main design consequences

Preserve original OpenFantasy's content and deterministic philosophy, while building stronger Beta authority, identity, persistence and history contracts. Carry RogueFiction's situated sensing, costs and contextual narrative into real actors and world consequences. The useful union is a coherent game model; merging old runtime ownership schemes would retain their conflicting assumptions.

Keep Godot C# presentation and a portable C# authority as the selected start. Rust offers meaningful ownership and allocation control, and an in-process C ABI does not require IPC. It does not solve causal scheduling, conservation, archive growth or readability. P00–P03 must qualify realistic workloads before bulk dependent implementation.

Arbitrary historical access is the largest unproven architectural obligation. Generated ancient history needs stable, consistent refinement; lived history needs preserved state changes and interpreters. Checkpoints accelerate exact destinations. Finite storage and preparation time remain real costs in both languages.

Use real sparse 3D terrain for floors, subfloors and tunnels; distinguish elevation from planes and timelines. Scene composition cannot be the authority for excavation, support, fluid flow or vehicle clearance. Validate actual geometry and logical advancement across skills, information, resources and time, rather than counting graph loops or balancing enemies alone.

Full-depth NPCs require durable individual state and shared action rules. They do not require all-pairs relationships or per-frame deliberation. Political effects travel through institutions, resources, communication and perception before changing local behavior. Storytelling must make those consequences legible and preserve specific character voices.

Art finish and responsive interaction have separate acceptance bars: Sea of Stars-level pixel craft and Stardew-like everyday animation and graphical response. Native-scale action families, paired choreography, low repetition and real weather/condition cues must be tested together. Detail remains selective; more state should not mean more visual noise.

## Coverage, disagreement and limits

[RESEARCH_COVERAGE.json](RESEARCH_COVERAGE.json) maps every current requirement to 25 topic groups, source dossiers, applications, retained proof families and explicit limits. It includes exact requirement-text hashes so later scope changes cannot silently inherit stale research coverage. A mapping means the topic was addressed and its evidence bounded; it does not mean every design is empirically proven or every conceivable combination was researched.

Reddit was consulted across the expanded dossiers, including disagreement over time pressure, information density, repetitive work, procedural surprise, mod/runtime friction and reference-game ports. These are dated, self-selected reports—not representative surveys or authority for internals. Where only indexed excerpts were retrievable, the dossier says so. Primary developer sources establish technical claims; community reports generate hypotheses for testing the experience.

No controlled model comparison, full native asset pipeline, Beta performance run, cross-platform export or real Beta playtest was conducted during this documentation pass. Official platform support does not certify the selected toolchain pairing. Accepted still images do not certify animation. The full scope exceeds what any single inspected reference demonstrates.

The seven dossiers define 53 named retained research cases: TERR 9, SOC 9, PX 8, SK 9, PLACE 10, LANG 4 and ECO 4. They remain unrun. The [validation contract](../spec-v1_0/VALIDATION.md) retains these, the preceding suites and critic counterexamples, while the [implementation plan](../spec-v1_0/IMPLEMENTATION_PLAN.md) stages their real execution.

After this evidence synthesis, the [pain-point and resolution review](../spec-v1_0/PAIN_POINTS_AND_RESOLUTIONS.md) compares conflicting requirements and concrete alternatives. The [synergy atlas](../spec-v1_0/CROSS_SYSTEM_SYNERGIES.md) and [presentation contracts](../spec-v1_0/PRESENTATION_CONTRACTS.md) carry the resulting integration duties into production.
