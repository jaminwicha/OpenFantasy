# Local lifeform and simulation code: reuse assessment

Inspected 8 September 2026. This is a read-only source review of six projects under `/Users/alucinor/Workspace/Projects`. The recommendation is **selective adaptation, led by LifeFormzFable's small algorithms and regression fixtures**. None is a replacement for Beta's authority, historical archive or complete ecology. One focused headless save-continuation test was subsequently executed offline and passed; no application UI, full suite, external service or benchmark was run. No source was extracted or changed. Counts below describe discovered source/test markers, not passing suites or verified performance.

Root workspace guidance was read. No project-root `AGENTS.md` was present in these six inventories; the two Lifeformz project context guides were also inspected. Dependency manifests and actual implementation took precedence over README feature claims. Existing binaries, virtual environments, generated distributions, personal memories, saved conversations and credentials were not treated as evidence of a working candidate.

## Inventory and confidence limits

| Project | Actual inspected implementation and dependencies | Test evidence and disposition |
|---|---|---|
| LifeFormzFable | 14 Rust source files; Rust 2021, `ratatui 0.29`, `serde`, `serde_json`; separate simulation, world, targeting, onboarding and persistence modules | 89 `#[test]` markers, including 29 simulation and 20 persistence tests. One save-continuation test passed locally; 88 were filtered out and remain unrun in this review. |
| Lifeformz | Four Python files; terminal `blessed`, `pygame` audio, standard `random`; tiny tile/energy simulation | No automated test definitions found in these files. Useful minimal counterexamples, little direct reuse value. |
| Lifeformz1 | TypeScript cellular simulation with `World`, `Cell`, `Rules`, `Simulation`; no production npm dependencies, Vite/TypeScript/Jest tooling | Jest configuration and setup exist, but no matching test files in current `src`. Claimed test infrastructure is not a test suite. |
| Lifeformz2 | `src` is empty; Cargo manifest and substantial design notes only | No implemented Rust candidate or executable test. Its context guide explicitly says implementation has not started. |
| ParticleLifeSim | TypeScript/WebGPU and WGSL, Vite; compute/render buffers, species attraction matrix and browser UI | No test script or automated test definitions found in current source. Cosmetic technique reference only. |
| Smallworld | Python Generative Agents-derived environment; Django 2.2, NumPy 1.25.2 and OpenAI 0.27.0 among its pinned dependencies | The Django test file is a stub; backend `test.py` is a live-model request experiment, not a deterministic regression suite. Selected cognition modules are useful design evidence. |

These are manifest observations, not a recommendation to adopt their versions. Lifeformz1 has an MIT license file and Smallworld has an Apache-2.0 license file; no root license file was observed in the other four projects. Before copying source, retain actual ownership/license and third-party provenance with the chosen artifact; a README badge or local folder alone does not establish an asset's provenance.

## Nine concrete candidates

### LIFE-01 — Bounded inheritance: adapt the narrow mechanism

LifeFormzFable represents two allele arrays, computes an additive phenotype and samples one allele from each parent per locus. Mutation is explicitly clamped, making the inheritance calculation small and inspectable. [Genome structure and phenotype](/Users/alucinor/Workspace/Projects/LifeFormzFable/src/genome.rs:156), [crossover](/Users/alucinor/Workspace/Projects/LifeFormzFable/src/genome.rs:289), [mutation](/Users/alucinor/Workspace/Projects/LifeFormzFable/src/genome.rs:358).

Adapt a whitelisted trait subset to Beta's integer/rational ranges, per-species definitions and stable birth/seed-event RNG. Keep parent/source identity and tradeoffs. Do **not** import 17 genes, recessive-defect gameplay, automatic speciation or mutation into wings as the default: the latter is deliberately implemented at line 397 and conflicts with a minor-adaptation envelope and approved body/animation families. `genome.rs` has no direct test module; simulation tests cover related mating/cohort behavior, not comprehensive inheritance properties. This is an algorithm to qualify, not a tested drop-in genetics package.

### LIFE-02 — Food, habitat pressure and succession: adapt causal links

Eating actually debits carrion or vegetation; grazing changes soil. Local reproductive capacity samples fertility, soil and vegetation, while vegetation growth depends on season, soil and maturity. These are useful concrete links between food, habitat and reproduction. [Consumption](/Users/alucinor/Workspace/Projects/LifeFormzFable/src/sim.rs:509), [capacity](/Users/alucinor/Workspace/Projects/LifeFormzFable/src/capacity.rs:25), [succession](/Users/alucinor/Workspace/Projects/LifeFormzFable/src/world.rs:537).

