# Ecology, fluids and living biomes

Research checked 8 September 2026. This is a proposed refinement for the preserved v1.1 specification, complementing [geological generation](geological-world-generation.md) and [submerged, subterranean and glacial worlds](submerged-subterranean-and-glacial-worlds.md). No reference binary, Beta runtime, ecological experiment or performance benchmark was executed. The six retained cases below are **unrun**.

**Recommendation:** build interacting, bounded processes over persistent material and population state. Water changes growing conditions; plants feed animals and spread seeds; animals migrate, graze and hunt; disturbance changes future establishment. Derive biome names and presentation from these facts. A magical influence adds explicit causes and effects. It should not secretly replace terrain, erase populations or make every creature hostile.

## 1. What the game evidence establishes

tModLoader's inspected stable documentation identifies itself as **v2026.07**. `LiquidData` exposes amount and liquid type; `Liquid` exposes update, synchronization and water/lava/honey/shimmer interaction surfaces. These establish typed cellular liquid state and distinct interactions, but API signatures do not establish Terraria's complete solver, conservation properties or performance. Its `SpreadInfectionToNearbyTile` explicitly supports infectious tile updates with range, progression conditions and protection checks. This is a practical precedent for rule-driven transformation with countermeasures, not evidence of biological succession.[^1][^2][^3]

Bay 12's historical *Dwarf Fortress Talk 11* is unusually explicit: grass carries type and a bounded quantity, grazing consumes it, and regrowth replenishes it. The developer distinguishes sparse/dense presentation and deliberately avoids individual grass-blade age. He also identifies unimplemented feeding and toxicity interactions, and worries that constantly animated uncanny grass overwhelms the screen. Those qualifications matter: this source supports resource-linked husbandry and selective detail, not a complete modern food web or universal animal nutrition simulation.[^4]

Rain World's developer site describes creatures hunting one another and a player learning those relationships through stealth. It also describes traditional animation combined with code to respond to surroundings. This supports legible interspecies interaction and responsive bodies. A promotional ecosystem description does not prove balanced energy accounting, hereditary evolution or persistent world-scale population simulation. Beta should retain the behavioral lesson without inheriting Rain World's compulsory urgency.[^5]

Factorio's 2024 fluid redesign supplies a useful counterexample. FFF-416 reports construction-order surprises and replaces local pipe flow with shared segments. FFF-430 reports that effectively unrestricted networks then weakened logistics and pump choices, motivating visible bounds and diagnostics. Both are historical development accounts, not a universal fluid prescription. Beta needs predictable limits that preserve interesting canals, reservoirs, ships and pumps; it does not need continent-wide instant equalization.[^6][^7]

## 2. A concrete game-grade fluid algorithm

Jānis Elsts' original 2009 cellular-fluid demonstration uses per-cell mass, an intermediate array and deliberately approximate compressibility to equalize connected vessels. It explicitly warns that direct updates introduce traversal-order artifacts. This is a small illustrative implementation, not a validated multiphase or cross-platform engine. Its floating-point thresholds and removal of negligible water cannot automatically become Beta's material accounting.[^8]

**Proposed Beta algorithm:** use the existing sparse three-dimensional cells, solid shapes, passage faces and compartment capacities. Store fixed-unit quantities per supported material phase, plus thermal state and conserved solute quantities where relevant. Water, brine, lava, oil, viscous honey and defined magical liquids obtain versioned profiles for mobility, permitted mixing, buoyancy ordering, hazard exposure and reactions. Different colors alone do not create different physical behavior.

For each scheduled fluid step:

