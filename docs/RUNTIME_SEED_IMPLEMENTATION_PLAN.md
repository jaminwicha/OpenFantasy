# OpenFantasy — Runtime Seed World: Implementation Plan

**Companion to:** `RUNTIME_SEED_DESIGN.md` · **Status:** proposed (rev 3) · **Scope:** Milestones 7–9

Rev 2 additions: full-stack seeded content (settlements, NPCs, shops, economy, quests —
design §2.3, §3.1) and Jaquayed adventure-site variety (design §3.3–3.4).
Rev 3 additions: generation technique catalog, per-family recipes, and blend
patterns (§3 below); cyclic graph grammar adopted as the topology synthesizer.

## 0. Feasibility Verdict

Feasible, and unusually well-positioned. The expensive architectural bets are already
made: the sim is engine-free, integer fixed-point, fully seeded (`XorShift64`), and the
world seed already travels to clients in the `Welcome` message. The migration seam is
clean: `WorldMap.TileAt` returns `-1` outside the 512×512 bounds today — the plan
replaces that boundary with a procedural chunk source, so the existing hand-tuned
Aldermere map *becomes* the Anchor Core with zero content migration.

## 1. Design Decisions (resolve before Phase A)

| # | Decision | Recommendation |
|---|---|---|
| D1 | **Noise math** | No floats in the sim (doctrine). Integer hash-based value noise (SplitMix64-style tile hash → fixed-point FBM with integer smoothstep). No `System.Math` transcendentals. |
| D2 | **World bounds** | Finite-but-vast. Positions are `int` world units (32/tile) → hard ceiling ±67M tiles. Playable bounds **±1,048,576 tiles (±32,768 chunks)**. "Endless" in practice; no coordinate-type or wire-format migration. |
| D3 | **Purity rule** | All generation is `Generate(WorldSeed, StableId, DomainTag)` — pure, re-derivable, never stored. Per-chunk/site RNG stream = `Hash(WorldSeed, coords, domain)`. |
| D4 | **Generator versioning** | Stamp every delta record with `GeneratorVersion`; bump on generator changes. Player deltas survive; terrain drift accepted (Minecraft model). Cheap now, impossible to retrofit. |
| D5 | **Client terrain** | Clients generate terrain/layouts locally from the seed (render-only, mirrors the current world.json trust model); server sends deltas + entities. |
| D6 | **Anchor stitching** | Anchor continent = existing 512×512 map at origin; ~16-tile forced-ocean blend band, then noise. Later anchors use the same mask-override pattern. |
| D7 | **Scale honesty** | Not an MMO. Listen-server, ~16 players, interest-managed snapshots. Meta systems built server-authoritative so a dedicated deployment inherits them. |
| D8 | **Stable content addressing** | Every procedural entity (settlement, NPC, shop, site, floor, chest) gets an ID derived from generative coordinates. Quest refs, waystones, discovery log, and deltas key on these IDs — they survive unload/reload. |
| D9 | **Loot determinism** | Chest contents = `Hash(WorldSeed, ChestId)`, fixed at world creation and independent of open order — avoids coupling loot to the world RNG stream. Mob drops keep using the tick-stream RNG (input-dependent is fine there). |
| D10 | **Sim calendar** | Restocks, quest boards, and rumors key off a sim-side `DayIndex = Tick / TicksPerDay`. `TicksPerDay` moves from presentation-only to sim-meaningful. Never wall clock. |
| D11 | **Jaquays invariants as validation** | Site topology graphs are checked against the design §3.4 invariants at generation time; a failing graph regenerates with `salt+1` (still pure). Invariant thresholds live in data, per site type and size. |
| D12 | **Geometry & solver determinism** | No float computational geometry: room layouts use Poisson-disc placement + MST over squared integer distances (no Delaunay triangulation, no physics separation). Constraint solvers (WFC-style) run with seeded deterministic cell/tile ordering and bounded backtracking, falling back to salt-retry per D11. |

