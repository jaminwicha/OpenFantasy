# Beta validation, production evidence and coordinated agents

## 1. Evidence levels

Revision 1.1 preserves prior evidence and adds geological, fluid, ecological and acoustic requirements. Independent 1.0 verdicts do not approve these new contracts. This specification has research and document review evidence. It does not have a playable Beta implementation. Keep evidence labels explicit throughout production:

| Level | What it can establish | What it cannot establish |
|---|---|---|
| Source/research review | A cited behavior/interface, architectural inference and applicability limits | Proprietary internals not inspected, target-game correctness or enjoyment |
| Specification critique/tabletop walkthrough | Missing rules, contradictions, likely exploits, incomplete phase coverage | Successful executable behavior, animation quality or real playtest results |
| Pure core test | Deterministic rules, transactions, invariants, replay and recovery on actual modules | Godot rendering/input, packaged platform behavior or fun |
| Engine/integration/export test | Real presentation, networking, assets and target build behavior | Long-term experience or every seed/package combination |
| Actual virtual playtest | An agent operating the build with retained inputs/observations and evidence | Human taste, emotional resonance or user art acceptance |
| Human acceptance | Whether the observed experience/art/control/pace meets its quality bar | Exhaustive technical correctness |

A virtual tabletop critic imagining a caravan encounter must label it specification analysis. When executable software exists, virtual playtesters must actually run it, retain the build/seed/configuration and record what they observed. No agent may infer that an unrun test passed because the implementation looks plausible.

## 2. Completion and traceability

[COVERAGE.json](COVERAGE.json) is the complete requirement-to-phase map. Each requirement has an introduction phase, full-completion phase, accountable role and concrete validation. Introduction does not mean completion. A feature's full-completion evidence must include its retained regression, player-facing use, data/assets and integration, not merely a type declaration.

Each task/evidence record contains requirement IDs, artifact/build revision, package/rules versions, seed/checkpoint, host profile, test inputs, expected and observed outcomes, result, logs/captures, limitations, owner, critic and disposition. Separate failed, blocked, not run and passed. Store durable task status, coverage and decisions in ordinary project files; chat coordinates work but must not be the only memory.

The production catalog enumerates finite shipping definitions and supported generated families. Each entry names required gameplay capabilities, source/migration mapping, body/asset family, narrative/economy role, reachable use, quality depth and tests. Mark family coverage only when a validator and representative adversarial samples justify it. An infinity of combinations is not exhaustively tested by a few screenshots.

Desktop production requires all required non-mobile domains at their declared depth; no dormant graph, empty menu or permanently placeholder asset satisfies completion. Mobile completion follows its explicit phase with its own exports/input/performance. Future MMO-scale discovery/hosting extensions are readiness commitments where specified, not an undeclared first-release MMO implementation.

## 3. Cumulative slices and foundations

Follow P00–P14 in the [implementation plan](IMPLEMENTATION_PLAN.md). Early work lives in the intended core, host, importer, content package, editor and tests. A small retained implementation may use a bounded content set while proving real state ownership. It must not fake a core service that later invalidates the proof.

The first connected playable slice must prove one original-content loop across perception, NPC decision, work/recruitment, travel, on-field Wait combat, consequence and save/restart. It includes a real bounded NPC planner and offscreen job/state reconciliation. Early package/temporal/clock proofs follow before persistence and content formats harden. Production does not begin with a universal engine detached from the game.

Temporary assets can support early logic testing when visibly labelled, but native artist acceptance is required before bulk production. Canned narrative can demonstrate a scene while clearly labelled; it cannot prove autonomous narrative generation. An authored small scenario can exercise the real world model, whereas a hidden script that injects resources or resets actors invalidates that claim.

Every slice exits with a decision: promote the validated modules, revise a named failing contract in place, or block expansion until the failure is resolved. Preserve useful code and fixtures; “no throwaway” does not require retaining a broken approach. Redesign is accompanied by migration/backward-compatibility analysis, not a new competing engine.

## 4. Test families and adversarial cases

Retain the baseline T01–T32 scenarios and specialist MIG, ENV, TIME, STREAM, TRANS and SYS scenarios, with exact source references. They are specifications until executed. New classic/systemic/frontier research adds targeted cases; unify overlapping fixtures instead of multiplying redundant test harnesses.

