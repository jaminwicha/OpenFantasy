# Fluids, living ecologies and changing biomes

## Intended simulation

Water, lava, other liquids, moving ice, plants and creatures participate in one evolving world. Ecology changes through seasons, food, dispersal, disturbance, institutions and magic, just as society changes through its people and resources. This extends [geological history](GEOLOGICAL_WORLD_DESIGN.md) with concrete game-scale algorithms; it is not a promise of full computational fluid dynamics or an unrestricted evolution game.

Terraria is a reference for understandable fluid interaction and player-shaped terrain, not evidence that its rules already implement this entire ecology/history model. The [supporting research](../research-v0_9/ecology-fluids-and-living-biomes.md) distinguishes observed/documented game mechanisms, procedural methods and Beta's original design. [Submerged/underground research](../research-v0_9/submerged-subterranean-and-glacial-worlds.md) grounds the separate treatment of water, aquifers, caves and ice.

## Liquid and phase profiles

All supported liquids share the same transfer authority. A material profile defines canonical mass/volume relation, density ordering, flow/viscosity limits, supported pressure/head behavior, thermal properties, miscibility group, dissolved/suspended constituents, and allowed reactions/phase changes. Quantities and remainders use checked integers/rationals. Every shipping liquid needs explicit rules and accepted feedback; adding a colored texture does not create a new simulated substance.

| Family | Required behavior | Deliberate limit |
|---|---|---|
| Water and dissolved constituents | Fall, spread, pool, drain, infiltrate supported material, evaporate/freeze/melt, carry declared sediment/solute and affect plants/creatures | Salinity, nutrients or contamination use explicit concentrations/properties, not arbitrary unbounded chemistry |
| Lava/magma | Source-budgeted hot flow, profile viscosity, heat exchange, declared gas/reaction products and cooling into persistent solid material | No universal thermodynamic solver; contact reactions specify actual reactants, yields and heat/pressure consequences |
| Other liquids | Profile-specific flow, layering or mixing, wetness/hazard/use and supported reactions; examples may include oils, acids, alchemical mixtures and magical liquids | Only catalogued compatibility/reaction rules execute; unknown combinations cannot silently turn into water or vanish |
| Ice, snow and glaciers | Stored solid mass, accumulation/ablation, terrain/support occupancy; glaciers move/deform slowly and erode/deposit under the geological process profile | A glacier is not water with a blue texture or a whole mountain rebuilt every frame |
| Floating ice | Buoyant solid pieces or declared cover with mass, drift, contacts, melting and obstruction behavior | Ice floes use moving-solid/assembly interaction; do not invoke a global ocean rigid-body simulation |
| Steam and trapped air | Bounded atmosphere stores, exposure, pressure/ventilation or vent-to-reservoir rules where relevant; coherent sealed/flooding chambers | Local hazard and enclosure models rather than unrestricted atmospheric CFD |

Freezing or cooling can change volume because material density changes. Reserve resulting occupancy/displacement before committing the transformation. Water displaced by a growing ice sheet and material expelled by lava cannot be deleted. Heat removal, light, magical sources or external regional fluxes have named owners and limits. Conservation refers to the supported material/source ledger; external atmosphere/mantle forcing is explicit rather than falsely described as a closed physical planet.

Mixing does not necessarily make one homogeneous liquid. Compatible constituents combine with quantity-weighted properties; immiscible layers use declared density/contact behavior. Reactions consume reserved reactants once and produce their declared outputs. Retain source claims through mixing, sedimentation, cooling and reuse, including branches. Unsupported chemistry is an explicit limitation, not an opportunity to mint resources.

## A deterministic local flow algorithm

The first retained solver extends the existing finite-volume cellular rule. Detailed flow reads one committed state and proposes bounded neighbor transfers. Downward movement precedes horizontal equalization; supported upward pressure/pump flows have an explicit later phase. Profiles bound amount per face and step. Stable source/destination ordering and retained fractional remainders resolve contention. Test directional bias and tune the profile openly; order must never depend on frame or worker timing.

