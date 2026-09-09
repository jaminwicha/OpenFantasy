# Local terrain and roguelike implementation reuse review

Reviewed 2026-09-08. This is a read-only source audit of seven local projects, with ten bounded reuse judgments. It inspected ancestor/project instructions, source inventories, dependency manifests, selected implementations and existing test/probe code. No external project was edited, built, launched or tested; no implementation was extracted. Test existence below does **not** mean a passing current run. The file hashes at the end identify the inspected candidates, independently of mutable working branches.

**Recommendation:** adapt specific algorithms, fixtures and interface lessons; retain Beta's new authoritative core. VirginiaCreepers and SubterraneanRobotWars contain the strongest reusable work. None of these projects supplies Beta's combined persistent geology, conserved fluid/ecology, arbitrary-instant history and multiplayer Wait model.

## Inventory and dependency boundary

| Project | Actual implementation | Audit result |
|---|---|---|
| Fluidics | Rust 2021; WGPU 27.0.1, Winit 0.30.5, egui 0.33, glam, CPAL and RustFFT; desktop/iOS crates | GPU fluid presentation candidate; its “core” imports UI/audio/platform libraries |
| RiverTile | Godot.NET.Sdk 4.4.1, .NET 8; seven C# source files | Connection/height puzzle logic; no discovered dedicated tests |
| VirginiaCreepers | Godot.NET.Sdk 4.4.1, .NET 8; plant, weather, wildlife, rendering and probes | Several separable policies and substantial relevant probe source |
| SubterraneanRobotWars (SRW) | Engine-free .NET 8/C# 12 library plus Godot frontend and native Rust botboard ABI | Useful generation/QA patterns; xUnit 2.7 tests; native-dependent tests copy a macOS dylib |
| RustAdventure | Rust 2021, crossterm 0.27, rand 0.8, serde/serde_json | Small terminal RPG; unsuitable world-generation foundation |
| MinesweeperAdventure | Browser JavaScript, global `Game` modules; no package manifest found | Interesting bounded puzzle neighborhoods; no discovered test suite |
| Cryptbro | Go 1.21 module `trader-ai-go`, urfave/cli | Financial trading application, not a crypt/dungeon game |

These are manifest declarations, not qualified current export combinations. No project-local `AGENTS.md` appeared in the excluded-build-directory inventory; the applicable ancestor instructions were read. Previous [RogueFiction/OpenFantasy/PacRogue/roguelinks topology analysis](../research-v0_8/places-puzzles-and-progression.md) remains complementary evidence and was not repeated.

## Ten concrete judgments

### 1. Fluidics: adapt presentation algorithms; reject authoritative water reuse

[sim.rs:334](/Users/alucinor/Workspace/Projects/Fluidics/crates/fluidics-core/src/sim.rs:334) dispatches interactions, advection, buoyancy, vorticity, divergence, Jacobi pressure iterations and gradient subtraction. [advect_dye.wgsl:10](/Users/alucinor/Workspace/Projects/Fluidics/crates/fluidics-core/src/shaders/advect_dye.wgsl:10) implements bilinear backtracing, boundary clamping, obstacle clearing and explicit dye dissipation. Float textures and deliberately dissipative color fields are suitable references for magical ink, smoke, underwater currents and restrained spell effects; they do not implement finite-volume water custody, oxygen, sediment provenance or deterministic historical reconstruction.

Adapt shader mathematics behind Godot's presentation boundary, driven by committed source/obstacle observations. Do not import WGPU/egui/audio infrastructure into the portable core. The headless initialization/obstacle tests at `sim.rs:819/875` can return early when no adapter/device is available, and do not establish mass conservation or cross-device bit identity. Cosmetic quality also still needs the user's pixel-art acceptance; a technically attractive fluid effect can undermine pixel readability.

### 2. RiverTile: adapt port puzzles; reject its solver as hydrology

[FlowSolver.cs:131](/Users/alucinor/Workspace/Projects/RiverTile/scripts/FlowSolver.cs:131) constructs adjacency and uses BFS to mark a single connected water network while rejecting leaks. Lines 288–349 check reciprocal ports, barriers and matching port heights; waterfall and jet ports span one level. This is a useful compact foundation for sluice, pipe, rotating-channel and ancient irrigation puzzles.

It stores `IsFlowing` and an illustrative direction, not conserved quantities or pressure. An enclosed source-free network can solve, and the solver does not make an uphill jet consume energy. Beta should adapt a typed port-compatibility predicate and diagnostic connectivity witness, replacing string directions and Godot `Vector2` in authority. Actual water movement still goes through Beta's fluid ledger. No dedicated tests were discovered; qualify disconnected loops, multiple sources, rotated waterfalls, blocked seams and powered uphill transport before reuse.

### 3. VirginiaCreepers: adapt organ lifecycle and dependency invalidation