1. Read one immutable revision of the affected cells, boundary reservoirs, geometry and profile rules. Wake only active interfaces and their required neighbors.
2. Propose transfers through open faces: gravity-driven descent, lateral equalization and any explicitly supported pressure/head response. Cap transfer by donor quantity, mobility and elapsed process exposure. Pumps additionally require capacity, direction and actual power.
3. Reconcile competing proposals against shared donor budgets and destination capacity, including multiple phases. Use a canonical allocation and remainder order; double buffering alone cannot prevent several donors overfilling one cell.
4. Commit equal donor debits and receiver credits. Evaluate defined reactions against that committed availability, then atomically publish material/temperature/solid-state changes with durable support, navigation, visibility and hazard invalidations.

This is a finite-volume-inspired cellular approximation, not a claim to solve Navier–Stokes. Qualify communicating vessels, narrow passages and pressure pockets explicitly; a gravity-only rule is insufficient to claim them. A visually sleeping puddle retains residual quantity. Rounding remnants stay in an account; they are not silently deleted or regenerated when a chunk loads. Reservoir exchange, rain, evaporation, vent supply and magical creation each declare their source/sink. An ocean boundary is a finite regional contract, not permission for every coast cell to manufacture water.

Material reactions require quantities and outputs: consume specified lava/water amounts, produce defined rock and vapor/heat consequences, and retain unreacted remainder. Mixing two unlike liquids is not universally “turn both into stone.” Unsupported combinations remain separated or use a declared inert mixture. Freeze/melt transfers conserve material across phases while the profile handles volume/capacity differences. Displacement that cannot fit schedules a real overflow or rejects an impossible construction; it cannot delete liquid.

Parallel workers may calculate proposals, but cannot publish independent contradictory edge transfers. A bounded queue preserves unfinished work across saves. Distant water uses compatible reservoir/compartment updates, with exactly one authority during promotion to visible cells. Rendering ripples are expendable; actual flood arrival and material quantities are not. Performance failure must trigger bounded work, visible preparation or a different approximation, not selective disappearance of dangerous water.

## 3. Lava, floating ice and glaciers are different processes

USGS describes rapidly formed lava crust insulating a still-hot interior; cooling depends strongly on thickness and environmental heat loss. Its estimates are specific to studied flows, and the article identifies extrapolation limits. Therefore Beta should track a simplified thermal reservoir and crust/solidification state, rather than changing orange liquid instantly into safe black pavement. Water contact can produce steam and partial cooling; rock composition and reaction outcomes belong to explicit fictional material profiles, not one universal real-world recipe.[^9]

Glaciers flow and deform over land, whereas floating sea-ice sheets drift under wind/current forces, collide and form ridges; some coastal ice remains attached. NSIDC documents these distinctions.[^10] Reuse the companion paper's glacier mass/extent and erosion/deposition contracts. Give floes persistent shape, thickness, buoyancy, drift, collision and load-bearing state; grounding or freezing attachment changes their motion rules. Ships can be blocked by ice, and a broken crossing can become drifting debris without pretending a glacier is low-viscosity water.

Use process-specific timescales: water can flood during an encounter, crops develop over appropriate exposed days, and regional forest/glacier changes accumulate over their declared historical intervals. The tentative 40-minute day is not authorization to compress an ice age into an unnoticed evening. Historical advancement and magical acceleration must carry explicit causes, costs and consent where applicable.

## 4. Seasonal plants with usable harvest decisions

The USA National Phenology Network identifies day length, temperature and rainfall as environmental cues for leafing, flowering and fruit maturation. Its 2026 growing-degree-day material explains accumulated temperature as a strong driver, not the sole universal clock. Different transitions and organisms can respond differently.[^11][^12]

**Proposed plant definition:** annual/perennial lifecycle; dormant, germinating, vegetative, flowering, fruiting and senescent stages as applicable; thermal accumulation, seasonal/photoperiod conditions, moisture/light/salinity suitability, maturity, harvest parts and regrowth rules. Crop designers configure a small useful subset for each species. Tea leaves, roots, herbs, fruit, seed and timber need different harvest effects. Repeated leaf picking can reduce reserves without killing a perennial; uprooting consumes the plant; leaving fruit can produce seed or feed wildlife.

