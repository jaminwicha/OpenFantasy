# Persistent frontier generation and streaming

## Research conclusion

An almost-infinite-feeling frontier is compatible with a portable C# core and a detailed pixel-art client. It requires a large stable address space, generation by need, durable changes, controlled simulation residency and a content system that keeps discoveries meaningful. No Man's Sky informs continuity and procedural production; Dwarf Fortress informs the causal variation of places, people and history. Neither precedent proves Beta's full combination of persistent individual NPCs, expressive artwork and independent Wait encounters.

Hello Games' public 2017 session describes continuous generation from terrain through surface preparation, population and simulation. Its session description and official video were located, but a full readable transcript or slide deck was not available in this pass. Exact queue algorithms, chunk dimensions and resource bounds must therefore not be attributed to the talk. The procedural-to-authored pipeline lesson is supported at the session level; Beta's detailed contracts below are original recommendations.[^nms-talk]

There is stronger direct technical evidence in the studio's Worlds Part I notes: terrain meshing was rewritten using dual marching cubes; environmental object rendering moved to the GPU; metadata, streaming and level-of-detail systems received optimizations. The same notes explicitly preserve underlying terrain shapes and existing base positions. This illustrates that better generation/presentation and continuity of player investment are distinct engineering responsibilities. It does not require Beta to use volumetric meshing or GPU simulation.[^worlds]

Origins supplies an earlier versioning example: generation changes preserved existing planets' fundamental type and hazard while previous creature discoveries moved to an extinct category. The transferable lesson is to define what a generation upgrade preserves, transforms and records. Beta's stronger no-loss and individual-history requirements demand explicit migration rather than silently treating a changed generator as the same world.[^origins]

## 1. Frontier contract

The playable world has practically inexhaustible addressable continuation, not an infinite amount of precomputed or concurrently active state. Use stable world/branch/layer addresses and integer spatial coordinates. Rendering uses a nearby origin; floating-point camera precision cannot change canonical positions, collision or generation seeds at great distances.

Generate broad constraints before local decoration: landforms, drainage and routes, climatic/ecological regions, historical occupation, settlement catchments, resources and travel connections. A coordinate-keyed hierarchy with shared edge constraints avoids neighboring chunks independently inventing incompatible river outlets, roads or ruins. Required Aldermere places and progression relationships belong to a guaranteed content realization, then blend into the larger frontier.

Unknown terrain can remain latent. Materializing it applies the pinned generator and world-history profile plus already committed regional constraints. A discovered place, an instantiated individual or a consequential fact becomes durable state. The camera does not decide that a known person has a different family or profession. Remote trade, migration and armies may materialize relevant destinations before a player sees them; realization must be transactional and independent of which client requests it first.

Uninstantiated population aggregates represent populations, not millions of fully invented people whose biographies will later be contradicted. Once a person is instantiated through birth, history, work, travel or another consequential event, their stable identity and recoverable individual state persist. This distinction must be inspectable in simulation diagnostics. It bounds realization work without weakening the required depth of any actual NPC.

The world stores a generated baseline plus durable mutations and referenced history. Never evict a player-built wall, excavated passage, relationship or named discovery to make a cache fit. Presentation caches can be rebuilt. Canonical storage can compact events into validated checkpoints while retaining the required facts and provenance. If storage is exhausted, safely stop further mutations with a recoverable save-state error; silently regenerating modified terrain is not an acceptable fallback.

## 2. Pipeline and demand ownership

Use demand records for cameras, navigation, encounters, vehicle routes, significant effects and remote simulation tasks. Each states necessary detail and urgency. Several consumers can hold the same area without creating duplicate generators or copies of its people.

Pipeline stages are: resolve address/version → prepare regional constraints → build deterministic local state → reconcile durable mutations → establish navigation/collision → prepare render resources → integrate through the Godot adapter. Pure preparation can run in background jobs. Canonical results validate their expected version and commit in deterministic order; asynchronous completion cannot decide history or ownership.

Collision and gameplay-ready data precede decorative polish. A complete background image is not proof that a tunneling ship may move. Conversely, distant scenery can render at reduced detail without simulating every visible individual at combat cadence. Readiness requirements belong to each consumer, not a single ambiguous “loaded” boolean.

Keep five distinctions explicit: spatial chunk, narrative location, authoritative transaction scope, encounter timing scope and presentation scene. A ship and its battle can span chunks. A city can contain many narrative locations. A weather front can cross all of them. These boundaries may overlap but must not be treated as synonyms.

## 3. Predictive loading and travel modes

A practical sizing relation is:

`required look-ahead >= visible/reachable lead distance + maximum supported speed × high-percentile preparation latency + turn/network margin`

Measure latency on new terrain with cold caches and real asset integration, not an empty already generated field. Measure memory as the union of consumer demands across up to four separated players, plus pinned encounters, attachments and queued preparations. Cell dimensions and queue budgets are tuning parameters in a versioned runtime profile; changing a render-cell size must not change canonical geographic identity.

| Travel | Additional demand |
|---|---|
| Walking/riding | Local branches in paths, ledges, water access, visibility and believable discovery density |
| Boat/canoe/ship | Shorelines, shallow/deep water, landings, decks, cargo/crew and route changes |
| Carriage/chariot/tank | Width, turning, slopes, obstacles, towing/mount state and structural loads |
| Sandship | Substrate, traversability, slope, propulsion and shifting conditions under declared world rules |
| Balloon/airship/flying mount | Larger visible horizon, wind/lift/load, abrupt turn and landing-detail requests |
| Tunneling ship | Geological volume, excavation cost, spoil, support/clearance, newly opened passages and nearby structures |
| Portal/temporal transfer | Explicit destination preparation, authority/custody validation and atomic transfer; no promise that arbitrary remote arrival is latency-free |