Retain the relationships, replace the accounting. Soil returns toward a biome target without an explicit transported source; corpse nutrition and soil credits use different coefficients. This is not Beta's conserved nutrient/material ledger despite README language about a closed loop. Capacity is clamped to 8–50 locally and reproduction has an absolute 720-creature guard. Those terminal-game tuning limits cannot become a hidden Beta population cap. Two capacity tests and simulation mating/birth-capacity tests exist, unrun. Retest actual source stocks, seed arrival, species suitability and coarse/detail ownership before reuse.

### LIFE-03 — Conditional predation and threat priorities: adapt with real perception

The Rust AI distinguishes fire escape, recent attackers, predator danger, herd alarms and survival needs. Potential predation depends on hunger and relative power; prey choice scores distance, injury, illness, age and pack engagement. This already provides more useful behavior than “monster means attack player.” [Predator/perception checks](/Users/alucinor/Workspace/Projects/LifeFormzFable/src/sim.rs:1110), [prey utility](/Users/alucinor/Workspace/Projects/LifeFormzFable/src/sim.rs:1206), [priority actions](/Users/alucinor/Workspace/Projects/LifeFormzFable/src/sim.rs:1285).

Adapt the decision factors as shared action proposals using Beta's beliefs, risk and commitments. Replace whole-population scans with spatial queries and stable entity tie-breaks. The existing distance/cover checks are not authoritative three-dimensional occlusion, acoustic evidence or an ethical relationship model; different helpers also use different visibility formulas. A retained predator/grazer/defending-parent fixture must use one shared observation contract for players and NPCs. Existing combat tests establish useful examples but do not prove food-web stability or these behaviors at city scale.

### LIFE-04 — Stable target identity: retain the invariant

`targeting.rs` stores selection by stable `uid`, orders visible candidates by distance then UID, and resolves pool indices only for immediate access. Tests cover stable cycling, dead/out-of-sight filtering and examine selection. [Implementation and tests](/Users/alucinor/Workspace/Projects/LifeFormzFable/src/targeting.rs:11).

This maps cleanly to Beta's stable actor IDs, filtered target projections and controller handoff. Re-express it inside Beta's authority/projection boundary; do not transplant the visibility arithmetic or assume array positions remain identities. Destructive actions must revalidate the chosen target and cost at commit. Fable's automatic fallback from unavailable combat/mating targets should not silently redirect a consequential Beta action to another person. Add actor removal, branch travel and concurrent movement cases beyond the three existing, unrun targeting tests.

### LIFE-05 — Readable start selection and contextual teaching: high-priority adaptation

`onboarding.rs` scores eligible adult bodies using nearby water, food, climate margin, mates, predators and fire. Selection is pure, consumes no RNG and breaks ties by UID. Short guidance prioritizes immediate needs before exploration. [Safety scoring and selection](/Users/alucinor/Workspace/Projects/LifeFormzFable/src/onboarding.rs:27), [contextual guidance](/Users/alucinor/Workspace/Projects/LifeFormzFable/src/onboarding.rs:123).

Retain pure assessment and concise state-dependent teaching, but replace body possession/breeding priorities with Beta's career and teaching grammar. A weighted score is not a constructive route/resource/deadline witness; attractive averages can hide an impossible first task. Authoritative generation may inspect full state to validate a start, while displayed guidance must respect learned knowledge. Two source tests exist, including preference for reachable fresh water. Adaptation must add invalidated teachers, exhausted materials, split-party readiness and a genuinely viable alternative after world change.

### LIFE-06 — Save validation and continuation tests: reuse test structure, replace archive scope

Fable validates state and occupancy before saving, versions its envelope, rebuilds derived occupancy on load and writes through a temporary file with synchronization and rename. Tests compare bytes/state hashes and future continuation after loading. [Save/load](/Users/alucinor/Workspace/Projects/LifeFormzFable/src/persistence.rs:155), [atomic write](/Users/alucinor/Workspace/Projects/LifeFormzFable/src/persistence.rs:241), [continuation tests](/Users/alucinor/Workspace/Projects/LifeFormzFable/src/persistence.rs:843).

Reuse those failure categories and fixture shapes. Beta still needs transactional journals, historical interpreters, branch provenance and partial process stages; one complete JSON snapshot does not supply them. Fable's serializable SplitMix64 is deterministic under a fixed consumption order, but a single stream couples unrelated systems and integer range uses modulo reduction. [RNG](/Users/alucinor/Workspace/Projects/LifeFormzFable/src/rng.rs:3). The 600-tick same-process comparison is valuable but does not prove cross-platform floating-point replay, worker-order independence or large histories. [Determinism test](/Users/alucinor/Workspace/Projects/LifeFormzFable/src/sim.rs:2887). Keep Beta's domain-keyed RNG/numeric contracts; no Rust-language conclusion follows from these tests.

