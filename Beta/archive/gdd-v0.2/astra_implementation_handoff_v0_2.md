# Astra implementation handoff — Persistent Seed JRPG Platform

Version 0.2 · 8 September 2026

## Read first

Use `persistent_seed_jrpg_gdd_v0_2.md` as the machine-friendly master and the DOCX as its reading edition. Read Appendix A against `persistent_seed_jrpg_requirements_v0_2.json`: 79 design requirements, each mapped to controlling sections and acceptance tests. Section 40 defines 32 tests; Section 41 defines ten priority architecture decisions. All implementation evidence is pending.

## Assignment

Convert the GDD into an implementation GDD/technical design and dependency-ordered development plan, then begin with an executable risk-first slice. Preserve the owner's full vision: original classic-JRPG presentation; Dwarf Fortress-scale adventure and settlement depth; Terraria-class crafting/exploration; procedural eon/civilization history; deep NPC family/clan/political/class motives; integrated creation and earned introspection; hosted solo/co-op worlds; first-class server-configurable packages; optional AI; and time/portal/alternate-history gameplay.

The commercial adventure target is 60–80 hours including ordinary side exploration, with bonus/alternate content and an open-ended persistent sandbox. Do not mistake that horizon for a requirement to implement every system at full resolution in the first prototype.

## Decisions before large-scale production

Resolve action/combat and co-op clocks; topology and adaptive historical resolution; branch causality and item/traveler identity; runtime/storage/determinism; package sandbox and lifecycle; NPC autonomy and labor; narrative/procedural validation; AI recorded decisions and fallback; art/animation scope; and supported hosting/distribution policy. Record decisions with alternatives, rationale, tests and requirements affected. Proposed numeric targets and the branch-on-intervention default are not owner-approved mandates.

## Required outputs

Produce system boundaries, domain schemas, command/effect/event contracts, world/branch identity, package API/manifests, migration and recovery design, static generation/graph validation, performance reference profiles, narrative/art/music inventories, test fixtures, repository layout, dependency-ordered milestones and an estimate with staffing assumptions. Each major requirement must receive an implementation owner/boundary, persistence and networking treatment, extension surface, delivery stage and acceptance evidence.

## First executable proof

Demonstrate a small original region containing a town, wilderness and dungeon. Include a material/process craft, a recruited worker with a real obligation, a persistent construction/work order, a world consequence and an earned calculation trace. Exercise two real clients, a sanctioned extension, a historical checkpoint/branch and an AI-off fallback. Test transaction retry/recovery and temporal identity. A beautiful isolated combat demo or a headless universal engine alone does not meet this proof.

## Constraints not to lose

The prototype is not the complete release scope. Do not silently drop time travel, fortress management, social affiliations or deep creation. Do not transplant reference-game code or assets. Do not treat an LLM evaluator as proof of balance, solvability or determinism. Seed-only replay does not cover unrecorded external decisions. A kernel/package boundary does not remove engine/security work. Adaptive simulation requires conservation and identity reconciliation. Future MMO deployment is not guaranteed to be a minor scaling change.

## Review status

The GDD has been revised against the full visible discussion and prior document; selected reference-game descriptions were checked against official sources. No repository/source audit, playable prototype, benchmark, security assessment or legal clearance has been performed in this document pass. Those are next-phase evidence, not missing user decisions.