[PlantLifecycle.cs:3](/Users/alucinor/Workspace/Projects/VirginiaCreepers/scripts/PlantLifecycle.cs:3) separately budgets foliage, reproductive organs and attachments, avoiding branches or tendrils consuming all bloom slots. Flower maturation depends on pollination, age and season. [PlantManager.Botany.cs:43](/Users/alucinor/Workspace/Projects/VirginiaCreepers/scripts/PlantManager.Botany.cs:43) retires organs by unlinking parent/child ownership, pests, wildlife targets and presentation caches.

These are concrete, relevant plant mechanisms. Adapt their policy decomposition and invalidation sequence, with stable organ IDs, species definitions, actor-exposure clocks and transactional resource/target updates. Literal summer strings, floating second thresholds and scene singletons are garden-specific assumptions. [SeasonalLifecycleProbe.cs:31](/Users/alucinor/Workspace/Projects/VirginiaCreepers/scripts/SeasonalLifecycleProbe.cs:31) already checks winter/pruning target release, surviving wood and regrowth capacity. Preserve these failure scenarios while adding offscreen simulation, branch reconstruction and harvest/feeding contention.

### 4. VirginiaCreepers: reuse ecological causality, replace spontaneous populations

[Wildlife.cs:320](/Users/alucinor/Workspace/Projects/VirginiaCreepers/scripts/Wildlife.cs:320) actually pollinates a target after interaction. Lines 384–409 consume an existing berry and may propagate a seed; this is stronger than purely decorative wildlife. However, lines 78–137 spawn seasonal animals from timers, and lines 454–480 create seedlings at screen coordinates rather than model seed transport and establishment.

Adapt the perception → target → interaction → plant consequence chain. Supply animals from a persistent local identity/cohort account, carry viable seeds with lineage and actual dispersal, and debit food quantities once. Keep bounded garden simplifications as explicit authored content where appropriate. Do not mistake `GardenStewardship`'s scalar spread-pressure meter for spatial biome succession. The useful lesson is visible causal feedback with manageable care, not copying its ecological accounting.

### 5. VirginiaCreepers: reuse lifecycle tests; replace transient deduplication

[HabitatProgress.cs:16](/Users/alucinor/Workspace/Projects/VirginiaCreepers/scripts/HabitatProgress.cs:16) uses a `ConditionalWeakTable` to avoid retaining retired plant graphs while deduplicating interaction types for live source objects. [HabitatLifetimeProbe.cs:20](/Users/alucinor/Workspace/Projects/VirginiaCreepers/scripts/HabitatLifetimeProbe.cs:20) checks duplicate credit, different objects at one location, maturation identity, reset isolation and garbage collection across generations.

Reuse these adversarial semantics and managed-cache lifetime techniques. Reject weak object identity as Beta's authoritative receipt key: rehydrating the same historical flower creates another object and could award credit twice. Persist `(source identity, reproductive cycle, interaction kind, receipt)` with branch/custody rules; store aggregate outcomes independently of loaded object references. Durable provenance need not keep every scene object alive. This distinction directly serves Beta's simultaneous memory-budget and no-lost-history requirements.

### 6. VirginiaCreepers: adapt metamorphic replay validation, not its clock wholesale

[SeasonalReplayProbe.cs:17](/Users/alucinor/Workspace/Projects/VirginiaCreepers/scripts/SeasonalReplayProbe.cs:17) compares 60/30/144 FPS, jitter with a hitch, and reduced motion across three species. Its sorted field comparison includes timers, RNG and graph relationships, excluding cosmetic pixels. This is a strong retained test pattern for proving that comfort options and rendering cadence do not change ecology.

[GardenSimulation.cs:30](/Users/alucinor/Workspace/Projects/VirginiaCreepers/scripts/GardenSimulation.cs:30) advances four scene owners in order at 60 Hz and clears accrued steps on a completion pause. Beta needs its existing command clock, explicit Wait islands, bounded resumable work and durable exposure cursors instead. Copying this scene clock would create a competing authority. Adapt the test matrix to whole/coarse/detail execution and save/reload, using independently enumerated canonical fields rather than an incomplete convenience hash.

### 7. SRW: adapt constructive capability graphs and reachability fixtures

[Dungeon.cs:116](/Users/alucinor/Workspace/Projects/SubterraneanRobotWars/src/SRW.Core/Gen/Dungeon.cs:116) connects successive room layers, repairs incoming reachability, finds a BFS entry-to-boss path and gates only side rooms. [CampaignTests.cs:45](/Users/alucinor/Workspace/Projects/SubterraneanRobotWars/tests/SRW.Core.Tests/CampaignTests.cs:45) checks ungated boss reachability independently; nearby tests compare seeded output and board constraints.