## 2. The Seeded Content Matrix

Single reference for "everything is seeded" (design §2.3). Each domain: pure derivation
key → what the delta layer may store on top.

| Domain | Derived from | Delta-persisted state |
|---|---|---|
| Terrain chunk | `(seed, cx, cy)` | tile edits, player structures |
| Settlement site (exists? tier?) | `(seed, siteCell)` | — |
| Settlement layout & architecture | `(seed, settlementId)` | — |
| NPC roster (name, race, appearance, personality, schedule, home/work) | `(seed, settlementId, npcIndex)` | per-player quest flags |
| Shop lineup & stock | `(seed, shopId, dayIndex)` | purchases this cycle |
| Regional prices | `(seed, regionCell)` + biome supply themes | (later: dynamic drift) |
| Quest board / rumors | `(seed, settlementId, dayIndex)` | taken/completed contracts |
| Monster spawn tables & mutations | `(seed, cx, cy)` + biome | kills awaiting respawn |
| Gathering nodes / farm plots | `(seed, cx, cy)` | harvested state |
| Adventure site (type, theme, name) | `(seed, siteCell)` | discovery record |
| Site floor layout (graph + tiles) | `(seed, siteId, floor, salt)` | cleared bosses, opened doors |
| Chest / fixed loot | `(seed, chestId)` (D9) | opened flag |

Two integration rules keep the world coherent rather than a pile of independent tables:
generated quests/rumors must reference *real* nearby generated content (the actual sites
and monsters in surrounding chunks), and settlement economies must reflect the actual
surrounding biome (D8 makes both possible).

## 3. Generation Technique Catalog & Blending

The site pipeline has three layers — **topology** (what connects to what),
**embedding** (how the graph becomes tiles), and **detail** (how tiles get dressed).
Every technique below is assigned to a layer; blending happens *within* a layer via
explicit patterns, never ad hoc. All techniques run under D1/D3/D12 (integer, pure,
seeded).

### 3.1 Topology layer

| Technique | Core idea | Evaluation |
|---|---|---|
| **Cyclic graph grammar** (Dormans, *Unexplored*) | Compose each floor from nested cycle motifs: two-route split, lock-key loop, hidden shortcut, dangerous detour, gated return | **Adopted as the synthesizer.** Produces loops *by construction* — the Jaquays invariants become a safety net instead of a search. Motif choice doubles as pacing control (foreshadowing, risk/reward routes). |
| **Lock-and-key / mission grammar** | Annotate the graph with keys, levers, and boss gates before embedding | Adopted as annotations on the cycle graph — guarantees chokepoints are *designed* (invariant 5) and secrets have payoffs. |

### 3.2 Embedding layer

