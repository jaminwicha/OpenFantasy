# OpenFantasy — Porting Brief: RogueFiction Topology Research

**Companion to:** `RUNTIME_SEED_DESIGN.md` §3.3–3.4, `RUNTIME_SEED_IMPLEMENTATION_PLAN.md` (7B) ·
**Status:** proposed · **Scope:** Milestone 7B accelerant · **Reference codebase:** `~/Workspace/Projects/RogueFiction`

## 0. Why this document

The runtime-seed plan commits to Jaquayed site topology with invariants enforced at
generation time (design §3.4, decision D11) and names "room scatter + MST + loop
reinsertion" as a first-wave embedder (plan §3.2, 7B). **RogueFiction (EMBERFALL) has
already implemented, tuned, and test-proven that subset** — in Rust, in a single-player
roguelike, but as pure integer grid logic with a seeded RNG and zero engine
dependencies: exactly the shape `src/Sim` demands. This brief maps what to port, what
to adapt, and what to leave behind, so 7B starts from established research instead of
a blank page.

What RogueFiction proved that is directly relevant here:

- Loop-reinsertion over nearest-neighbor room connection produces reliable, legible
  cycles on small grids (72×42), with a data dial for topology intensity.
- Secret doors are safe and *fun* only under two constraints it enforces
  mechanically: a **detour metric** (BFS walk-around ≥ threshold, or the secret is
  pointless) and a **never-load-bearing rule** (flood-fill with secrets opaque must
  still reach the goal).