| Family | Mandatory checks |
|---|---|
| Determinism and persistence | Full canonical state equality for repeated ordered input; RNG domains; asynchronous completion order; checked numeric edge cases; duplicate command; crash mid-transaction; fresh-process restore; schema/package migration |
| Player/NPC parity | Same legal action from equivalent state, same costs/effects; learned capabilities; controller handoff; inventory/relationships; distant work and observation; nonplayer initiative and independent outcomes |
| Knowledge and authority | Unseen covert alliances/route data absent from projections; evidence delays and false beliefs; familiar senses scoped; narrative and optional AI cannot invent authoritative facts |
| Encounter clocks | Long decision hold, outsider supplies/attacks, coupled fire/falling object, merging, flight/re-entry, disconnected controller, vessel motion, world deadline and personal commitment behavior |
| Frontier | Cold generation, shared edges, far/negative coordinates, four separated cameras, fastest travel, abrupt landing, tunneling, version upgrades, storage pressure and persisted mutations |
| Assemblies and companions | Fit/replace/salvage, real cargo containment, damaged propulsion, boarding/reconnect, towing/load/anatomy, feeding/care delegation, familiar recall/dismiss and no duplication |
| Rules, classes and research | Imported abilities, specialization/class combinations, stacking, resistance, lingering effects, cost conservation, researched spell definitions, knowledge transfer and invalid compositions |
| Production and ecology | Reserved lots/labor/station, interruptions, substitutions/byproducts, care/food/weather, distant aggregation, resource cycles, dependent jobs and impossible inputs |
| Rest and camps | Approximately 40-minute full-cycle mapping as an initial target, paused-time exclusion, sleep/short-respite eligibility, partial interruption, no reset exploit, consented acceleration, actual watches/care/work and campsite/caravan/ship continuity; REST-01–06 |
| History, society and narrative | Lineage/deity provenance, exceptional individual, institutions and supply, secret evidence, NPC-resolved crisis, no-crisis seed, multiple interacting arcs, deck replay/exhaustion and truthful changed objectives |
| Temporal/planar | Arbitrary requested generated and lived historical instants, stable refinement under reordered queries, pinned head/commit cursor, lossless archive/replay, checkpoint acceleration without destination substitution, branch isolation, traveler/item custody, unique provenance, costs, permissions and crash recovery; dream tether/body interruption and no symbolic cargo export |
| Content and presentation | Every legacy mapping, supported generated family, complete native body/attachment/action intersections, variant repetition, scene interruption, accessibility and readable weather/magic/combat |
| Production operations | Installation/update/uninstall, save upgrade/backup, packaging/signing, controlled release/rollback, support diagnostics, offline operation, privacy/permissions for optional providers and mobile follow-through |

The expanded scope adds the following required families. All are **not run in Beta** until retained executable evidence exists.

| Family | Mandatory expanded checks |
|---|---|
| Mutable 3D terrain and materials | Stacked floors/subfloors; excavation under occupied structures; hardness/tool/material distinctions; support and fluid propagation across chunks; concurrent edits; restart; conserved ore/alloy/refining chains; tunneling ship interaction |
| Lifecycles and institutions | Birth/maturation, marriage/household/custody, inheritance/death/succession, personal versus institutional mandates, distant progression and exposure-based biology across held encounters |
| Motives, politics and city life | News/evidence → belief → feasible action → work/dialogue/performance; sparse relationships without lost depth; actual merchant arrivals/stock; district use and delegation; candidate 20,000-resident workload with meaningful work and bounded queue age |
| Progression, places and mysteries | Realized geometry plus logical capability/resource/knowledge/event routes; constructive supported-start witnesses; composite difficulty; altered world/recovery; multiple entries/loops/vertical routes; clues and hypotheses; truthful unresolved cases and branch-specific evidence |
| Field skills and crafted powers | Every named discipline has a real action, learning route, quest/puzzle or production use and native performance; alternate/cooperative solutions; recursion/resource cycles; input protection; accessible timing; boundary between real-time puzzles and Wait |
| Anatomy and restoration | Eye/limb/sense/grip changes, equipment custody and adapted actions, native performance, restart/branch transfer and supported restoration; cumulative lasting-injury incidence and accessible recovery over campaign-length exposure |
| Integrated presentation | Separate Sea of Stars-level finish and Stardew-like interaction/motion acceptance; all domain rows in PRESENTATION_CONTRACTS; native body/attachment/action/condition intersections, five place-based scenes, meaningful variation, quiet visual hierarchy and no hidden-state gestures |
| Combined causality | SYN-01–22 in CROSS_SYSTEM_SYNERGIES, including political trickle-down, market/mining/construction chains, dreams/evidence/history, careers/care, vehicles/hazards and progression-sensitive UI |