| Technique | Core idea | Evaluation → role |
|---|---|---|
| **BSP subdivision** | Recursively split space into rooms + corridors | Orderly, man-made feel; tree-shaped by default so loop edges from the cycle graph must be added back. → catacomb wings, castle underdungeons, barracks. |
| **Room scatter + MST + loop reinsertion** (TinyKeep-style) | Poisson-scatter rooms, connect via MST on squared-int distances, re-add k% of edges as cycles | Direct loop-density knob; organic placement. True Delaunay + physics separation dropped per D12 — Poisson spacing makes them unnecessary. → catacombs, ruin underlevels, grottos. |
| **Cellular automata** | Noise seed + smoothing generations | Natural caverns with emergent loops; needs flood-fill connectivity repair (tunnel or discard orphans). Doubles as a *decay pass* over ordered layouts. → caverns, grotto chambers, ruination. |
| **Perlin worms** (noise-carved tunnels) | Carve winding galleries along seeded paths | Long connective tunnels and sloping shafts — the natural implementation of level-skipping links (invariant 3). → cave connectors, mine breakthroughs, escape tunnels. |
| **Drunkard's walk** | Random-walk carving | Too erratic as a base generator; excellent as a *damage/burrow pass*. → collapse zones, ruin decay, beast burrows. |
| **Diffusion-limited aggregation** | Particles aggregate into dendritic growths | Striking organic branches, but dead-end-prone — braid afterwards. Niche, high flavor. → crystal caverns, Aethertech overgrowth, root systems. |
| **Maze algorithms + braiding** | Perfect maze (backtracker/Prim), then remove k% of dead ends | Tunable corridor loopiness; monotonous alone — inject chambers. → sewer laterals, hedge mazes, crypt wings. |
| **Agent tunnelers** | Miner agents dig with turn/branch/room probabilities | Man-made mine logic (cart-track straightaways, junction rooms). → mines, sapper tunnels. |
| **Voronoi partitioning** | Seeded sites partition space into cells (integer distance compares) | Zone seams and districts, not corridors. → town districts, cavern chamber clusters, farm plots, **zone assignment for blends**. |
| **L-system / agent road growth** | Grammar-grown networks | Street-shaped, hierarchical. → town roads, sewer mains echoing the street grid above, river/ravine courses. |
| **Recursive interior subdivision** | Treemap-split a footprint into rooms | Believable floorplans at 16-bit scale. → building interiors, keep/temple floors, tower stories. |
| **Space colonization** | Trails grow toward attractor points | Organic trail webs with natural loops between attractors. → forest depths, grove networks. |
| **Poisson-disc scatter** | Even-but-random placement under a density mask | Placement workhorse, not a carver. → trees, props, room seeds, site distribution. |

### 3.3 Detail layer

| Technique | Core idea | Evaluation → role |
|---|---|---|
| **Wave Function Collapse** | Constraint-solve tile adjacency from small hand-authored samples | Deliberately **demoted to decoration**: macro layout stays graph-driven (WFC gives no topology guarantees), but WFC excels at locally coherent ornamentation. Runs constrained — macro tiles pre-collapsed as hard constraints (D12). → temple mosaics, Aethertech circuitry floors, town plazas, rubble fields. |
| **Prefab vaults / herringbone chunk stitching** (Brogue-style) | Hand-authored micro-rooms injected into generated layouts | The main channel for authored 16-bit charm at scale; implements landmark, boss, and secret set pieces (invariants 4, 6). → all families. |
| **Wang autotiling (47-tile)** | Corner/edge bitmask transitions | Presentation seam-fixer for everything above; roadmap Phase 2, prerequisite-level priority. |

### 3.4 Blend patterns

Blends are data-declared per family (weighted recipe tables), never emergent accidents.
Rare cross-family blends are treated as discoveries (a flooded catacomb, a rooted tower).

1. **Zonal blend** — partition the site (Voronoi cells / BSP leaves), assign a different
   embedder per zone, stitch seams with explicit transition rules (breakthrough rubble,
   door frames, waterfalls). Signature: mine grid ↔ cavern breakthrough.
2. **Pass layering** — base generator, then modifier passes: CA erosion to ruin a
   crisp layout, drunkard damage, DLA growth on walls.
3. **Carve union** — union of carvers in one tile mask (worm galleries ∪ CA chambers),
   connectivity re-validated after.
4. **Vault injection** — cycle-graph nodes reserved as prefab vaults; the embedder
   plants them first and generates around them.
5. **Constrained detail solve** — WFC/decor passes run after embedding with macro
   tiles locked.
6. **Hierarchical handoff** — exterior generator emits footprints; interior generators
   recurse within (town → building → floor; castle → keep → underdungeon).

### 3.5 Family recipes