Compute bounded development from elapsed exposure and a temperature response table, gated by viable conditions. Water consumption, growth, harvest and seed production share plant/cohort accounts. Regrowth is a process with resources, not a timer that duplicates harvested mass. The accounting model can abstract atmospheric carbon and sunlight as declared environmental inputs without simulating individual molecules. Plant death moves useful material into litter, wood or other defined pools.

Player forecasts should give qualified practical guidance—“ready in roughly two warm days,” “frost-sensitive,” “leave some leaves for recovery”—rather than exposing hidden future weather. Later botanical knowledge improves diagnosis and choices. Rain, irrigation, mulch, shade, storage and competent delegated care reduce repetitive chores. Native art distinguishes tender growth, maturity, harvestable parts and damage at play scale; seasonal recoloring alone does not prove usable farming.

## 5. How forests and deserts actually change

LANDIS-II supplies a stronger algorithmic foundation than biome-colored noise. The US Forest Service describes a grid of species age/biomass or density cohorts, with establishment, competition, growth, mortality and disturbances handled by process modules at appropriate time steps. Its Biomass Succession extension separately retains dead woody and leaf material. These are scientific landscape abstractions; their original scales and parameter sets do not directly calibrate an adventure game.[^13][^14]

LandR's documented `Biomass_core` offers distance/trait-dependent dispersal and explicitly reports that sequential species processing in its predecessor could make outcomes depend on input ordering. Its simultaneous competition/growth approach and separate disturbance ordering are particularly useful warnings. It also distinguishes dispersal frequency from annual growth. Do not copy its cohort merging or coarse time intervals where Beta has already established an individual tree's identity or a specific historical instant.[^15]

**Proposed ecological update sequence:** apply committed disturbance and hydrology; distribute seed/propagule budgets from mature sources; calculate establishment suitability; allocate shared light, space, moisture and nutrient budgets; commit growth, competition, grazing and mortality; then schedule migration and derive labels. Preserve event ordering and fixed-point rules. A seed kernel can combine common short-range dispersal with rarer longer-range arrival, biased by actual wind/water routes. Actual animals and cargo may carry persistent seed lots. USDA's seed manual documents wind, animal and water dispersal and distinguishes arrival from suitable germination.[^16]

A forest expands when propagules arrive, establish and survive—not because a neighboring cell says “forest.” Cohorts represent ordinary vegetation; encountered, owned, harvested, named or historically significant plants retain individual identities and are deducted from aggregate quantities. Materialization must never count the same tree twice. Seed banks permit recovery after a disturbance when viable remnants remain; clearing all sources and exhausting the bank changes recovery prospects. Remote arrival requires a funded source region or a recorded generation obligation, not an infinite immigration faucet.

Desertification is not contagious sand. USGS describes patchy degradation involving soil stability, water and vegetation; its long-term watershed study found persistent soil differences after historical grazing/drought and responses that varied by soil type. Returning rain therefore need not instantly restore the previous community. Conversely, a naturally dry desert is not automatically degraded land.[^17][^18]

Track temperature/moisture regime, soil depth and stability, salts, substrate, plant cover and disturbance history. Windblown sediment moves through geology's material ledger; reduced cover can expose soil to erosion, which reduces establishment. Restoration can require stabilizing soil, managing water and bringing viable plants. Use hysteresis and persistence windows when deriving “woodland,” “grassland,” “arid scrub” or “wetland,” so one storm does not toggle a whole region's name/music. Local dry pockets and wet refuges remain meaningful. This label scheme is a proposed readability measure, not an ecological law.

## 6. Living creatures, bounded adaptation and magical taint

Diet, hunger, energy, injury, perceived danger, territorial claims, offspring, pack relationships and learned familiarity should drive animal decisions. A neutral creature can flee, warn, defend or hunt according to context; predator and prey are relationships, not moral alignments. An animal attacking another animal uses actual pursuit, injury and consumption operations. Carcasses, grazing and available forage change shared resources. No invisible food replenishment should be required just because the player looks away.