Retain specialist cases from the expanded research: TERR-01–09 in [terrain/runtime](../research-v0_8/terrain-runtime-and-worlds.md), SOC-01–09 in [society/lifecycles](../research-v0_8/society-lifecycles-and-emergent-stories.md), PX-01–08 in [presentation/experience](../research-v0_8/presentation-production-and-player-experience.md), SK-01–09 in [skills/crafting/dreams](../research-v0_8/skills-crafting-magic-and-dreams.md), and PLACE-01–10 in [places/puzzles/progression](../research-v0_8/places-puzzles-and-progression.md). Preserve the original research cases alongside these; share real fixtures where concerns overlap. Retain LANG-01–04 from the [language reassessment](../research-v0_8/csharp-rust-core-reassessment.md), ECO-01–04 from [ecosystem/packages/production](../research-v0_8/ecosystem-packages-and-production.md), and the retained VT-01–29 counterexamples in the [preserved 1.0 systemic critique](../spec-v1_0/CRITIC_REVIEW.md), alongside integration-critic cases. Combine shared fixtures while preserving each distinct assertion. This register specifies obligations; actual pass/fail evidence is recorded per build in the production ledger.

Use failure injection at state boundaries, not only happy paths. Retain reproducible minimized failures. Property-based and seed-sweep checks are valuable for conservation, graph validity and world consistency, but must be paired with authored semantic cases that can expose a wrong yet deterministic result.

## 4a. Geological, ecological and acoustic revision

All 31 added requirements RQ-151–181 remain separate traceable obligations in the current register. Canonical project naming is Beta; references in unchanged historical source rows to original OpenFantasy identify Alpha OpenFantasy. Validate the [geological contract](GEOLOGICAL_WORLD_DESIGN.md), [fluid/ecology contract](ECOLOGY_AND_FLUIDS.md) and [audio contract](AUDIO_DIRECTION_AND_RUNTIME.md) together with existing actors, custody, clocks, source claims and presentation.

| Revision case | Required result and retained phase |
|---|---|
| REV-01 Feature and material history | P01/P03/P10: same crust, river, city and mineral source across motion, erosion, split/merge, burial, extraction and reconstructed instants; fixed-coordinate versus feature-address queries are distinct |
| REV-02 Live flood intervention | P03: close a sluice or breach a dam after a flood plan begins; uncommitted fronts revalidate current topology/budgets/occupancy, reservations release/transfer once, and predicted casualties are not forced |
| REV-03 Held inflow capacity | P03: hold a cellar while rain/upstream flow continues; water stays in finite physical storage, legal backpressure/overflow or wider hold occurs, no invisible infinite buffer/catch-up damage appears |
| REV-04 Macro dependency termination | P01/P03: reject unending downstream dependency and unintended A→B→A recursion; retain legitimate finite reservoir/tidal cycles and declared boundary budgets; reverse neighboring request order |
| REV-05 Reworked strata | P03/P10: mine, volcanic infill, re-excavate, mix sediment and revisit every intermediate state; source shares/export rights remain conserved and provenance fragmentation is measured |
| REV-06 Coupled ecological change | P02/P03/P04/P10: seasonal crop, herbivore, predator and dispersing plant cross weather/drought/flood/taint transitions; growth/harvest and food compete for one state, actual creatures can ignore/flee/hunt each other |
| REV-07 Biome transition | P04/P10/P12: sustained conditions permit or block seed establishment, succession, forest loss/desertification and recovery; labels cannot repaint mature cover or reroll species; minor inherited traits stay within supported envelopes |
| REV-08 Historical environment intervention | P03/P10: change a dam or magical source in a child; recompute dependent water, vegetation, migration and settlement events while preserving parent history and exact requested instants |
| REV-09 Inhabited submerged/underdark route | P02/P06/P07/P10: a real city's dry/flooded/submerged states and evolving cave network preserve inhabitants, claims, resources, safe traversal and supported breath/heat/pressure/Wait behavior |
| REV-10 First-class atmosphere | P02/P03/P12: native town→wilderness→cave/underwater→encounter→aftermath, actual material/action and bird/creature/weather cues, quiet versus epic scoring, fades and changing enclosure; acoustic mappings or intentional silence accounted for |
| REV-11 Audio recovery and fatigue | P03/P12/P14: duplicate deliveries versus legitimate temporal revisits, late joins, long Wait, competing transitions, muted layers and mobile interruption; no stale impact burst, secret leak, forced loud ordinary combat or repetitive long-session overload |
| REV-12 Terminology and archives | P00/P13: current successor labels say Beta, predecessor labels identify Alpha OpenFantasy, and preserved 1.0 files/archives match original hashes |
| REV-13 Gradual introduction and orchestration | P02/P03/P12: real teaching opportunities survive season/knowledge differences, alternate solutions, changed mentors and mixed-experience co-op; human sessions establish low early overwhelm and coherent endgame coordination; ignored/skipped opportunities reserve nothing, and accepted tasks release ordinary reservations on expiry/cancel |
| REV-14 Conditional local reuse | P00/P01–P11: selected source files match recorded hashes, candidate contracts/tests are understood and ported fixtures prove Beta-specific ordering/ownership/behavior before integration; rejected architectures remain rejected |