```text
for each canonical fluid step:
    read committed cells, reservoirs, material profiles and boundary revisions
    prepare due source/sink and thermal operations with explicit budgets
    propose legal face transfers from starting volumes and head differences
    allocate transfers by phase and stable edge order
        reserve source quantity and destination starting capacity
        do not spend this step's promised inflow or uncommitted outflow capacity
    propose bounded mixing/reaction/phase changes against reserved state
    validate occupancy, material/heat accounting and encounter admission
    commit the legal stage, source claims, invalidations and durable continuation
```

Separate transfer and reaction stages have explicit canonical order. A reaction may become eligible in the next stage after inflow; it cannot see a renderer's interpolated mixture. A water/lava contact tests exhaustion, blocked steam/solid output, changing void capacity and concurrent mining. A pump consumes its declared power/work and transfers real liquid; it does not materialize a new source at its outlet.

Use active cells/faces and sleeping unchanged regions instead of updating every cell. A change in a border, source, level, heat or enclosure wakes dependent work. Far water bodies and aquifers use reservoir/head/boundary models that conserve the same owned quantities. Detailed activation allocates from those reservoirs and reproduces their established topology; demotion retains actual modifications. Approaching a lake cannot refill it or change a known flood outcome.

The detailed solver propagates effects through finite causal fronts. Coarse regions expose finite boundary fluxes and travel/arrival rules; one ocean connection does not require visiting the whole ocean in one tick. Do not interchange a detailed and aggregate resolver based on camera distance after outcomes have become consequential: pin the resolver/representation contract and retain its conversion evidence. Overload delays dependent authority or uses the defined recoverable hold; it cannot skip selected floods or spend water twice.

## Plant growth, seasons and harvest

Plant profiles define life form, germination conditions, growth stages, thermal/light and seasonal suitability, moisture/nutrient tolerance, dormancy, flowering/pollination/fruiting, harvest parts, regrowth, lifespan and seed production. Annual crops, perennial plants, trees, fungi and explicit magical flora have different schedules. A ripe fruit, root, seed and standing tree are distinct harvest opportunities with distinct effects on survival and next growth.

Accumulate growth from actual exposure, for example a versioned thermal-suitability rate multiplied by water, nutrient and light factors over elapsed time. Keep integer fractional progress. Integrate only to the next weather, water, care, damage, stage or season transition; then re-evaluate. A threshold commits one phase change or harvest cycle with stable identity. A big offline interval cannot skip drought mortality, collect every harvest automatically or mature a tree merely because a region loads.

The calendar has the selected roughly40-minute full day/night target, but plants do not all mature in a day or obey one universal season. World/species profiles specify meaningful multi-day crop and longer ecological times. Dormancy or lack of water can stop growth while some stress continues under the same exposure policy. Irrigation, shade, greenhouse-like shelter, pruning, fertilizer and declared magic alter known conditions through real actions and resources.

Vegetation may use persistent cohorts or resource patches for unindividuated cover. Each cohort retains species/trait band, age/stage distribution, biomass/resource allocation, seed store, condition and provenance. Named or individually modified plants—an inherited orchard tree, a carved trunk, a cultivated specimen—retain individual identity and changes. A plant does not become a different specimen on promotion; converting cohort allocation to an individual reserves and removes its exact share.

Harvest debits the correct part/cycle and creates source-accounted lots. Grazing, foraging, fire and commercial harvest compete for that same vegetation. Regrowth and reproduction are scheduled physiological processes with declared environmental inputs and output limits; a texture refresh is never a fresh resource roll. Farming, teas, cooking, breeding, forestry and caravan seed trade use these same records.

## Food webs and creature behavior

“Animal,” “monster” and a faction label do not automatically specify hostility. Each applicable actor has diet/resource preferences, hunger, fear, territory, temperament, social bonds, perception and learned experience. An herbivore may graze and flee; a predator may hunt another animal; a territorial creature may warn before attacking; a satiated dangerous creature may ignore the player. Magical influence can change specific dispositions or perceptions through declared effects, without making an entire species innately evil.