| Family | Recipe (topology is always cyclic grammar) |
|---|---|
| Caverns | CA chambers ∪ worm galleries (carve union); landmark vaults; DLA crystal variant |
| Mines | Agent tunnelers + grid rooms; drunkard collapse zones; zonal breakthrough into CA cavern |
| Catacombs | Room scatter + MST + loop edges; braided-maze crypt wings; boss/secret vaults |
| Sewers | L-system mains derived from the town street graph + braided maze laterals; junction-chamber vaults; water mask |
| Temples | Symmetric subdivision; WFC mosaic detail; hidden sanctum vault; nested crypt sub-level via catacomb recipe |
| Towers | Per-story interior subdivision; collapsed-floor damage pass linking non-adjacent stories; basement via catacomb recipe |
| Castles / Forts | Wall/courtyard grammar → hierarchical handoff to interior subdivision; BSP underdungeon; worm escape tunnel surfacing in the wild |
| Surface Ruins | Intact grammar layout → decay passes (CA erosion + drunkard damage); buried underlevels via catacomb recipe |
| Forest Depths | Poisson trees under biome-noise density; space-colonization trails between Voronoi clearings; grove-heart vault |
| Aethertech | Concentric subdivision; WFC circuitry floors; teleporter edges from the cycle graph; DLA crystal overgrowth |
| Sunken Grottos | CA + water-level masks; shipwreck vaults; vehicle-gated entrances |
| Sky Islands | Noise island mask + tower recipe; chute edges back to the surface |

## 4. Phases

Each phase lands independently, keeps `--selftest` green, and is playable at exit.
Ordering rationale: the site-graph generator and instanced maps (M7) build the
"runtime map from seed" machinery on small, low-blast-radius surfaces before the
streaming overworld (M8); the settlement stack (8C) reuses the stable-ID and
day-index machinery proven in M7.

### Milestone 7 — Runtime Generation Foundations

**7A. Deterministic noise + preview tooling**
- `src/Sim/Noise.cs`: integer value noise, FBM, domain hashing (D1, D3);
  `src/Sim/StableId.cs`: content-address hashing (D8).
- Elevation / temperature / moisture fields + biome classification table.
- Preview tooling (`--previewgen` or `tools/preview_worldgen.py`): biome-map PNG for
  a seed; used for art/design review before anything ships.
- *Exit:* self-test pins golden hashes for reference-seed fields; preview PNG committed.

**7B. Jaquayed site topology core**
- **Cyclic graph grammar** (§3.1) as the topology synthesizer: floors composed from
  nested cycle motifs with typed edges (corridor, stair, chute, secret door,
  teleporter, water passage) and typed nodes (entrance, landmark, boss gate,
  sub-level seam); lock-and-key annotations on the graph.
- Invariant validator (D11): entrance count, cycle count per floor, ≥2 adjacent-floor
  connections, level-skip links, secret-path quota, chokepoint check (articulation
  points vs designed boss gates), landmark per floor. Thresholds data-driven per site
  type/size. With cyclic synthesis the validator is a regression net, not a search.
- **Embedder registry**: `ISiteEmbedder` implementations + modifier passes, composed
  per family from data-declared recipes (§3.5). First two recipes: **caverns**
  (CA ∪ worm galleries, vault injection) and **catacombs** (room scatter + MST +
  loop edges, vault injection).
- Preview tooling: floor-map PNG + graph stats dump per recipe for tuning.
- *Exit:* self-test generates N sites across reference seeds and asserts every
  invariant on the *embedded* result (not just the abstract graph); a deliberately
  broken validator config fails loudly; preview sheets reviewed.

**7C. Instanced adventure maps in the live game**
- `MapInstance`: sim hosts overworld + N instanced maps; entities carry `MapId`;
  snapshots filter by `MapId` (first, easy interest management).
- Convert `hollow_mine`, `sunken_crypt`, `emberdeep`, `storm_spire` into multi-floor
  Jaquayed delves (cavern + catacomb families), difficulty scaling per floor, boss on
  final floor, chest loot per D9.
- **Brasshollow Sewers**: a loop-lattice sewer site under the anchor capital with ≥3
  grate/cellar entrances — proves multiple-entrance topology and site-under-authored-
  content stitching before any frontier work.
