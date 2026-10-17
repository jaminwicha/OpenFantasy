# OpenFantasy Beta (spec-v1_1) — Research Specification Pass (2026-10-17)

> **Pass Context:** This specification document integrates the empirical research pass conducted on October 17, 2026, mapping net-new user saves [`SR-238` through `SR-278`](file:///Users/alucinor/Workspace/Research/catalog/COMPLETE_158_CATALOG.md) to the systems of [`OpenFantasy Beta (spec-v1_1)`](file:///Users/alucinor/Workspace/Projects/OpenFantasy/Beta/spec-v1_1).  
> **Managing Agent Contract:** This document is an additive proposal in `spec-v1_1/`. The managing agent of `OpenFantasy` reviews these proposals against architectural invariants and decides if and how to ingest them into the implementation roadmap.

---

## 1. High-Leverage Research Mapping Matrix

| Research ID | Domain / Precedent | Key Technique | System Target in OpenFantasy Beta |
|---|---|---|---|
| **`SR-240` `[NEW]`** | `r/proceduralgeneration` (*Hydraulic Erosion Planets*) | C++/CUDA acceleration of planetary hydraulic erosion (10m down to 10s runtime) | **`GEOLOGICAL_WORLD_DESIGN.md` (`GEOR` / `RED-01`):** Fast GPU or vectorized SIMD heightmap erosion passes during macro world generation. |
| **`SR-245` `[NEW]`** | `r/gameenginedevs` (*BRUTAL Framework*) | Standalone pure C# engine architecture decoupled from commercial engines | **`TECHNICAL_DESIGN.md` / `OpenFantasy.Core`:** Enforcing 100% headless C# simulation authority with zero Godot engine coupling. |
| **`SR-258` `[NEW]`** | `r/hobbygamedev` (*Village Builder Auto-Battles*) | Autonomous village defense and self-resolving simulation battles | **`SettlementGen.cs` & `World.Settlements.cs`:** Off-screen settlement defense and raid resolution using deterministic statistical combat rolls. |
| **`SR-275` `[NEW]`** | `r/IndieDev` (*Red Skies at Dawn*) | First-person cozy cabin interior reverberation and hull acoustics | **`PRESENTATION_CONTRACTS.md` / `AudioDirection`:** Interior room reverberation filters attenuating exterior weather and rain. |

---

## 2. Accelerated Hydraulic Erosion Passes (`SR-240`)

### 2.1 Macro Orogeny & Hydrological Realism
Following `SR-240`:
- Simulating droplet-based hydraulic erosion (advection, sediment capacity, dissolution, deposition) over a 2048×2048 macro world map traditionally requires minutes of CPU computation.
- By structuring droplet simulation into parallel grid passes (using compute shaders in Godot or native SIMD in C#):
  $$\text{Capacity}(d) = K_{\text{capacity}} \times \|\mathbf{v}_d\| \times \text{Slope}(x, y)$$
- The world generator runs 200,000 droplets in $< 5\text{ seconds}$, carving realistic river catchments, alluvial fans, and sediment deltas that feed directly into `SettlementGen` fertile soil plots.

---

## 3. Decoupled Standalone C# Authority (`SR-245`)

### 3.1 Strict Separation of Core and Hosting
Per `SR-245` (*BRUTAL*) and ecosystem doctrine:
- `OpenFantasy.Core` maintains zero engine dependencies.
- `OpenFantasy.Hosting` runs headless simulations, CLI replay tools, and unit tests.
- `OpenFantasy.Client` (Godot C#) subscribes only to `WorldSnapshot` and `WorldStepBatch` events for sprite, particle, and audio presentation.