Adapt this constructive witness approach to adventurelet composition, adding Beta's combined knowledge, tools, consumables, party, timing, permissions and plane requirements. A reachable room graph alone does not prove walkable 3D geometry or playable progression. Preserve witnesses through spatial realization, then revalidate after actual world changes without forcibly repairing history. Its sequential room IDs, directed sector structure and always-ungated boss are optional local motifs, not Beta-wide policy. `Rng.Fork` at [Rng.cs:38](/Users/alucinor/Workspace/Projects/SubterraneanRobotWars/src/SRW.Core/Rng.cs:38) consumes parent state, so it cannot replace Beta's order-independent address/domain derivation unchanged.

### 8. SRW: adapt reproducible automated QA and inspect the native boundary

[Vetting.cs:44](/Users/alucinor/Workspace/Projects/SubterraneanRobotWars/src/SRW.Core/Gen/Vetting.cs:44) runs seeded generated-army matches, records winners/plies and flags degeneracy. Lines 119–126 reconstruct exact setup JSON; later paired evaluation controls seed and swaps sides. [VettingTests.cs:15](/Users/alucinor/Workspace/Projects/SubterraneanRobotWars/tests/SRW.Core.Tests/VettingTests.cs:15) verifies report production and replay consistency. Reuse the harness structure for generated builds, magic combinations and encounter decks, adding resource exploits, stalls and invalid decisions; bot win rates cannot certify human fun or NPC believability.

[Native.cs:35](/Users/alucinor/Workspace/Projects/SubterraneanRobotWars/src/SRW.Core/Engine/Native.cs:35) demonstrates an opaque Rust battle handle through in-process C# P/Invoke, with observer-specific visibility queries. This is useful local evidence that Rust need not imply IPC, not measured justification for changing Beta's language. No native implementation was audited here. Reject the campaign save pattern as Beta persistence: [Saves.cs:173](/Users/alucinor/Workspace/Projects/SubterraneanRobotWars/src/SRW.Core/Campaign/Saves.cs:173) regenerates sector zero and restores selected mutable state; it does not preserve Beta's arbitrary terrain/history.

### 9. MinesweeperAdventure: adapt explicit puzzle neighborhoods with new proofs

[engine.js:27](/Users/alucinor/Workspace/Projects/MinesweeperAdventure/js/engine.js:27) separates flat/cylinder/torus/Möbius/Klein wrapping from neighbor offsets and weights. This is promising for dream-realm riddles or authored runic boards, not a replacement for Beta's stable world coordinates.

Source inspection exposes necessary corrections: Möbius wrapping flips once whenever X is out of range rather than according to the parity of all crossed widths; neighbor enumeration does not deduplicate coordinates when narrow boards wrap multiple offsets onto one cell. Mine placement uses `Math.random` and silently stops when available cells run out. Decide whether repeated-neighbor weights are intentional, validate dimensions and mine capacity, seed accepted layouts, and require a deduction/solution witness. First-click safety alone does not prove a puzzle needs no guessing. No automated suite was discovered.

### 10. RustAdventure and Cryptbro: reject as terrain/ecology foundations

[RustAdventure/world.rs:350](/Users/alucinor/Workspace/Projects/RustAdventure/src/world.rs:350) stamps a straight river and random lakes; lines 379–486 add random floor cells, rectangles and disconnected candidate corridors before placing stairs. Generation uses global `rand::random`, with no connectivity witness in these functions. The finite 200×150 overworld/60×40 dungeon model offers little beyond algorithms already better represented in OpenFantasy/RogueFiction. Retain interesting content only through a separate inventory if desired; do not translate this world generator merely because it is Rust. No inline tests or dedicated test files were discovered.

[Cryptbro/models/types.go:8](/Users/alucinor/Workspace/Projects/Cryptbro/models/types.go:8) defines financial trades, orders and market data with wall-clock timestamps and floating prices. Its name was misleading for this audit. No terrain/gameplay candidate warrants expanding the review into its trading infrastructure; Beta's local-stock market already has materially different authority and resource contracts.

## Qualification before extraction

All six proposed cases are **unrun**; they supplement existing Beta proofs rather than introduce another runtime:

- **LOCAL-01:** feed the same authoritative fluid observations to GPU effects enabled/disabled and different graphics backends; world receipts, water and hazard outcomes remain identical.
- **LOCAL-02:** exercise rotating/height-channel puzzles with disconnected loops, unloaded seams, depleted sources and unpowered uphill ports; connectivity success never creates water or power.
- **LOCAL-03:** two animals target one berry across pruning, coarse/detail transitions, Wait and reload; one consumption, valid targets and no duplicated seed/credit survive.
- **LOCAL-04:** repeat seasonal care at varied frame cadence, reduced motion, unloaded intervals and historical reconstruction; compare canonical ecology, exposure and receipt state.
- **LOCAL-05:** realize a capability graph spatially, then remove a bridge/tool or change permissions; retain a valid witness or report truthful changed reachability, and reproduce every flagged encounter from saved inputs.
- **LOCAL-06:** exhaust tiny-board and multi-wrap puzzle neighborhoods, overcapacity placement and mixed mine values; record seeded layouts and a valid solution/deduction contract.

