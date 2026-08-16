# OpenFantasy — Milestone 7: *The Depths Below*

**Version:** 1.0 · **Date:** 2026-08-09 · **Builds on:** M6 (`9aa9615`) ·
**Spec:** `RUNTIME_SEED_DESIGN.md` + `RUNTIME_SEED_IMPLEMENTATION_PLAN.md` (7A–7C) ·
**Research port:** `ROGUEFICTION_PORTING_BRIEF.md`

## Theme

The first runtime-seed milestone: the sim learns to *generate* content at
runtime instead of only loading it. Deterministic noise + biome fields lay the
terrain foundation for the chunked overworld (M8), and the four dungeon
regions plus the new Brasshollow sewers become **multi-floor, Jaquayed,
procedurally generated delves** — instanced map spaces hosted alongside the
overworld, generated pure from `(WorldSeed, siteId, floor)`.

## What shipped

### 7A — Deterministic generation foundations
- `Sim/StableId.cs` — content addressing (plan D3/D8): everything procedural
  derives from `Hash(seed, coords, domain)`; nothing is stored, only re-derived.
- `Sim/Noise.cs` — integer value noise + FBM (Q16 fixed point, no floats, D1).
- `Sim/BiomeField.cs` — elevation/temperature/moisture/anomaly fields → 13
  biomes; golden-hash pinned; `--previewgen [seed]` renders a biome map PNG
  (`docs/screenshots/worldgen-preview.png`).
- `SimConstants.GeneratorVersion` (D4) stamped for future delta persistence.

### 7B — Jaquayed site generation (RogueFiction port)
- `Sim/SiteGen/` — the topology machinery, ported per the brief:
  `GridSearch` (BFS detour metric + honest-route flood), `SiteValidator`
  (D11 invariants incl. the articulation-point check the reference lacked),
  `RoomScatterEmbedder` (scatter → nearest-connect → loop reinsertion with
  goal-room targeting → doors with lock-safety proofs → secrets gated on a
  ≥12-step detour → terrain passes: cave erosion, water/slag channels,
  rubble, chasms), `SiteRecipe` (the FiveRoom/Standard/Xandered pacing dial
  as data), `SiteGenerator` (per-floor pure derivation + salt-retry).
- Key deviation from the reference: secrets place **after** terrain passes so
  the detour metric holds on final geometry (the validator re-proves it there).
- Property battery: 30 seeds × 5 sites × all floors — determinism, honest
  route, coherence after reveal, spawn/chest placement. Full 5-floor delve
  generates in ~2.7 ms.
- The cyclic graph grammar remains the target topology synthesizer (plan
  §3.1); per the brief it lands on top of these validators in a later phase —
  the style dial + loop targeting satisfy the invariants meanwhile.

### 7C — Instanced delves in the live game
- `Entity.MapId` + `World.Instances.cs`: map spaces (0 = overworld, >0 =
  instance floors), lazy creation, per-floor monster/boss population from
  recipe spawn tables, respawn queues.
- **Five sites live**: Hollow Mine, Sunken Crypt, Emberdeep (slag lanes
  sear), Storm Spire (5 stories, no chasms), and the **Brasshollow Sewers**
  — entered via three grates around the capital, each arriving at a
  different floor-0 room (multi-entrance topology proven under an anchor town).
- **Arrival semantics** (per the brief): main stair → next floor's entry;
  collapsed second stair → emerges by the lower second stair; chasm = a
  *chosen* drop (interact, never a misstep) skipping a floor, landing at the
  landmark with fall damage; death → the crystal calls you home.
- Secrets (E near a suspicious wall), pryable locks, and chests: contents
  are `Hash(seed, chestId)` (D9) — fixed at world creation, once per
  character, persisted in saves. Logging out below saves the surface entrance.
- **Networking**: protocol v5 — per-map snapshots (the first interest
  management), `MapData` tile streaming with RLE, `SiteOp` interact verb.
- **Client**: instance floor rendering from streamed tiles (placeholder art
  reuse — mine/rock/bridge/dock tiles), underground light tint, and the new
  **auto-map (M key)**: floor layout, stairs, hazards, chests, entities —
  the UI half of the Jaquays landmark rule.

## Verification

- `--selftest`: **37 passed, 0 failed** — including noise/biome golden
  hashes, per-floor invariant + determinism assertions, a deliberately
  broken validator config failing loudly, delve enter/descend/ascend/exit +
  sewer grate entry, chest once-per-character persistence, and a 400-tick
  delve-activity determinism run.
- Two-bot multiplayer smoke: both exit 0 on protocol v5.
- `--auto` playtest screenshot green (overworld unaffected).

## Known placeholders / follow-ups

- Delve tiles reuse overworld art (bridge = stairs down, dock = stairs up,
  crystal = chest); sewer grates are invisible on the overworld until the
  grate tile art lands. An art pass replaces `WorldRenderer.SiteTileToLogical`.
- Locked doors always pry open (no keys yet — lock-and-key rides the cyclic
  grammar phase). Secret walls have no visual/audio tell yet (brief's
  breadcrumb pattern pending).
- Stat sheet exists nowhere yet; the auto-map landed, the character sheet is
  next UI debt.