Retain GEOR-01–06 from [geology](../research-v0_9/geological-world-generation.md), SUB-01–06 from [submerged/underground/ice](../research-v0_9/submerged-subterranean-and-glacial-worlds.md), ECOG-01–06 from [ecology/fluids](../research-v0_9/ecology-fluids-and-living-biomes.md), and AUD-01–04 from [adaptive audio](../research-v0_9/adaptive-music-and-world-sound.md). Also retain LEARN-01–04 from [procedural onboarding](../research-v0_9/procedural-onboarding-and-mechanic-pacing.md). All 26 new web-research cases and REV-01–14 are **defined, not run**. Also retain LIFE-V01–04 and LOCAL-01–06 from the two [local code audits](LOCAL_REUSE_DECISIONS.md), ten additional unrun Beta adaptation cases. One LifeFormzFable source continuation test passed in isolation; that separate source check does not qualify Beta. Combine fixtures while preserving distinct assertions; exact mappings live in the production coverage register.

Geography/ecology qualification includes finite active fronts and their age, macros/catchments/reservoirs, cohort/individual counts, provenance fragmentation, mixed-liquid/phase steps, historical preparation and four separated players. Sound qualification includes voice/mix limits, streaming/seek/loop continuity, enclosure/medium changes, accessible feedback and human listening fatigue. Smaller early fixtures must be requalified with full content and chronology; a passed document check is no claim of solver capacity or immersion.

## 4b. Cultural life and generative coherence

CULT-01–08 in the [cultural research](../research-v0_9/language-aging-and-afterlife.md), GEN-01–05 in [generation validation](GENERATION_VALIDATION.md) and RED-01–04 in the [Reddit synthesis](../research-v0_9/reddit-procedural-world-synergies.md) add 17 distinct unrun assertions. P01–P03 retain small real knowledge, age, death/form, composition and recovery counterexamples; P07–P10 expand breadth; P12–P14 require native/human/platform evidence. Requirements RQ-173–181 have individual coverage rows. Do not confuse natural slow aging with a historical destination date, revoke all possible future spirit capabilities with living-body control, translate hidden truths, or let optional pruning remove mandatory Alpha content. Validate directed cultural attitudes and actual architectural/craft evolution through the same actor/material/history contracts.