Prioritize plant policies/probes and generation witnesses in P01–P03; presentation effects and broader self-play belong behind their applicable art/content gates. Extraction must retain attribution/source hashes, remove incompatible dependencies, document state ownership and keep the old scenario as a regression fixture. These findings support selective reuse without reopening the settled need for a new persistent world authority.

## Inspected candidate fingerprints

SHA-256 values below bind principal source and test candidates to this review. Linked line numbers identify the relevant functions; manifests were read separately. These hashes certify bytes inspected, not correctness, authorship rights or passing tests.

| Candidate file | SHA-256 |
|---|---|
| Fluidics/crates/fluidics-core/src/sim.rs | `2427015148a7998af389b6af3128341dce42972d5076a1b242c2dcc6b1bae74d` |
| Fluidics/crates/fluidics-core/src/shaders/advect_dye.wgsl | `c21980762ce33a9a53fb1a24d81dc9c0d20a93ef8e41d2bbdd62ee9a33b92602` |
| RiverTile/scripts/FlowSolver.cs | `cb95239f3e0832cc27cf6af8e0761fc45367b1b04a9ea190a96c05e4c7e9f0cc` |
| VirginiaCreepers/scripts/PlantLifecycle.cs | `7655a0568709d85029042c9198236e14b5bde7e33a669662d93eabe9c271608e` |
| VirginiaCreepers/scripts/PlantManager.Botany.cs | `363c81a3f8d0322b2838129d9ef46cb228f65c943595e9c64fdf5ddbad6c19b4` |
| VirginiaCreepers/scripts/SeasonalLifecycleProbe.cs | `a72bd57ad40017b3ed2327a96506ffb9e2f91d1173dd517153e98f771f2ee080` |
| VirginiaCreepers/scripts/Wildlife.cs | `768d37e73a154186a9d0e04b48c2bd8ef2d5a510773e07eff3899603ca099386` |
| VirginiaCreepers/scripts/HabitatProgress.cs | `f18c78cc588f2ab772a999ff67bd72c5fe7d70bf3f47fc811c1c24ff744e9d86` |
| VirginiaCreepers/scripts/HabitatLifetimeProbe.cs | `2977166033e50def0bb1dfcd57153c86a64826f32bc267a5bfa0fdef139429c8` |
| VirginiaCreepers/scripts/SeasonalReplayProbe.cs | `f277f65d05fc7c0cac3bd637604c9d08eafdd12d19ca267da4d4655bd73171ed` |
| VirginiaCreepers/scripts/GardenSimulation.cs | `e493a613bb1e77c4404b4a02c1b881b67fa465442de300f7a32724241837a0da` |
| SubterraneanRobotWars/src/SRW.Core/Gen/Dungeon.cs | `c5d5d8bf759dfe8756739a3bcab70a4dbcd6fd639e5e131277b1b47766c53432` |
| SubterraneanRobotWars/tests/SRW.Core.Tests/CampaignTests.cs | `ecda54eef10970f327fc2d20e736f2206e49e9aeef78b0974f9b26e81206356b` |
| SubterraneanRobotWars/src/SRW.Core/Rng.cs | `3603366848f735185a462a6d96e82a902b86a8ae70783687d925c813cef2b4d0` |
| SubterraneanRobotWars/src/SRW.Core/Gen/Vetting.cs | `df14034d73ace66b46e39e884719e2ca6d352bf6927dc16a1b96a35f717467dd` |
| SubterraneanRobotWars/tests/SRW.Core.Tests/VettingTests.cs | `1f8df2adebbb3e18ce9bf21c9bee2389c9933978a6f42d5a53358225618754f0` |
| SubterraneanRobotWars/src/SRW.Core/Engine/Native.cs | `375d70366de36f2faf68f7a6d9c084a5a77fdae6c36c2d36ea336a3317b4fb18` |
| SubterraneanRobotWars/src/SRW.Core/Campaign/Saves.cs | `6d2b044a05845a436cf19db483117bf432902fcac064c645c718cdcf42651b18` |
| MinesweeperAdventure/js/engine.js | `f23cc923cd1fc4e76c44fd5b4283cb1215ac480af4f9c900658ff7759b653faa` |
| RustAdventure/src/world.rs | `6552a5f41be4ebdf53e8b43e14db1349669e5e7531828752829249137bf725b7` |
| Cryptbro/models/types.go | `9ef59a5ac0916453b4f2568afbbc7cb4be67034f593eba02577460596322c6d8` |