- Locked doors need a placement-time reachability proof, and the proof is cheap
  (per-door BFS between the door's two sides with the door sealed).
- Topology intensity must be *paced*, not maximized — its "five-room / standard /
  xandered" dial exists because all-maze-all-the-time made loops illegible. This is
  the empirical grounding for our "invariant thresholds live in data, per site type
  and size" (D11).
- Empirical tuning matters: its secret-door placement shipped twice; the first
  version (strict wall-jamb pattern + 18-step detour) produced doors on <50% of
  floors — dead content. Measured across 200 seeds and relaxed to a 12-step detour
  with the detour metric as the only quality gate. Expect the same tuning loop here;
  build the measurement harness (7B's preview/stats tooling) before tuning.

## 1. What ports where

Reference files: `src/game/map.rs` (~750 lines, deps: seeded RNG only),
`src/game/world.rs` (arrival/persistence semantics), `tests/topology.rs`,
`docs/AREA-DESIGN.md` (the audit + spec this grew from).

| RogueFiction artifact | Ports to | Notes |
|---|---|---|
| `walk_distance` (BFS step count) | `Sim/SiteGen/GridSearch.cs` | Shared primitive for the two safety proofs below. Integer-only already. |
| `place_secret_doors` (candidate scan + detour ≥ 12 gate) | `Sim/SiteGen/SecretPaths.cs` | Implements invariant 4's *quality* half — the design says "at least one per floor"; RogueFiction adds "and it must shortcut a real detour." Adopt both. |
| `place_doors` lock-safety check (seal door → BFS sides) | invariant validator (D11) | Generalizes to any gated edge: locked door, boss gate, key barrier. Known limitation: RogueFiction checks per-door at placement time, so a *later* locked door can invalidate an *earlier* door's alternate route. Its global flood test catches this; here, run the global check once after all gating, per floor (cheaper and airtight). |
| `flood_reaches_stairs(doors_passable)` test family | validator + `--selftest` | The "honest route" reachability proof: secrets opaque, locks sealed, plain doors passable. Port as property tests, not golden vectors (§2). |
| `try_generate` (scatter → nearest-connect → loop edges → far-goal selection) | `Sim/SiteGen/Embedders/RoomScatterEmbedder.cs` | The 7B "catacombs" recipe, minus MST (nearest-carved-room connection approximates it; swap in true MST over squared int distances per D12 if wanted). Loop-edge targeting: RogueFiction guarantees the first loop lands on the goal room — a cheap way to satisfy "≥2 routes to the objective" before the cycle grammar exists. |
| `erode_caves` (2-pass CA erosion sparing door jambs) | modifier pass registry | Plan §3.5 "pass layering" — this is a working decay pass that provably cannot break connectivity (only opens floor). The jamb-sparing rule matters; without it, erosion strands doors in open space. |
| `carve_channel` (fill-only-walls L-channels) | modifier pass registry | Water/slag lanes that cut through walls as continuous risky shortcuts. Fill-only-walls is the trick: adds walkable tiles, so connectivity is monotone — no revalidation needed. Maps to grotto water routes and invariant 4's "underwater passages." |
| `place_chasm` + warn-then-commit step | level-skip links (invariant 3) + client UX | Two-depth drop; first step onto the edge warns (free), second confirms. Port the *interaction contract*, not the code — an MMO needs the same "falls are chosen, never misclicked" rule, especially with latency. |
| `MapStyle` dial (FiveRoom / Standard / Xandered) | D11 threshold tables | The pacing principle: set-piece floors (entrances, boss floors) get low thresholds *on purpose*; chapter-enders get high ones. Encode as per-site-type, per-floor-index threshold rows, and keep the rationale: linear floors are what make xandered floors read as xandered. |
| `Arrival::{Descend, Ascend, Collapse}` (`world.rs`) | `MapInstance` entry logic (7C) | Explicit arrival semantics per connection: which stair/portal you took decides where you stand. The Collapse variant (a connection that dumps you somewhere unplanned) is a cheap discontinuity that plays much bigger than it costs. |
| Draft-hint breadcrumb (1-in-3 ambient line near a secret) | detailing/dialog layer | Discoverability pattern: secrets need a tell, or players never search. Translate to a visual/audio tell at 16-bit fidelity (dust motes, faint seam tiles, sound cue). |
| `SavedLevel` freeze/restore | **do not port** — see §3 | Persistence models differ fundamentally. |

## 2. RNG adaptation (the one real impedance mismatch)

RogueFiction draws all generation from a **single sequential RNG stream** — floor N's
layout depends on every roll before it. OpenFantasy requires **order-independent pure
derivation** (`Generate(WorldSeed, StableId, DomainTag)`, D8) so any floor regenerates
identically, on demand, in any order.

The algorithms are indifferent to this; only the plumbing changes:

- Seed a fresh RNG per site floor: `rng = XorShift64(StableId.Hash(worldSeed, siteId, floorIndex, salt))`.
- RogueFiction's retry-until-valid loops (`generate` retries `try_generate` until
  min-rooms met; secret/chasm placement retries candidates) become **salt-retry**
  (D11): a failed floor regenerates with `salt+1`, still pure.
- **Do not attempt cross-language golden-vector parity.** It would require porting
  PCG32 and freezing call order — all cost, no benefit. The portable artifacts are
  the *property tests*: connectivity, cycle counts, non-load-bearing locks, detour
  minimums, determinism (same derivation key twice → identical tile grid). Port
  RogueFiction's test suite as invariant assertions over reference seeds in
  `--selftest`.

## 3. What not to port, and why

- **The sim (`world.rs` combat/AI/FOV/story).** Turn-based single-player bump combat;
  no overlap with the ATB MMO sim. Only the arrival semantics and interaction
  contracts (table above) transfer, as design.
- **`SavedLevel` persistence.** RogueFiction freezes whole visited levels because its
  generator is stream-order-dependent (regeneration would produce a different level).
  OpenFantasy's pure derivation makes freezing unnecessary and wrong: regenerate from
  the derivation key, overlay delta records. The *equivalences*: RogueFiction's
  "opened door / revealed secret / opened compartment persists" become delta records
  (`DoorOpened(siteId, doorId)`, `SecretFound(...)`) — note secrets and doors already
  need stable per-feature IDs from the generator, so mint them at placement time.
- **FFI binding of the Rust crate.** Technically feasible (cdylib + P/Invoke,
  ~2–3 days with build integration), and wrong: a second toolchain in the server
  deploy to wrap ~500 lines of portable integer logic that needs C#-side StableId
  and salt-retry integration anyway. Port > bind.
- **The generator as-is for every site family.** RogueFiction's embedder is one row
  of the §3.2 catalog. The cyclic graph grammar (7B's synthesizer) remains the
  target architecture; the port is scaffolding and safety net, not a replacement.

## 4. Suggested sequencing (fits inside 7B as planned)

1. **`GridSearch` + invariant validators first** (~a day): flood reachability with
   configurable passability (secrets opaque / locks sealed / doors passable), BFS
   detour metric, cycle-rank counter, articulation-point check (invariant 5 — the one
   validator RogueFiction *doesn't* have; add it here). Wire into `--selftest` with a
   deliberately-broken config that must fail loudly (7B exit criterion).
2. **`RoomScatterEmbedder`** (~1–2 days): scatter → connect → loop edges → goal-room
   selection → doors/locks → secrets → modifier passes (CA erosion, channels). Lands
   the "catacombs" recipe with working topology before the grammar exists.
3. **Threshold tables** (hours): encode the topology dial as D11 data, per site
   type/size/floor-index, with RogueFiction's tuned values as starting points
   (loops ≥ 2 standard / ≥ 4 xandered per ~16-room floor; ≤ 2 secrets; detour ≥ 12;
   lock rate ~1 in 12 doorways).
4. **When the cyclic grammar lands**, keep the validators (regression net, per plan)
   and demote the embedder to the grammar's embedding backend for room-scatter
   families — its loop-edge and safety machinery is exactly what "loop edges from the
   cycle graph must be added back" (plan §3.2, BSP row) needs anyway.

RogueFiction stays useful afterward as the topology testbed: sub-second test cycles
and a TUI renderer for eyeballing layouts, with `docs/AREA-DESIGN.md` as the running
design log. Anything proven there ports here by this brief's mapping; nothing here
takes a dependency on it.
