# Beta — specification baseline 1.1

This is the current consolidated product and implementation specification, dated 2026-09-08. It preserves **181 requirements** and their earlier revisions, with **15 phases (P00–P14)** through desktop production and later mobile support. “1.1” identifies this specification edition, not a completed game release. The earlier [editions and snapshots](../archive/README.md) remain preserved.

## Recommendation

Build the successor in Beta with a new portable deterministic C# authority and Godot C# presentation. Reuse Alpha OpenFantasy algorithms, fixtures and content where their behavior survives the stronger contracts. Fully import and incorporate the original content; keep the original game as a preserved comparison and migration source. Extend RogueFiction's contextual quest, character and narrative ideas through actual world actors, knowledge, resources and history. The resulting game can encompass the concepts of both projects without inheriting every implementation dependency.

Keep the core in-process and independent of Godot. The [expanded C# versus Rust reassessment](../research-v0_8/csharp-rust-core-reassessment.md) retains C# with an explicit P00–P03 workload gate before broad P04 production. Whole Rust through a coarse in-process C ABI remains a serious evidence-triggered alternative; it does not require IPC. The classic JRPG homage informs data-driven effects, event choreography, coherent fields and expressive presentation. Modern deterministic ownership, durability and multiplayer contracts govern implementation.

The GameDev ecosystem is useful as a selective collection of maintained instructions, asset tools and verification workflows. Use the current task owner and ordinary implementation tools by default; load a specialized skill when it improves the work. Codex and Claude remain interchangeable leads, artists have separate ownership, Antigravity handles bounded maintenance, and critics inspect work they did not author. No ecosystem integration or model benefit is established merely by documentation or installation.

The delivered [1.0 specification](../spec-v1_0/README.md) remains byte-preserved and [archived](../archive/specification-v1.0-2026-09-08/manifest.json). This edition adds explicit geological history, liquids/ice/ecology, adaptive sound, gradual teaching, cultural life/equipment/technology, slow aging/afterlife, generation compatibility and additional local-code reuse decisions.

## Read and execute

| Artifact | Purpose |
|---|---|
| [Astra implementation handoff](IMPLEMENTATION_HANDOFF.md) | Reading order, full-scope readiness checklist, first retained work package and open executable gates |
| [GDD](GDD.md) | Complete intended player experience and systemic scope |
| [Technical design](TECHNICAL_DESIGN.md) | State ownership, arithmetic, determinism, persistence, networking, time, streaming and content contracts |
| [Decisions and qualification gates](DECISIONS.md) | 63 selected decisions and nine explicit evidence gates |
| [Full implementation plan](IMPLEMENTATION_PLAN.md) | Retained foundations, cumulative slices, complete domain expansion and production |
| [Requirements](REQUIREMENTS.json) / [coverage](COVERAGE.json) | All 181 requirements, prior records, introduction/completion phases, owners and concrete validation |
| [Validation workflow](VALIDATION.md) | Tests, platform and performance qualification, central agents, actual-build playtests and human acceptance |
| [Systemic critic](CRITIC_REVIEW.md) / [integration critic](INTEGRATION_CRITIC.md) | Independent findings, corrections and remaining evidence limits |
| [Expanded research synthesis](../research-v0_9/README.md) | Nine further dossiers, 35-topic coverage, source-level local audits and dated Reddit evidence |
| [Cross-system synergies](CROSS_SYSTEM_SYNERGIES.md) / [presentation contracts](PRESENTATION_CONTRACTS.md) | Twenty-two causal scenarios and native state-to-feedback-to-action evidence across every domain |
| [Pain points and resolutions](PAIN_POINTS_AND_RESOLUTIONS.md) | Concrete conflicts, alternatives, recommended defaults and required proofs |
| [Archive continuity audit](ARCHIVE_CONTINUITY_AUDIT.md) | Semantic source-to-current mapping, restored details and intentionally superseded proposals |
| [Specification integrity](SPEC_VALIDATION.json) / [verification scripts](../tools/README.md) | Reproducible mechanical scope, links, preservation and instruction checks; no runtime certification |

## Focused contracts

The GDD is the consolidated player-experience authority; these documents specify the difficult boundaries:

- [Evolving geography](GEOLOGICAL_WORLD_DESIGN.md) and [ecology/fluids](ECOLOGY_AND_FLUIDS.md): tectonics, erosion, shifting rivers/coasts, ice, underground/underwater, food webs and exact history.
- [Language, culture and life/death](LANGUAGE_AND_AFTERLIFE.md): light comprehension, personal attitudes, distinctive architecture/crafts/technology/equipment, broad species, slow aging and accountable afterlife.
- [Procedural composition validation](GENERATION_VALIDATION.md): finite repairs/pruning/rejection before commitment, no loss of mandatory content or realized history.
- [Onboarding and orchestration](PLAYER_EXPERIENCE_AND_ONBOARDING.md), [adaptive sound](AUDIO_DIRECTION_AND_RUNTIME.md) and [local reuse decisions](LOCAL_REUSE_DECISIONS.md).

## Selected experience

Windows, macOS and Linux launch first, supporting one to four players per hosted world. Mobile follows as a supported phase using the same core. NPCs retain player-equivalent applicable capabilities and individual state, with different control and simulation frequency. Almost-infinite-feeling frontier generation, persistent mutations, authored adventurelets, contextual encounter decks and multiple contingent grand narratives share the same causal world.

Bounded Wait encounters use on-field transitions inspired by Chrono Trigger and Sea of Stars. Independently exploring co-op explicitly accepts local tactical stasis; deadlines, environment, transport and outside admission have defined policies. A full day/night cycle initially targets about **40 minutes of advancing exploration time**. Sleep and camps provide recovery, care, preparation, relationships and contextual events. Consented time advancement, interrupted rest and a separated player whose companions decline acceleration must be tested in real builds before pacing is accepted.

## Readiness and limits

Independent integrated review and dispositions are recorded in the two critique documents. The selected contracts guide P00–P03 retained foundations and proofs; broad expansion remains conditional on their executable gates. It is not evidence that the game, import, native animation or multiplayer simulation already works. Minimum hardware, exact toolchain export compatibility, measured capacity, native art/motion, content throughput and human enjoyment remain named qualification gates. Accepted concept scenes are preserved; production assets still require native and animated acceptance. Sea of Stars-level pixel finish and Stardew-like responsive animation/interactivity are separate acceptance bars. Arbitrary historical access, stacked excavation, deep city life and composite temporal progression are required scope, not optional appendices.

Future revisions must archive this edition and its companion registers before replacing its authority. Preserve failing seeds, tests and decision history as implementation supplies new evidence.