### LIFE-07 — Cellular phases: retain the lesson, reject direct integration

Lifeformz1 explicitly orders area effects, movement and bookkeeping and marks moved cells to avoid repeated moves. This is a readable model for staged resolution. [Simulation phases](/Users/alucinor/Workspace/Projects/Lifeformz1/src/core/Simulation.ts:132).

However, construction replaces global `Math.random` with a sine-based generator, coupling independent simulations. [RNG override](/Users/alucinor/Workspace/Projects/Lifeformz1/src/core/Simulation.ts:88). Some collision branches mark a temporary survivor and then call `setCell`, which constructs a fresh cell instead of installing that marked object. [Collision branch](/Users/alucinor/Workspace/Projects/Lifeformz1/src/core/Rules.ts:88), [replacement](/Users/alucinor/Workspace/Projects/Lifeformz1/src/core/World.ts:122). The intended movement guard is therefore not preserved by those branches; exact gameplay impact needs execution. Use these as adversarial examples for canonical transactions and preserved identity. Do not import color-based combat, global RNG replacement or assume configured Jest means these cases are tested.

### LIFE-08 — Attention, memory retrieval and interrupted plans: adapt contracts

Smallworld deduplicates local events, limits attention by distance and retains spatial memory. Retrieval combines recency, importance and semantic relevance; planning checks day changes, action completion and reactions, with a conversation cooldown. [Perception](/Users/alucinor/Workspace/Projects/Smallworld/reverie/backend_server/persona/cognitive_modules/perceive.py:25), [retrieval](/Users/alucinor/Workspace/Projects/Smallworld/reverie/backend_server/persona/cognitive_modules/retrieve.py:199), [planning](/Users/alucinor/Workspace/Projects/Smallworld/reverie/backend_server/persona/cognitive_modules/plan.py:931).

These suggest bounded attention, salient memory and interruptible commitments for Beta. Replace arena-equality visibility with actual topology/senses and stable ordering. Retrieval changes access timestamps and performs live embeddings; plan generation invokes model prompts and uses ambient randomness. Persist accepted optional-AI results and memory mutations as explicit events, with deterministic non-AI action planning remaining viable. Do not embed the Django/Python/model-server stack or expose the world's omniscient state to prose generation. The inspected test files provide no deterministic regression evidence; the live request experiment was deliberately not run.

### LIFE-09 — GPU particle interactions: optional presentation only

ParticleLifeSim uses read/write ping-pong buffers, 64-thread workgroups, finite-radius attraction/repulsion and Euler-Cromer integration. These are implemented GPU techniques worth considering for decorative motes or non-authoritative magical effects. [WGSL compute](/Users/alucinor/Workspace/Projects/ParticleLifeSim/src/shaders.wgsl:80), [dispatch](/Users/alucinor/Workspace/Projects/ParticleLifeSim/src/main.ts:565).

Its kernel still loops over every other particle, so all-pairs work is quadratic even when forces outside a radius are skipped. Floating-point integration, frame-count noise, browser-random initialization and one compute dispatch per active render-loop iteration are unsuitable foundations for Beta's conserved liquids, creatures or historical authority. No benchmark was run, and a shader comment claiming performance is not evidence of target capacity. Port only a justified visual effect through Godot's presentation layer; it cannot reveal or generate authoritative organisms.

## Deliberate exclusions and next proofs

Lifeformz's five integer traits are already averaged, perturbed and clamped, but mating creates a full-energy child without debiting parental energy; color distance determines attack/mating and global random drives results. [Small Python model](/Users/alucinor/Workspace/Projects/Lifeformz/lifeform.py:45). Its AI selects a tribe leader by scanning all matching-color creatures, without local knowledge. [AI](/Users/alucinor/Workspace/Projects/Lifeformz/ai.py:8). Fable supplies stronger mechanisms; retain these as negative examples rather than another runtime dependency. Lifeformz2's empty source means its attractive inheritance/configuration proposals remain unproved.

All extraction/port work is future implementation. Retain four **unrun** acceptance cases: **LIFE-V01**, fixed-parent bounded inheritance across retry/worker/query order without unsupported morphology; **LIFE-V02**, grazing→depletion→predation→remains→recovery with accounted stocks and no camera refill; **LIFE-V03**, valid contextual onboarding after lost teacher/resource and independent co-op readiness; **LIFE-V04**, save/crash/branch continuation plus stale-target and duplicate-action failures. Integrate them into existing Beta fixtures rather than launching six applications or creating parallel production engines. Candidate adoption requires measured maintenance/integration benefit and actual source provenance; the language and ecosystem decisions remain evidence-gated.