Predator/prey selection is a feasible action choice using the actor's available evidence, capabilities, expected food value, risk and obligations. NPC planners cannot inspect hidden prey across a mountain because the ecology module knows its ID. Predation, defense, carcass handling, scavenging, taming, hunting and care execute the same actions, damage, custody and remains rules used elsewhere. Two hunters cannot each consume a whole copy of the same carcass.

Represent producer, herbivore, predator, scavenger and decomposer/resource-return roles through supported species/material processes. Carrying pressure, competition, disease or disturbance may affect condition and reproduction, but do not require a complete biological textbook model. Local extinctions, migration and recolonization are possible; they must follow causes rather than a hidden replenishment script. Supported starting profiles validate accessible food/learning/recovery routes, and later world changes revalidate them honestly.

Distant realized creatures retain player-equivalent applicable individual state. Routine travel, grazing or resting may be batched only until an interrupt changes the outcome. Any aggregate encounter resolves committed participants, supplies and casualties under a pinned rule; it must not mint replacements as the camera approaches. Uninstantiated background populations remain constrained generation potential until realized, not a second population secretly competing with the saved actors for the same food.

## Dispersal, succession and biome spread

Biome labels summarize continuous conditions and actual ecological composition. Store climate forcing, local heat/light, soil/substrate, water, salinity, disturbance, magical influence and plant/animal state independently. Forest, desert, wetland, steppe, tundra or a tainted woodland is a derived classification with transition thresholds and hysteresis. Relabelling a region cannot instantly add mature trees, remove residents or change inventory.

The procedural ecological update is an explicit dependency schedule:

```text
at each canonical ecological interval or interrupt:
    obtain committed geology, weather, water/soil and magical conditions
    advance existing plant/cohort growth and condition to the next threshold
    reserve feasible producer/consumer actions and resolve their real effects
    advance reproduction, bounded trait inheritance and actual seed stores
    propose dispersal/migration through known physical boundaries and carriers
    reserve source shares and destination capacity; commit arrivals/establishment
    update cover, age structure, habitat quality and derived biome classification
    emit observable changes and revalidate affected routes/opportunities
```

Use double-buffered environmental proposals or a declared sequential phase order so parallel workers cannot give one neighbor a free extra generation of growth. Suitability alone does not produce a forest: viable propagules must arrive, establish and survive the required growth stages. Seed kernels may use bounded distance/direction distributions derived from wind and terrain; rivers, animal carriers and caravans use actual routes and capacity. Long-distance colonization is a recorded rare dispersal event with an accountable source, not an arbitrary change on discovery.

Succession rules provide the mechanisms for spread. Pioneer plants can establish after disturbance, modify soil/cover and enable shrubs or later canopy where conditions and seed sources permit. Browsing, harvest, fire, drought or salinity may prevent that progression or reverse it. Desertification emerges through sustained water/soil/cover imbalance under the profile; a desert does not spread solely because neighboring cells have the word “desert.” Reforestation needs suitable conditions, surviving or introduced seed stock, establishment time and protection from continued pressure.

Biome feedback may alter bounded evapotranspiration, shade, infiltration, fuel or soil retention in the next declared environmental step. It cannot retroactively change the rainfall that already produced a recorded harvest. Hydrology and atmosphere boundary budgets reconcile those exchanges. Underwater and subterranean habitats use their actual light, chemistry/temperature, connectivity and available energy/resource sources; they do not reuse surface grass rules blindly.

## Minor adaptation and magical transformation

Keep four separate mechanisms: reversible acclimation to current exposure; individual learned behavior; bounded inherited variation; and explicit magical transformation. A cold season can affect acclimation without rewriting every descendant. A learned avoidance of a caravan differs from a heritable temperament tendency. Effects applied by a source can end while their physical/ecological consequences remain.

Bounded ongoing adaptation does not remove the broader generated prehistory of species, subspecies, divergence/convergence and cosmic affinities. Those histories select and connect supported lineage/body/capability families under versioned rules. The restriction concerns unbounded live genome/body invention, not the required breadth of authored and generated fantasy peoples.