For ordinary remote wildlife, bounded cohorts and migration obligations can reduce planning cost while preserving population/biomass totals and named individuals. Detailed decisions use local perception; knowledge of an unseen food patch needs memory, senses or a declared abstract search process. Population growth, starvation and dispersal must remain stable across detailed/coarse transitions. A region can suffer a real decline, but supported new starts still need the existing progression/recovery qualification. Do not force artificial equilibrium after every player action.

Minor inheritance is a small versioned trait vector—cold tolerance, drought tolerance, coat coloration, sensory acuity, maturation or yield modifiers—with bounded ranges and tradeoffs. Reproduction records parents/lineage and one deterministic inheritance draw; clonal spread preserves genotype unless an explicit mutation occurs. Separate temporary acclimation, injury, learned behavior and magic from inherited variation. Environmental selection changes surviving/reproducing frequencies; it does not grant every living creature the exact beneficial mutation it needs. Unlimited body-plan evolution and combinatorial new sprite species are outside this bounded mechanism.

Magical taint should be a distinct persistent influence with source, intensity, exposure, propagation route, susceptibility and removal rules. It may alter growth, toxins, fungal competition, perception or behavior through typed effects. A dark power can destroy seed viability, poison drinking water and drive hungry animals toward farms while resistant organisms persist. Sapient residents retain individual motives and choices. “Corrupted forest” must not imply universal evil ancestry or compulsory attack behavior.

Containment, cleansing and adaptation need different outcomes: removing a source stops its contribution; purifying water removes accounted contamination; restoring soil/seed stock takes work; cure does not resurrect a killed ecosystem or erase historical testimony. Healing and restoration magic may accelerate specified operations with real costs. Quiet color, sound and motion changes should communicate stages without repeating animated corruption sprites everywhere. This connects familiar places across epochs and creates opportunities for herbalists, hunters, farmers, caravan guards and researchers through the same world state.

## 7. Retained integration and acceptance cases

All cases retain seed, rule versions, input revisions, event order, material/population ledgers and expected outcomes. **They have not been run or passed.** Introduce real primitives during P01–P03; expand domestic breadth in P04, aquatic travel in P06, settlements in P07, social consequences in P08, narratives in P09 and historical/biome breadth in P10. Full-scale P12/P13 and later mobile qualification remain required.

| Case | Retained proof and failure criterion |
|---|---|
| ECOG-01 — conserved fluids | Run communicating vessels, stacked basins, pumps and three converging cross-chunk inflows, including unlike liquids. Vary worker/load order and crash mid-commit. Quantities, residuals, capacity, reactions and resumed flow must agree; duplication, disappearance or order-dependent outcomes fail. |
| ECOG-02 — heat and moving ice | Water partially cools a lava channel; crust remains hazardous until its declared thermal state permits crossing. Freeze/melt a loaded crossing and drift a floe against a ship; compare a glacier's distinct process. Verify conserved phases, support/nav consequences and interruption under Wait. |
| ECOG-03 — seasonal harvest | Grow annual crops, perennial tea and wild herbs through rain, drought, frost, grazing and partial harvest. Revisit after coarse scheduling, rest interruption and restart. Yields, seed viability and regrowth match accounting; human playtests assess forecasts, native readability and care burden. |
| ECOG-04 — changing communities | Realize the same forest edge and dryland in reversed chunk/history-query order. Apply clearing, seed transport, drought, overgrazing and restoration; inspect arbitrary interior dates. Cohorts, named plants, soil/sediment and derived labels remain consistent without invented seed sources or label flicker. |
| ECOG-05 — creature relationships | A neutral grazer feeds, a predator chooses between prey and risk, and a parent defends offspring. Change hunger, injury and local knowledge; move the group between detailed/coarse simulation. Consumption, deaths, migration and bounded offspring traits remain causal, persistent and reproducible. |
| ECOG-06 — tainted watershed | Corrupt a healthy forest upstream of farms and a town with four separated players. Trace contaminated water/food, displaced wildlife, NPC responses, containment and partial recovery across held encounters, unload/reload and historical forks. Native scenes must communicate actionable changes without visual overload or unavoidable maintenance spam. |