- *Exit:* party clears a 5-floor delve using at least one loop and one secret path;
  same seed → identical floors; state hash stable across 1000 ticks in an instance;
  screenshots (delve floor, sewer entrances).

### Milestone 8 — The Chunked Overworld

**8A. Chunk infrastructure (invisible refactor)**
- `Chunk` (32×32), `IChunkSource`, chunk cache; `StaticChunkSource` serves the existing
  `world.json` verbatim; all tile/walkability/safe-zone queries routed through chunks.
- *Exit:* zero behavior change — Milestone 6 pinned state hashes pass byte-identical.

**8B. Procedural frontier terrain**
- `ProceduralChunkSource` (7A fields) beyond the anchor coast; blend band per D6.
- Server chunk lifecycle: fault-in on proximity (sync, in-tick), evict cold chunks,
  `GeneratorVersion` stamps (D4), per-chunk delta records.
- Client `WorldRenderer` streams `TileMapLayer` chunks from local generation (D5).
- Entity interest management: per-player snapshot radius replaces zone-wide broadcast
  (protocol bump).
- *Exit:* sail past the old map edge indefinitely; two clients see identical remote
  terrain; tick ≤ 2 ms during faulting; determinism test crosses chunk boundaries.

**8C. The full settlement stack**
- Deterministic settlement placement over the chunk grid (spacing via site-cell
  hashing); tier ladder hamlet → village → town → port.
- Grammar town layout + biome architecture styles + phoneme names (design §3.1).
- **Population roster**: per-NPC seed-derived name, race, paper-doll appearance
  (reuses the compositor), personality, role, schedule, home/work tiles.
- **Shops & economy**: tier/biome shop lineup; stock derived per `(shopId, dayIndex)`
  (D10) with purchase deltas; regional price modifiers from biome supply themes.
  Existing `Npc.Shop` fixed-list system is generalized, not replaced — anchor-town
  shops keep working.
- **Tavern layer**: rumor + bulletin-board contract generation per settlement per day,
  referencing real nearby content (§2 integration rules).
- *Exit:* walk into a never-visited frontier town and find a coherent, fully staffed,
  economically themed settlement; return a week later to rotated stock and new
  contracts; all reproducible from seed.

**8D. Adventure-site variety rollout**
- Site placement pass: biome-appropriate distribution of the design §3.3 taxonomy;
  surface stamps (ruins, groves, castle exteriors, tower bases) override chunk
  terrain; interiors/underlevels are instanced maps via 7C.
- New family recipes on the 7B registry per §3.5: **sewers** (under every frontier
  town above hamlet tier — L-system mains from the street graph + braided laterals),
  **temple**, **tower**, **castle/fort**, **mine** (first zonal blend: tunneler grid
  breaking through into CA cavern), **surface ruins** (first decay-pass family),
  **forest depth** (first overworld-overlay family), **sunken grotto**.
- Blend machinery lands here: zonal assignment (Voronoi), modifier passes, seam
  transition rules, and the weighted per-family recipe tables (§3.4) — including
  rare cross-family discoveries (flooded catacombs, rooted towers).
- Aethertech facilities and sky islands intentionally later (M9 showpieces) — they
  ride the same graph core (teleporter/elevator edges already typed in 7B).
- *Exit:* a survey flight over ~200 frontier chunks finds ≥6 distinct site types;
  each new family passes the invariant self-test; castle escape-tunnel and tower
  collapsed-floor connections demonstrably traversable; screenshot sheet per family.

### Milestone 9 — Making It Matter (meta layer)

- **Atlas of Aldermere**: server-persisted discovery log (first-explorer credit) keyed
  on stable site IDs (D8); map UI reads it; site *type* recorded (first to chart a
  temple vs. a grotto).
- **Cartographer renown**: guild XP for charting hexes/sites; rides existing quest/XP.
- **Waystones & coordinate codes**: craftable bind-to-location items, `/navigate`
  sharing, tradeable through existing shops.