Vehicle passengers and attached props retain local coordinates, identity and custody under a stable parent. Pin necessary content while it is in use. Speed is limited by declared vehicle capability, not opportunistically increased because a warm cache happens to load quickly.

Unexpected readiness failure preserves position and authority and provides brief synchronization feedback. It must not expose a void, teleport to another route, spawn a fight to hide loading or reroll the next region. Sustained failures at supported speeds fail the release gate. Honest fallback is protection against exceptional conditions, not the normal travel experience.

## 4. Variability that survives repeated play

Large coordinate ranges and many random seeds are insufficient. Variation should affect decisions: climate changes materials and travel; historical occupation changes ruins and claims; ecology changes resources and dangers; institutions change access and obligations; craft traditions change available solutions; individual motives change how similar situations develop. Local motifs should form recognizable cultures and environments instead of randomly recombining every style in each screen.

Use a mixture of systemic happenings, contextual encounter draws, authored adventurelets and longer arc fragments. Each candidate declares location/ecology/history/actor predicates, required resources, knowledge exposure, encounter footprint, possible outcomes and repeat rules. Eligibility uses real state. Selection may be random, but its result becomes a persisted instance with accountable participants and consequences.

An encounter deck is a selection mechanism, not an authority that fabricates resources. Finite decks or weighted pools retain draw history, cooldowns and instance IDs. Sampling order belongs to a named deterministic RNG stream. Retry or unload cannot redraw a more favorable result. Empty eligible pools produce ordinary travel or another valid activity rather than inventing an incompatible ambush.

Authored adventurelets can provide strong pacing and voice while binding to varied but compatible places and people. A caravan dispute, missing familiar or ruined workshop should change because of its history, resources, relationships and participants—not only swapped names. Surprise can still be genuine: not every eligible event needs to be announced in advance. On-field staging and legal encounter admission remain required when combat starts.

Multiple grand narrative potentials can coexist: a claimant's ascent, an ecological collapse, rediscovery of a lost craft, a divine schism, a migration, a trade hegemony or the rise of a dark lord. These are proposed narrative families to author, not claims that every seed includes each. Seeded history establishes possibilities; live events choose their course. Shared resources and participants couple arcs. Resolution, failed preconditions or changed allegiance can end or redirect a thread; no director silently restores its intended villain.

The [systemic-worlds research](systemic-worlds.md) supports this separation of causality, history, knowledge and narrative selection. A narrative scheduler may prioritize how an eligible development is presented and avoid exhausting the player, but delaying presentation cannot retroactively delay a world event that already happened.

## 5. Retained acceptance experiments

These are test specifications, not completed runtime results.

| ID | Experiment | Required evidence |
|---|---|---|
| FRONT-01 | Generate the same region through different approach orders, concurrency schedules and negative/far coordinates | Identical canonical baseline, shared edges, stable named identities and required landmark placement |
| FRONT-02 | Four players travel apart on the fastest supported modes with cold caches and abrupt turns/landings | Frame/tick latency distribution, memory, demand/queue telemetry and no routine voids or stalls |
| FRONT-03 | Excavate, build and alter a border region; unload, update a compatible content package, restart and revisit | Mutations, people, ownership, passage connectivity and provenance survive; incompatible generation changes fail or migrate explicitly |
| FRONT-04 | A distant society sends an actor/cargo through a not-yet-rendered destination; two clients arrive concurrently | One materialization, no duplicate population/work/resources, reconciled real history |
| FRONT-05 | Repeatedly trigger, cancel, reload and revisit an eligible adventurelet/deck opportunity | One accepted draw and instance, legal prerequisites, persistent consequences and no reset exploit |
| FRONT-06 | Traverse multiple independent seeds and long routes at equivalent progression | Track meaningful encounter/solution/terrain/culture motifs, repetition intervals, discovery gaps, choice diversity and human boredom/readability feedback |
| FRONT-07 | Let several narrative potentials share a faction or resource, then remove or redirect it | Contradictions detected; affected arcs continue, fail or close truthfully; no mandatory dark-lord replacement |
| FRONT-08 | Exhaust a controlled cache/storage/work budget during a vehicle encounter at a region boundary | Safe backpressure and recoverable error, no discarded canonical mutation, duplicated companion or frozen process outside its timing rules |

Automated motif statistics can find repetition and missing coverage. They do not prove wonder or enjoyment. Virtual playtesters can execute routes and report observations when a playable build exists; human acceptance still judges whether the frontier feels varied, coherent and worth exploring. A useful production gate includes unfamiliar seeds and long sessions, not only a showcase seed curated by its generator author.

## Sources

[^nms-talk]: Innes McKendrick, Hello Games, *Continuous World Generation in No Man's Sky*, GDC 2017; [official session listing](https://www.gdcvault.com/play/1024265/Continuous_World_Generation_in__No_Man_s_Sky_), [official GDC video](https://www.youtube.com/watch?v=sCRzxEEcO2Y). Session metadata/description reviewed; full transcript/slides not inspected.
[^worlds]: Hello Games, [*Worlds Part I*, version 5.0](https://www.nomanssky.com/worlds-part-i-update/), July 2024, engine improvements and planetary-variety notes.
[^origins]: Hello Games, [*Origins*, version 3.0](https://www.nomanssky.com/origins-update/), September 2020, generation and discovery migration notes.