## Source fingerprints

SHA-256 values below identify the reviewed local bytes, not a license, successful build or immutable upstream revision. Recheck before adaptation because another task may update these projects.

### Executed evidence and its limits

On Darwin arm64 with Homebrew `rustc 1.88.0` and `cargo 1.88.0`, ran from LifeFormzFable:

```sh
cargo test --locked --offline --target-dir /private/tmp/beta-lifeformz-review-target persistence::tests::loaded_game_has_identical_future_continuation -- --exact
```

Result: **1 passed, 0 failed, 88 filtered out**, test execution 0.68 seconds after a 10.63-second debug build using cached dependencies. The existing test saves a game, restores it and compares state hashes after each of 40 additional ticks. It establishes this local continuation fixture only; it is not a cross-platform replay, performance, ecological-balance or Beta integration result. Build artifacts stayed in temporary storage, with no install, network request, UI launch or external source/lockfile edit. This positive evidence supports adapting the fixture, not declaring the entire source production-ready.

Manifest SHA-256: `Cargo.toml` = `5b789351cc610c350a93c3f22660ef2262b39b6906912f01b16d6d13a252a54b`; `Cargo.lock` = `8727f8393bba8ac175f72044c291b118abd28e77fe738005da46d723a92d332a`.

| Selected local file | SHA-256 |
|---|---|
| LifeFormzFable/src/genome.rs | `ac22daad421fce84eb61fb74d9f4432d4f37663f666361210e1dbecb5c239fca` |
| LifeFormzFable/src/sim.rs | `d9ca7759b997c1b3e47a715ca38f74be19f32b6ab4b163de1c5eceabd0aa33a3` |
| LifeFormzFable/src/capacity.rs | `0ffc2283259c25630480208a1040bf4b645461cd469290bc8fc024e92a12fbd4` |
| LifeFormzFable/src/world.rs | `2eee83e427b84fcf717645f9a975fd853ce159556c045bf9e8da5cb595858b35` |
| LifeFormzFable/src/targeting.rs | `420689ee7b90ddec12745f15718c773f65f727edd9c6edc6f354b714e0f14e0d` |
| LifeFormzFable/src/onboarding.rs | `3e2fb16d53f77906346ecdac10169148310ace4af626fc0dd2e71e267300a7c7` |
| LifeFormzFable/src/persistence.rs | `2c37f1248272615bf71a64d70a8d9ac269edba58c117dbc990e8cfd5273a3016` |
| LifeFormzFable/src/rng.rs | `41a958ae6de0e19f23019c9c508bb28f255dd0dbc80e0168442115279b27ba2c` |
| Lifeformz1/src/core/Simulation.ts | `85d7d88c99f58aafb595826a3084c3f6bbde84998a27ffbe5e5e1815ab1343d1` |
| Lifeformz1/src/core/Rules.ts | `77877803e48fa0428e4ff3a4a0f890dd63743859f72f5c6e905cfefaa0c84d22` |
| Lifeformz1/src/core/World.ts | `ffb545a06ef1e7110db87cb27daf6be7d8a3080bd378f1ceee8e9b7cb1f70be0` |
| Smallworld/reverie/backend_server/persona/cognitive_modules/perceive.py | `e0243136ac0bd78fd39e38c3dcb8239a219e9da1398a3d9d0224c2785befe29a` |
| Smallworld/reverie/backend_server/persona/cognitive_modules/retrieve.py | `a609d9ba431dd8cb0f3ec804d128ff9d1755719221127b45a248d20c51dc7120` |
| Smallworld/reverie/backend_server/persona/cognitive_modules/plan.py | `d0cf387a9015750b05606693d126d1d5e1c2d12b95a85b22937a2b3d8b14042a` |
| ParticleLifeSim/src/shaders.wgsl | `ff58d878564a99d097524bb199226cd23fdf3436117fc2ba8a8936e6eda855bd` |
| ParticleLifeSim/src/main.ts | `20eb830e7c50c57fafd39a239319556c548ebefb99e644c339332ec42d66470b` |
| Lifeformz/lifeform.py | `cbab7d4d1fb53b8362852fa4fcb7157b0fedd46e39e21ea15dedc6d52b981307` |
| Lifeformz/ai.py | `1f0c62bdcd727339cf35353306d05c9097e8b500082972d5c7f6a9a8f1ebc10a` |