Species profiles enumerate supported trait ranges and tradeoffs, such as modest cold/drought tolerance, flowering window, seed dispersal behavior, coat/pigment variants, diet tolerance or magical sensitivity. Birth/seed events derive inheritance and small permitted variation from parent/lineage and stable event RNG. Survival and reproduction may change trait frequencies over generations, but no arbitrary new organ, animation family, intelligence system or power is generated without supported definitions. Mutation and selection are bounded game mechanisms, not open-ended genome simulation or inevitable stat inflation.

Magical fields have sources, carriers, intensity, extent, persistence, resistance and decay/cleansing rules. A hostile tower might alter local water/soil, stress trees, favor certain organisms and amplify fear or aggression through declared effects. Wind, water, creatures or carried materials spread only the supported component; corruption does not jump everywhere because a polity changed allegiance. Different actors and species respond differently. Private motives and ethics remain separate from the environmental hazard.

Destroying the source stops its supported emissions; it does not instantly restore dead trees, remove contaminated sediment, undo learned fear or rebuild displaced communities. Recovery can require containment, water treatment, removal of a reservoir, reseeding, changing grazing pressure, healing and time. Some adaptations may persist until replaced across generations. Player and NPC actions can accelerate, redirect or exploit these processes through actual capabilities. Reconstruct their causes in historical visits, retaining affected plant lineages, species variants, routes and settlements.

## Clocks, history and native feedback

Environment, growth, predation, reproduction and decay use declared authoritative exposure and event schedules. Held encounter dependencies cannot suffer wall-time suffocation, lava burns or hunger; a held garden cannot produce free harvests on another clock. Admission includes relevant air/water/heat/terrain and creature interactions before consequences. Normal regions continue under the selected split-party stasis policy, with boundary exchanges reserved and suspension history retained. Pending inflow remains in finite physical storage under the geological backpressure contract; a full source/receiver causes legal overflow or a wider hold, never invisible infinite storage. Each stage revalidates changed dams, enclosures, source budgets and occupancy before committing, so live intervention changes future propagation.

Historical reconstruction includes water/ice and ecological state, not only the land shape. Querying before and after a flood, harvest, dispersal or corruption event must recover the appropriate plant stages, populations, source stocks and beliefs. An intervention branch recomputes dependent succession, migrations and crops from the changed conditions; it does not replay an incompatible parent outcome. Coarse interval partitioning, save/restart and alternate query order must preserve the selected model's results.

Readable sprite growth stages, fruit/seed/leaf changes, restrained stress indicators, grazing/hunting/scavenging actions, warning/flight behavior, moving liquid/ice, and condition-sensitive sound make these processes understandable. Dangerous does not always look evil; a neutral creature's warning or retreat must be distinct from attack commitment. Experts can inspect learned causes and conditional forecasts; basic air, heat, unstable ice and known toxicity cues remain accessible. Native quality still requires separate Sea of Stars finish and Stardew-like responsive interaction acceptance.

## Retained proofs and scope

P00 pins liquid/phase/reaction and ecological species/cohort/trait/forcing profiles. P01 retains conservation, scheduling, identity and historical contracts. P02 presents one real watercourse, seasonal crop/vegetation patch and food interaction involving non-hostile and predatory creatures near original content. P03 validates fluid reaction/phase change, distant-versus-detailed ownership, exact-date ecological history, branch intervention and Wait boundaries before broad expansion.

P04 expands seasonal farming, food webs, dispersal and disturbance; P06 covers aquatic/ice travel and frontier transitions; P07 adds underwater/underdark settlements; P08/P09 integrate migration, trade and magical environmental narratives; P10 completes long-horizon biome/trait/geological history; P11/P12/P13 close catalog, native quality, endurance and desktop operation. P14 retains mobile validation. Candidate C#/Rust workload comparisons include these retained operations; added scope alone is not evidence that a language switch is needed.

ECOG-01–06 in the research dossier and the revision validation matrix define required counterexamples. They are unrun. Performance qualification must measure active fronts, habitat/cohort counts, realized individual work, reaction/claim fragmentation, historical growth and backlog under four separated players. A finite active workload can be bounded; conserved effects, saved individuals and required ecological history cannot simply be dropped to meet a frame budget.