The remaining uncertainty is measured cost and enjoyable pacing, not permission to defer the primitives. Benchmark ordinary quiet regions, cold frontier realization and coupled flood/ecology/disturbance spikes. Retain meaningful ecosystems through approximation changes; do not certify them from a beautiful before/after image.

## Sources and evidence boundaries

[^1]: [tModLoader, LiquidData](https://docs.tmodloader.net/docs/stable/struct_liquid_data.html), inspected v2026.07 documentation.
[^2]: [tModLoader, Liquid API](https://docs.tmodloader.net/docs/stable/class_liquid.html), inspected v2026.07.
[^3]: [tModLoader, WorldGen infection utility](https://docs.tmodloader.net/docs/stable/class_world_gen.html), documented behavior, not a full proprietary source audit.
[^4]: [Bay 12, Dwarf Fortress Talk 11 transcript](https://www.bay12games.com/media/df_talk_11_transcript.html), historical development discussion; proposed work is not treated as shipped behavior.
[^5]: [Rain World developer site](https://www.rainworldgame.com/), original-game description and animation explanation; not a current expansion/platform inventory.
[^6]: [Factorio FFF-416, Fluids 2.0](https://www.factorio.com/blog/post/fff-416), 21 June 2024.
[^7]: [Factorio FFF-430, Drowning in Fluids](https://www.factorio.com/blog/post/fff-430), 27 September 2024.
[^8]: [Jānis Elsts, Simple Fluid Simulation With Cellular Automata](https://w-shadow.com/blog/2009/09/01/simple-fluid-simulation/), 1 September 2009; original illustrative algorithm/source.
[^9]: [USGS HVO, How do lava flows cool?](https://www.usgs.gov/observatories/hvo/news/volcano-watch-how-do-lava-flows-cool-and-how-long-does-it-take), 28 February 2019.
[^10]: [NSIDC, Science of Sea Ice](https://nsidc.org/learn/parts-cryosphere/sea-ice/science-sea-ice), indexed full-text scientific explanation; direct reopen failed during this pass.
[^11]: [USA-NPN, Why phenology?](https://www.usanpn.org/about/phenology).
[^12]: [USA-NPN, Accumulated Growing Degree Day and Spring Index Maps](https://www.usanpn.org/files/npn/reports/usa-npn_agdd-and-six_2026.pdf), January 2026; indexed document text.
[^13]: [US Forest Service, LANDIS](https://research.fs.usda.gov/nrs/products/dataandtools/landis-landscape-disturbance-and-succession-model), 15 April 2024.
[^14]: [LANDIS-II Foundation, Biomass Succession](https://landis-ii-foundation.github.io/Extension-Biomass-Succession/), inspected page lists 7.2.1, June 2026.
[^15]: [PredictiveEcology, Biomass_core documentation](https://github.com/PredictiveEcology/Biomass_core), particularly simulation flow and algorithm differences; the repository describes its lineage and differs from current LANDIS-II. No model run was performed.
[^16]: [USDA, The Woody Plant Seed Manual](https://www.fs.usda.gov/rm/pubs_series/wo/wo_ah727.pdf), dispersal/dormancy discussion from indexed text; not a complete manual review.
[^17]: [USGS, Desertification](https://pubs.usgs.gov/gip/deserts/desertification/), historical general explanation used for mechanisms, not present-day statistics.
[^18]: [USGS, Long-term ungrazed and grazed watersheds](https://www.usgs.gov/news/insights-long-term-ungrazed-and-grazed-watersheds-western-drylands), research report on specific dryland sites; no universal recovery times inferred.
