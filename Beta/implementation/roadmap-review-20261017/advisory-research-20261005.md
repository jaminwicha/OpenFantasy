# OpenFantasy Beta (spec-v1_1) — Research Specification Pass (2026-10-05)

> **Pass Context:** This document captures the empirical research pass conducted on October 5, 2026, integrating findings from the 236 curated research saves in [`~/Workspace/Research`](file:///Users/alucinor/Workspace/Research) (specifically `SR-158` through `SR-236`) into concrete specification addenda for **OpenFantasy Beta (`spec-v1_1`)**.  
> This file is an additive specification pass; it leaves existing spec files (`GDD.md`, `TECHNICAL_DESIGN.md`, `GEOLOGICAL_WORLD_DESIGN.md`, `ECOLOGY_AND_FLUIDS.md`, `PRESENTATION_CONTRACTS.md`) intact.

---

## 1. Executive Research Mapping Matrix

| Research ID | Domain / Precedent | Key Technical Finding | OpenFantasy Beta Subsystem & Requirement Impact |
|---|---|---|---|
| **`SR-236` `[NEW]`** | `r/godot` (2026-10-04) | Godot 4 spatial & screen-space shaders: foliage wind-sway, vertex displacement, water refraction, depth fog | **`PRESENTATION_CONTRACTS.md` / `Aldermere` Presentation:** Pure presentation-layer shaders driven by simulation read-only parameters; zero tick coupling. |
| **`SR-214` `[NEW]`** | `r/proceduralgeneration` (*TropoPlate*) | Space-to-surface transition with dynamic plate tectonic faultlines and orogenic crustal deformation | **`GEOLOGICAL_WORLD_DESIGN.md` (`GEOR` / `RED-01`):** Faultline-anchored continental boundary generation and rain-shadow macro basins. |
| **`SR-191` `[NEW]`** | `r/proceduralgeneration` (*3-Billion-Year Tectonics*) | Multi-epoch crustal heat, continental collision, and mineral vein extrusion | **`GEOLOGICAL_WORLD_DESIGN.md`:** Deep-time metallogenic belt placement for mining sites and settlement resource priors. |
| **`SR-162`, `SR-179`, `SR-182`** | `r/proceduralgeneration` (*Nagomi Blue*, *Procedural Koi*) | Localized, heritable flocking/boids in aquatic biomes with obstacle collision avoidance | **`ECOLOGY_AND_FLUIDS.md` (`ECOG`):** River and coastal school entities driven by deterministic carrying-capacity budgets. |
| **`SR-020`, `SR-027`, `SR-127`** | *Lootdeck* & *Baldur's Gate 3 Chokepoint Tactics* | Turn-based room geometry manipulation, forced displacement (shove/hook/swap) into hazards | **`SiteGenerator.cs` / `TempleEmbedder.cs`:** Site battle arena topology featuring active slag, water, and spike chokepoints. |
| **`SR-078`, `SR-158`** | *Graph-First Dungeons* & *Multi-Phasic Map Generation* | Multi-phasic capability gating and shortcut-loop reinsertion (Jaquayed topology) | **`SiteValidator.cs`:** Verification that temple and ruin layouts maintain cyclic connectivity with capability-gated detours $\ge 12$ steps. |
| **`SR-204`, `SR-181`** | *Argosy* & *Komachi* | Painted/paper aesthetic with relaxed town daily schedules and footpaths | **`SettlementGen.cs` & `World.Settlements.cs`:** Organic desire-path road accretion and daytime schedule-state resolution. |

---

## 2. Presentation Layer Shaders (`SR-236`) — Zero Simulation Mutation

### 2.1 Doctrine Alignment
Per Global Game-Dev Doctrine, presentation polish reads gameplay snapshots but **never** mutates authoritative simulation state. Godot 4 spatial and screen-space shaders provide atmospheric depth without desynchronizing the deterministic C# simulation.

### 2.2 Shader Specification Contracts
1. **Foliage Wind Sway (`res://assets/shaders/nature/foliage_sway.gdshader`):**
   - **Inputs:** World time scalar `u_sim_tick_progress` (interpolated render tick), wind vector `vec2 u_wind_dir` read from the current chunk's `AtmosphereSnapshot`.
   - **Mechanism:** Vertex displacement scaled by vertex color green channel (weight factor):
     ```glsl
     VERTEX.xz += sin((VERTEX.y + TIME * u_wind_speed) * 2.0) * u_wind_dir * COLOR.g * 0.08;
     ```
   - **Simulation Isolation:** The underlying tile passability and collision mask remain fixed integers in `WorldGrid`.

2. **Hydrological Water Refraction & Depth Color (`res://assets/shaders/water/water_surface.gdshader`):**
   - **Inputs:** Water depth scalar read from `WaterTile.depth_class` (Shallow, Deep, Torrential), sediment load float (`SedimentPpm`).
   - **Visual Output:** Screen-space refraction with depth-based color absorption from crystal turquoise to silt brown.

3. **Depth Fog & Retro Silhouette (`res://assets/shaders/post/atmosphere_fog.gdshader`):**
   - Full-screen post-processing quad sampling `SCREEN_TEXTURE` and `DEPTH_TEXTURE`.
   - Projects distance-attenuated atmospheric haze matching the current biome's ambient luminance.

---

## 3. Geological Faultline & Metallogenic Veins (`SR-214`, `SR-191`)

### 3.1 Continental Plate Tectonic Invariants (`GEOR-ADD-01`)
- Continental plates are simulated as convex spherical polygons initialized from the world seed `WorldSeed`.
- At plate convergence zones:
  - **Oceanic-Continental:** Generates volcanic coastal cordilleras with high mineral concentration (`Iron`, `Copper`, `Obsidian`).
  - **Continental-Continental:** Generates high-altitude orogenic plateaus with severe rain shadows (arid leeward basins, humid windward forests).
- At plate divergence zones:
  - Generates rift valleys with hydrothermal springs and fertile volcanic ash soil.

### 3.2 Deep-Time Metallogeny (`GEOR-ADD-02`)
Mineral deposits are not randomly scattered per tile; they follow linear orogenic belts derived from plate boundary intersections. Mines, quarries, and dwarven settlements generated in `SettlementGen` must cluster within $N$ tiles of these verified geological fractures.

---

## 4. Tactical Arena Room Geometry & Forced Movement (`SR-020`, `SR-127`)

### 4.1 Battle Room Topology (`SUB-ARENA-01`)
In-situ and encounter arenas generated by `SiteGenerator.cs` must include active surface hazards:
1. **Conduit Trenches / Slag Vats:** Conductive or burning tiles dealing tick damage.
2. **Elevation Ledges:** Height differentials that grant $+20\%$ hit mitigation from high ground and inflict falling impact damage on forced displacement.
3. **Chokepoint Pillars:** Destructible or solid pillars that break line-of-sight and funnel enemy pursuit into predictable tiles.

### 4.2 Displacement Verbs (`COMBAT-DISP-01`)
All combat skills must specify an integer `KnockbackTiles` vector:
- If a displaced target hits an obstacle, the target takes `ImpactDamage = RemainingTiles * ArmorFactor`.
- If a displaced target lands on a hazard tile, the hazard's effect resolves immediately.

---

## 5. Settlement Desire Paths & Multi-Era Evolution (`SR-204`, `SR-181`, `SR-146`)

### 5.1 Organic Path Accretion (`SETTLE-PATH-01`)
In `World.Settlements.cs`:
- Every actor trip between home, workplace, well, and market records a step count on a persistent tile transit grid.
- Tiles exceeding a traversal threshold transition from `TerrainType::Grass` to `TerrainType::DirtTrack` to `TerrainType::CobbleRoad`.
- This ensures ancient footpaths define modern settlement street layout, directly realizing the *Paleolithic-to-Metropolis* paradigm (`NP-03`).

---

## 6. Verification & Automated Acceptance Tests

1. **Shader Invariance Gate:** Headless test verifying that compiling, activating, or toggling Godot spatial shaders produces zero state changes in `WorldSnapshot` or `WorldStepBatch`.
2. **Geological Belt Test:** Unit test asserting that $100\%$ of generated ore veins lie within distance $D \le 4$ of a tectonic convergence line.
3. **Cyclic Dungeon Loop Test:** Test iterating 100 generated temple sites to guarantee that BFS detour secret doors provide non-load-bearing alternate paths $\ge 12$ steps.