| Cultural/body case | Required unrun result |
|---|---|
| CULT-X01 | P02→P09: trace distinct weapon/armor/tool designs and magical/skill practices through learning, actual production, body fit, use, repair and historical lineage; native clothing and kept objects reflect means/taste/history, with valid custody and restrained variation |
| BODY-X01 | P01→P11: enumerate the additive eleven named species, earlier RQ-019 beings, full Alpha creature catalog and shipping subspecies; compare supported small, giant, serpentine, tailed/winged and other bodies through infrastructure, movement, equipment, care, Wait/history, player/NPC control and accepted native actions; reject unsupported scaled-human shortcuts |
| CULT-X02 | P02→P09: compare fortified sites and workshops emphasizing different martial/mechanical/alchemical/magical capacities, actual supplies/knowledge/operators and counterplay; changing institutions and supply alters capability without instant universal technology unlock or free magical defense |

## 5. Performance and frontier qualification

The [technical design](TECHNICAL_DESIGN.md) owns initial workload profiles and deterministic work limits. Qualification numbers are measured targets, not proof of capacity or permission to delete existing NPC depth. Profile representative worlds with real assets, histories, mutations, effects and four separated players.

Record frame-time and simulation-step distributions, allocation/collection, core queues and age of scheduled work, generation/readiness latency, memory, network volume, save growth, load/recovery time and active/distant actor counts. Use cold and warm runs. A mean frame rate cannot hide recurrent stalls, and a tick budget cannot hide years of overdue NPC decisions.

Test at a normal profile, an expected upper workload and deliberate overload. Define safe backpressure or degradation of presentation while preserving canonical state and causal order. Do not change which game rules run based on machine speed. An overloaded host may progress more slowly or refuse an unsupported new allocation clearly; it cannot erase a town or simulate only whichever actions finish first.

World variation requires separate measurements: meaningful motif reuse, route/solution diversity, shared versus distinct histories, event eligibility failures, long discovery gaps, resource access and major-arc interference. Examine unfamiliar seeds and long journeys. Repeatable generation statistics do not replace human judgments of cohesion, surprise and boredom.

## 6. Native art and content acceptance