- **Land claims**: wilderness plots + basic structure placement in chunk deltas.
- **Showpiece sites**: Aethertech facilities (teleporter-pad "unusual paths") and sky
  islands (airship-gated) as the crown of the taxonomy.
- *Exit:* full loop — sail out, discover a temple, chart it, bind a waystone, sell it
  in Brasshollow, buyer navigates there and clears the hidden sanctum.

## 5. Explicitly Deferred / Cut

- True infinity (bounded, D2) · MMO-scale serving (D7).
- Dynamic price drift from actual trade volume (static seeded modifiers first).
- NPC dialog grammar engine (roadmap Phase 3) — the roster carries personality +
  dialog *seeds* now so the grammar engine can consume them later without a reroll.
- Weather/day-night sim effects (roadmap §2.3) — orthogonal; D10's calendar is the
  hook it will use.

## 6. Risks

| Risk | Mitigation |
|---|---|
| Frontier looks worse than hand-tuned Aldermere (biggest real risk) | Land roadmap Phase 2 (Wang autotiling) before/during 8B; preview tooling (7A/7B) forces art review pre-integration. |
| Jaquayed generation reads as *confusing* rather than rich at 16-bit top-down scale | Landmark invariant + minimap/auto-map support in 7C; playtest loop explicitly asks "could you find your way back?"; loop density tuned per family in data. |
| Graph→tile embedding fails for constrained families (towers, symmetric temples) | Embedder chosen per family; invariants validated on the *embedded* result, not just the abstract graph; deterministic salt-retry (D11) absorbs hard cases. |
| Blend seams read as incoherent (grid meeting cave, decor patch mismatch) | Zonal seams get explicit transition rules (§3.4); detail solves run constrained with macro tiles locked; every new recipe/blend entry gated on a reviewed preview sheet. |
| WFC contradiction stalls or nondeterminism | WFC is decoration-only, constrained, with seeded ordering and bounded backtracking (D12); contradiction falls back to salt-retry, worst case skips the decor pass — never blocks layout. |
| Seeded NPC/shop stack collides with hand-authored anchor content | Anchor settlements keep their static rosters/stock; the generalized systems treat static data as a fixed override of the same interfaces (mirrors D6 stitching). |
| Chunk/instance lifecycle breaks determinism | Generation is pure (D3); hashes cover entity state, not caches; self-tests add boundary-cross, fault/evict/refault, and instance-transition cases. |
| Sim refactor destabilizes M1–M6 systems | 8A gated on byte-identical pinned hashes; `MapId` proven in instances (7C) first. |
| Generator tuning churn invalidates player worlds | `GeneratorVersion` from day one (D4); anchor continent never regenerates. |
| Scope creep from meta layer | M9 strictly after engine work; every feature rides existing systems. |

## 7. Verification Strategy

- **Determinism**: same seed + inputs ⇒ identical `StateHash` across chunk streaming,
  instance transitions, shop restocks, and reconnects.
- **Golden seeds**: pinned hashes for reference-seed chunks, site graphs, settlement
  rosters, and day-0/day-7 shop stocks; intentional drift = version bump (D4).
- **Jaquays invariants**: structural self-test per site family (7B validator) run over
  many seeds — entrances, cycles, level links, secrets, chokepoints, landmarks —
  asserted on embedded tile output.
- **Recipe coverage**: every registered family recipe, modifier pass, and blend-table
  entry (incl. rare cross-family blends) is exercised and validated in self-test
  across reference seeds — no blend ships untested.
- **Coherence spot-checks**: generated contracts point at sites/monsters that actually
  exist; shop economies match surrounding biome; NPC schedules reference real tiles.
- **Playtest evidence per milestone**: screenshots (delve loop, sewer grates, frontier
  town roster, site-type survey sheet), server logs with hash lines, tick-time
  histogram during chunk faulting.
