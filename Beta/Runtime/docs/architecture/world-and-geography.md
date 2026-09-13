# World and geography

## Addresses and physical space

World, branch, realm and signed three-dimensional coordinates identify a place. Floors and subfloors
share a realm's Z axis; shadow worlds, dreams and other planes have separate realm identities.
Underwater and underground locations are physical spaces with material, access and environmental
requirements, not interchangeable screen layers. Historical time adds another address coordinate.

## Domain contracts: local terrain and navigation

[StratifiedTerrainGenerator](../../src/OpenFantasy.Core/Terrain/StratifiedTerrainGenerator.cs)
derives integer terrain from pinned rules, seed and world/realm scope. Its genesis derivation excludes
branch identity so common-ancestor baseline material retains ancestry across forks. Stable formation
and source identities survive excavation; changing a cell does not assign a new origin to its material.

[TerrainRegionBounds](../../src/OpenFantasy.Core/Terrain/TerrainRegionBounds.cs) admits up to
32 × 32 × 16 cells, canonically indexed by Z, Y, then X. A
[TerrainRegionSnapshot](../../src/OpenFantasy.Core/Terrain/TerrainRegionSnapshot.cs) is immutable.
Its excavation operation validates an expected revision and at most 256 unique solid cells before
producing a candidate. This terrain operation alone does not authorize mining labor, award loot,
reserve mineral rights or adjudicate every consequence of removing support.

[TerrainRegionCodecV1](../../src/OpenFantasy.Core/Terrain/TerrainRegionCodecV1.cs) stores pinned
generation input and ordered excavation indices, regenerates that exact baseline and applies edits.
It is not a general geological event log. Old interpretations remain pinned.

[LocalSpatialSnapshot](../../src/OpenFantasy.Core/Spatial/LocalSpatialSnapshot.cs) composes at most
16 nonoverlapping regions and 64 actors with exact common scope and rule pins. Movement uses integer
body clearance, supporting terrain, occupied bodies and installed structures. Cardinal movement
preserves feet elevation; it does not imply arbitrary climbing. Local interaction checks equal feet
elevation, bounded centre reach and a clear segment. The
[ground route search](../../src/OpenFantasy.Core/Spatial/GroundRouteSearch.cs) and body checks
support legal local routing, not an unrestricted global pathfinder.

## Runtime binding: immutable watershed preparation

[WatershedPlanner](../../src/OpenFantasy.Core/Geography/WatershedPlanner.cs) accepts an explicit
world/branch/realm, seed, rules, finite bounds, one boundary outlet and an external boundary identity.
Bounds are at most 64 × 64 columns. The planner assigns natural elevations, then performs a
deterministic priority flood using spill elevation and canonical index for ties. Drainage ranks
resolve flats without cycles. Reverse accumulation counts contributing columns.

[PreparedWatershed](../../src/OpenFantasy.Core/Geography/PreparedWatershed.cs) retains natural and
spill heights, downstream indices, ranks, contribution counts, substrate/formation identities and
internal faces. Opposite views of an internal face share its identity and facts. Window reads require
complete containment and cannot grow the preparation. Spill height is a routing result; it does not
raise terrain or create a lake. Contributing columns are not water volume, flow rate or stored liquid.

[WatershedCodecV1](../../src/OpenFantasy.Core/Geography/WatershedCodecV1.cs) bounds input, recomputes
the pinned plan and requires exact canonical agreement. Its maximum plan contains 4,096 columns and
8,064 internal faces. [WorldWatershedAdmission](../../src/OpenFantasy.Core/Worlds/WorldWatershedAdmission.cs)
retains one plan in a fresh profile-six construction world and checks role collisions across the
finite admitted root and its dependency inventories.
The ordinary world step preserves those facts; the local terrain generator does not consume this
preparation. Internal face consistency does not prove neighbouring-basin seams or water custody.

## Design contract: finite preparation and frontier streaming

An almost infinite-feeling frontier is generated through bounded preparation, not an unbounded
recursive request. Macro provinces establish compatible geography before local terrain realization.
Upstream and downstream dependencies form a finite acyclic graph, or a declared finite reservoir
component with explicit storage and boundary behavior. Common-ancestor addresses determine shared
faces independently of branch, loading order and worker order. Candidate basins cannot silently
change accepted drainage when their neighbours load.

Residency, simulation detail, story geography and encounter timing are separate concerns. Streaming
loads projections and detailed representations of persistent facts. Unloading preserves individual
identities, inventories, commitments, terrain changes and material custody. Coarse simulation has
declared exact boundaries and deterministic continuation; activation cannot invent a richer person
or regenerate a consumed resource. Predictive loading accounts for speed, turning, ships, airships,
teleportation and spread-out players, with bounded preparation and visible backpressure.

## Design contract: geological history, liquids and ecology

Continental movement, uplift, volcanic activity, erosion, shifting rivers, floods, earthquakes,
glaciers and sea-level change operate through versioned stages. Established causes and committed
stages are immutable; interventions revalidate uncommitted propagation. Geological feature and
material ancestry persists through a drowned town, revealed cavern or eroded mountain. Historical
reconstruction includes inhabitants, claims and ecology affected by those changes.

Water, lava and other liquids have typed quantities, source capacity, receiving capacity and
material-specific reactions. Cross-scale flux debits and credits real owners. A reservation buffer
cannot create invisible storage, an infinite outlet or deleted overflow. Ice transport, freezing,
melting, sediment and erosion obey declared conservation/transition rules. Expensive motion can use
bounded coarse models, but a visual flow never authorizes a separate physical transfer.

Plants and cultivated crops share growth, seasonal requirements, soil, water, harvest and provenance.
Animals and monsters have diets, needs, habitats and predator/prey relationships; hostility is an
actor decision rather than a universal creature flag. Bounded trait variation and dispersal alter
populations without requiring unrestricted evolutionary simulation. Forest/desert boundaries and
magical taint change through explicit environmental causes, exposure and ecological response.

Mining, tunneling, building, pumping and enchantment are interventions in these same processes.
An earthquake opening a cave can affect drainage, travel, resources, property and narrative
opportunities together. Support, hazards and Wait admission are validated before effects reach actors.
The authoritative design details reside in
[geological history](../../../spec-v1_1/GEOLOGICAL_WORLD_DESIGN.md) and
[ecology and fluids](../../../spec-v1_1/ECOLOGY_AND_FLUIDS.md).