Preserve actual accepted reference files and scope. The current still-image direction is accepted for exploration; native pixel scale and motion require their own gate. Use the [GDD family matrix](GDD.md#17-art-animation-audio-and-interface-contract) and production catalog to define required intersections before multiplying assets.

Dedicated artist agents can independently generate/calibrate assets, but each work order supplies canonical identity, native dimensions/anchors, body/attachment family, supported actions, condition/weather variants, palette/composition constraints and export requirements. The engine builder supplies a stable preview harness and reports actual import errors. Artist tasks must not modify core state contracts opportunistically to make a picture fit.

Review native and integer-scaled images, silhouette/facing, equipment alignment, loops, transitions, personality, paired interactions, terrain seams, weather/light conditions and reduced repetition across scenes. The acceptance package retains editable/source assets, import-ready exports, provenance, preview configuration and captures. A generated montage or spritesheet does not prove consistent frames until imported and played.

Maintain restrained detail, material history and clear action/condition cues. Variation can use family-consistent shapes, construction, pose and placement, not arbitrary clutter or palette noise. Gameplay-critical state must remain legible under night/storm/magic effects. Audio and UI receive equivalent integration, accessibility and long-session review.

Apply the [presentation contracts](PRESENTATION_CONTRACTS.md) and [synergy atlas](CROSS_SYSTEM_SYNERGIES.md) in actual native scenes. Art finish and responsive interaction are independent gates: good still images cannot certify animation, action availability or gameplay feedback.

Human acceptance decides visual direction, animation quality and felt experience. Virtual agents can spot obvious inconsistencies, repetition, clipping and missing feedback, but cannot accept the user's art style on their behalf.

## 7. Central orchestration and task roles

The central collaboration task owns the current requirement baseline, dependency order, cross-domain decisions, integration and final acceptance reporting. Subagents receive bounded task packets: purpose, requirements, owned files/modules, dependencies, permitted tools, output/evidence expectations and stopping criteria. They are explicitly told that others share the workspace and that they must not revert unrelated edits.

| Role | Responsibility and required evidence |
|---|---|
| Engine builders | Core/host/presentation modules within assigned ownership; tests, diagnostics and contract changes with consequences |
| Content designers/creators | Definitions, processes, adventurelets, histories and migration records; validators and actual reachable uses |
| Dedicated artists/animators/audio creators | Calibrated editable assets and native previews within accepted style; avoid engine-authority changes |
| Research specialists | Primary-source claims, evidence limits, applicable decisions and falsifiable experiments |
| Virtual playtesters | Operate a real build with scenario/seed/build identity, observation logs and captures; distinguish observation from interpretation |
| Independent critics | Challenge state contracts, scope, failure cases, gameplay coherence and evidence; do not approve their own authored work |
| Central integrator | Resolve cross-task conflicts, update durable status, run appropriate integration checks and request human art/fun acceptance on concrete results |

Codex and Claude can fill any full task-owner role. Antigravity defaults to mundane maintenance. Role assignment follows context and evidence; there is no compulsory vendor handoff. Current tool concurrency is finite, so tasks run in bounded waves. A planned role is not a claim that its tool or a physical target device is available.

Research/implementation and criticism can run in parallel where useful, but a final critic checks the actual integrated revision. Avoid having many agents edit the same schema. Let one owner maintain shared types; dependent workers propose changes through the central coordinator. Version the task contract when a cross-domain change is accepted.

## 8. Critic protocol and issue disposition

Use at least two independently assigned critical perspectives for consequential milestones: architecture/integrity and product/scope/experience. Add art/performance/security-specific review when the change warrants it. During this specification pass, research owners write distinct artifacts and the systemic critic evaluates the combined work; an additional integration critic reviews documents it did not author.

Each finding records severity, affected file/section/requirement, concrete counterexample, consequence, proposed correction and verification. Distinguish blocking contradictions from tunable values and intentional declared tradeoffs. Resolve by fix, evidenced rejection, or explicit gated deferral. A deferral needs an owner, dependency and stopping gate; it is not a hidden permission to omit scope.

The author supplies the correction; a critic or central reviewer verifies the final text/code and updates the finding. Preserve unresolved findings in the release gate. A document labelled “final” cannot conceal a contradiction in its appendix. Conversely, runtime performance or fun that requires an executable build is an evidence gate, not a reason to pretend the design can prove it now.

## 9. Ecosystem use and persistence

Use skills and tools when they improve a specific outcome: coherent requirement mapping, native asset creation, source lookup, tests, diagnostics, export or independent review. Keep capability checks local to the active host. Documented MCP coverage does not prove a tool is installed, connected, affordable or appropriate.

Prefer readable instructions and narrow helpers over a rigid orchestration framework that forces unnecessary roles, approval loops or competing engines. Persist reusable owner guidance in the maintained ecosystem and installed skills with backups and provenance; keep game-specific art references, rules, seeds and content in Beta. Measure tool/pipeline utility through reproducible output and creator throughput, not the quantity of integrations.

## 10. Production release gate

Desktop release requires complete non-mobile requirement coverage, full legacy migration gates, a versioned shipping catalog, passing core/integration/export/recovery/security checks appropriate to the game, verified supported-device profiles, accepted art/motion/audio/interface, and real human gameplay evidence across adventure and ordinary careers. World-frontier longevity, NPC continuity, independent crisis outcomes and four-player behavior are part of that evidence.

The release candidate includes package manifests, save compatibility/migration policy, backups, diagnostics, install/update/support instructions, known issues with dispositions and a controlled rollback path. Publishing/signing/paid-service actions follow explicit authorization when implementation reaches that gate. This specification task creates the reviewable plan; it does not deploy a game.

Mobile later completes its own input/layout, export, thermal/performance, persistence, interruption and device matrix using retained modules. Production support tracks regressions and content/source deltas without silently discarding older saves, identities or required content. Schedule commitments follow measured implementation and approved-content throughput.

## Archive carry-forward acceptance

ARC-01 (P02→P03, P12–P14 regression): two clients use retained Alpha zone chat and scoped system notices across location changes, reconnect and retries; safe text, correct recipients and bounded history hold, with no automatic NPC knowledge or secret event disclosure. ARC-02 (P03 policy schema→P07 actual operation, P12–P14 regression): filters/min-max stock, priorities/shifts/district roles and stewards/foremen/quartermasters dispatch real jobs under supply/capacity/agency constraints; interruption, protected goods and absence preserve work and readable exceptions. The additive roster assertion is part of BODY-X01. These cases are unrun; the [archive continuity audit](ARCHIVE_CONTINUITY_AUDIT.md) records source-to-current mappings and remaining proof limits.
