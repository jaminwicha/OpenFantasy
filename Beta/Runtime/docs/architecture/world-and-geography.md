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

## Domain contract: finite watershed networks

[WatershedNetworkPlanner](../../src/OpenFantasy.Core/Geography/WatershedNetworkPlanner.cs) composes
one through sixteen immutable basins under an explicit province ancestry identity. Members have
the same world, branch, realm and seed. Scope roots and all admitted province, basin, formation,
substrate, internal-face, outlet and composed-face roles are collision-checked. Columns cannot
overlap. The composition does not mutate member bytes or their basin-local contribution counts.

Each [outlet closure](../../src/OpenFantasy.Core/Geography/WatershedOutletClosure.cs) names either
a receiving boundary column in a different basin or a terminal with finite contributing-column
capacity. A link must be cardinally adjacent to the source outlet, and the receiver's spill route
cannot exceed the source outlet's natural elevation. Missing, duplicate, unknown or cyclic closures
reject the whole candidate. A low depression cannot bypass a higher downstream spill route.

Composition orders basins by their dependency graph. Incoming counts enter the exact receiving
column before local downstream accumulation. The
[prepared network](../../src/OpenFantasy.Core/Geography/PreparedWatershedNetwork.cs) exposes separate
composed counts; terminal totals equal the number of network columns exactly once, and each total
must fit its declared capacity. These are finite topology witnesses, not water volume, discharge,
storage, rainfall budgets or evidence of a physically realized ocean outlet.

Every touching cross-basin face has one identity derived from world, realm, province and canonically
ordered global endpoints. Identity is independent of branch and member enumeration. Queries from
either side return the same face and endpoint elevations. Only nominated outlet links carry drainage;
other touching faces do not imply a flow connection. An existing outlet boundary remains its own role
and cannot double as a composed face. Signed address extrema never wrap into false neighbours.

The envelope is 65,536 columns and 2,048 cross-basin faces. Preparation and decode support cancellation
without returning a partial candidate. [WatershedNetworkCodecV1](../../src/OpenFantasy.Core/Geography/WatershedNetworkCodecV1.cs)
uses a bounded four-million-byte archive containing scope, seed, province, exact member archives,
canonical closures, composed counts and faces. Decode rederives the entire composition and compares
every byte; unknown versions, noncanonical ordering, unused union fields and altered derived facts
reject. Members and closures sort by basin identity; face rows sort by global Y/X origin, then positive
X before positive Y. The nested basin interpreter retains its own unchanged format.

`OpenFantasy.Host --inspect-watershed-network` provides a privileged read-only archive inspection.

## Runtime binding: admitted network ownership

[WorldWatershedNetworkAdmission](../../src/OpenFantasy.Core/Worlds/WorldWatershedNetworkAdmission.cs)
retains the complete prepared network in a fresh profile-seven construction world. It requires a
profile-five basis with revision and advancing-step count zero and no reading hold. Admission pins
the prior admission version and exact network hash. World, branch and realm match the local space.
A root owns either a single watershed or a network; it cannot independently own both representations.

Province, every member's basin/formation/substrate/outlet/internal faces, and all shared faces receive
reserved macro roles. They cannot alias local terrain, material definitions or dynamic world roles,
and cannot be reused as accepted batch or command identities. The identity layer is reused only when
the macro object and terrain layer references all match; scope validation still runs. Changing the
network or terrain invalidates that cache. Member outlet identities remain reserved even when their
closure leads to another basin; the role does not declare physical terminal storage.

Ordinary world actions preserve the immutable network reference. Canonical snapshots, durable
commits, restart and exact recorded-commit history preserve its complete bytes alongside actors,
held dialogue, material provenance and construction geometry. The operator
`--create-watershed-network-world` creates an explicit new save from bounded canonical inputs under
the existing exclusive authority. It does not insert geography into a lived profile-five or profile-six
save, and a network cannot be silently attached by opening an older save.

This binding establishes retained macro ownership. It does not map macro columns into local terrain,
grant extraction rights, create liquids, change the local calendar, realize a river or migrate lived
geological history. The [persistence contract](persistence-and-history.md) defines its profile and bounds.

## Domain contract: dry macro-to-local geometry

[WatershedTerrainMapper](../../src/OpenFantasy.Core/Geography/WatershedTerrainMapper.cs) prepares a
separate immutable [terrain candidate](../../src/OpenFantasy.Core/Geography/PreparedWatershedTerrain.cs)
from an exact finite network, an explicit transform and a local window of at most 32 × 32 × 16 cells.
It does not construct a `TerrainRegionSnapshot`: that type's pinned archive reconstructs through its
own stratified generator. A different generator cannot be placed behind that old interpreter.

[WatershedTerrainMapping](../../src/OpenFantasy.Core/Geography/WatershedTerrainMapping.cs) pins integer
horizontal cells per macro column, vertical cells per elevation unit and a signed elevation datum.
Both scales range from 1 through 1,024. Horizontal zero maps to macro zero. Inclusive local footprints
are `[macro × scale, macro × scale + scale − 1]`; inverse lookup uses floor division, including negative
coordinates. Complete network footprints and every natural surface must fit signed 64-bit cell
coordinates. Intermediate products and datum addition use signed 128-bit arithmetic. Unrepresentable
transforms reject, including when the requested window itself would occupy a representable subset.

Each local XY column takes its parent's natural elevation through that transform. The surface is
piecewise constant within a macro footprint; there is no interpolation, detail noise, spill filling or
channel carving. Every requested column requires exact network coverage. Gaps and exterior requests
reject the complete candidate instead of falling back to unrelated generated terrain. A vertical query
compares absolute Z to the same surface regardless of its window origin; the top surface cell belongs
to substrate. Querying beyond the retained window rejects rather than inferring unseen support.

Each column references the exact macro basin/index, formation and substrate parcel and records its
offset within the macro footprint. A substrate cell exposes a
[typed ancestral address](../../src/OpenFantasy.Core/Geography/WatershedTerrainColumn.cs): parent
formation/parcel, mapping hash and absolute local cell coordinate. Branch identity and requested window
are absent from that address. Void cells have no substrate address. These are references and geometric
addresses, not additional owned entity roles, minted source IDs, physical quantities or export rights.
Changing the mapping changes its address pin; it cannot reinterpret established terrain in place.

[WatershedTerrainCodecV1](../../src/OpenFantasy.Core/Geography/WatershedTerrainCodecV1.cs) retains the exact
network archive, mapping, bounds and canonical Y/X column witnesses in a 4,100,000-byte envelope. Decode
rederives and compares the entire candidate. Its 68-byte column row contains basin ID, unsigned-32
macro index, formation ID, parcel ID, signed-64 surface Z and two unsigned-32 local offsets. Both
preparation and decode accept cancellation without returning a partial candidate. The privileged
`--inspect-watershed-terrain` operator reads that archive without changing a world.

Dry occupancy counts are geometry witnesses. They do not classify soil or ore, allocate material,
admit navigation/collision, transfer fluids, establish climate or fulfill coastal/reservoir dependencies.
Live realization requires its own retained terrain interpreter, complete macro dependency profile,
source allocations and explicit world admission. It cannot overwrite the farm or an established place.

## Domain contract: funded terrain material

[TerrainMaterialDeposit](../../src/OpenFantasy.Core/Geography/TerrainMaterialDeposit.cs) binds one
homogeneous solid slab to an exact network, mapping, macro column, item/physical calibration and
pre-admitted counted source interval. Its horizontal support is the entire macro column footprint;
depth is 1 through 1,024 cells below and including the natural surface. This finite support is
independent of the retained or requested detail window. The slab's full source quantity equals
`side × side × depth × unitsPerCell`. Each cell contains exactly 1,000,000,000 microlitres (one cubic
metre). Total mass and volume must fit their existing unsigned-64 physical measures. Porosity,
mixed ore, soil and stratigraphy require their own calibrated profiles, not implicit reinterpretation.

The source interval is partitioned in Z/Y/X order across that full support. For offsets from the
slab minimum, `ordinal = (z × side + y) × side + x`; each cell owns the canonical interval beginning
at `source.Start + ordinal × unitsPerCell`. Inverse unit lookup uses the same finite support. Signed
coordinate extrema and physical totals use checked arithmetic. Branch copies can preserve ancestral
source partitions; that fact confers no cross-branch export rights or permission to attach two copies
to one authority. Macro parcel identities remain references distinct from counted source identities.

[TerrainMaterialState](../../src/OpenFantasy.Core/Geography/TerrainMaterialState.cs) composes a
single immutable deposit, existing `CountedAuthorityState` and at most 4,096 active cell coordinates.
Initial admission requires the entire budget already funded in coarse custody. Coarse and detailed
custodians must have the same admitted owner. Scope, item/source pins and geographic/physical versus
custody identity roles are checked. No genesis stock is issued here. Every funded unit must remain
covered exactly once across coarse, detailed or other custodians; aggregate quantities are views of
those lots, not a second ledger. Detailed lots fit one active cell each; coarse lots cannot intersect
an active cell. A cell may retain no detailed stock after an actual transfer elsewhere.

[TerrainMaterialPlanner](../../src/OpenFantasy.Core/Geography/TerrainMaterialPlanner.cs) accepts at
most 256 unique cells per operation. Refinement requires substrate in an exactly pinned detail window
and moves only currently coarse-owned intersections into detailed custody. Each selected cell needs
some remaining coarse stock. Demotion moves only the selected cells' remaining detailed stock, then
removes their active bindings; an empty active cell can demote without producing a lot. Adjacent coarse
intervals can coalesce only with matching source, definition and custodian, no protection/reservation,
and complete containment in the deposit. Gaps and unrelated protected stock remain unchanged.

The [partition builder](../../src/OpenFantasy.Core/Geography/TerrainMaterialPartition.cs) creates one
atomic custody candidate, preserving coverage across arbitrary interior splits. Selected protected or
reserved lots reject the operation. Existing 256-custodian, 4,096-lot, 1,024-source and storage-capacity
bounds remain in force; fragmentation rejects rather than truncating stock or raising a budget.
Changed lots derive fresh identities from the accepted cause and canonical ordinal, checked against
current roles and the caller's historical-occupancy predicate. Cell addresses are not reusable lot IDs.
Unchanged lots retain their identities, revisions and metadata. Both representation storages and the
custody root advance their revisions; other storages remain unchanged.
Entity revisions cannot exceed that complete root revision, preventing a rewrite from rolling an
entity's version backward.

Each [plan](../../src/OpenFantasy.Core/Geography/TerrainMaterialPlan.cs) pins the complete input hash
and revision. Application against changed state rejects. Applying an immutable plan is not a durable
receipt or command retry protocol. The caller owns accepted-cause validation, historical identity
completeness, authorization and atomic durable publication. `ReconcileInventory` binds a separately
validated, advancing custody result with unchanged scope and admission; identical input is idempotent.
It preserves active cells and rejects missing funded units. Consumed/transformed claims therefore need
explicit production-retirement composition. A missing unit can never be recreated by refinement.

[TerrainMaterialCodecV1](../../src/OpenFantasy.Core/Geography/TerrainMaterialCodecV1.cs) uses a
5,710,000-byte standalone archive. The outer frame contains its tag, deposit, unchanged authority
archive and canonical signed-64 X/Y/Z rows (24 bytes each, sorted Z/Y/X). The deposit contains exact dry
terrain bytes, basin/index, depth, item identity/hash, existing physical-material definition bytes,
units per cell, source ID/start/end and coarse/fine custodian IDs. Decode bounds each field, rederives
terrain, validates coverage/bindings and compares canonical bytes. Cancellation returns no partial
state. The `--inspect-terrain-materials` operator reads this archive without writing a world.

This domain composition is separate from lived `TerrainRegionSnapshot`, local collision, the composed
world profiles and their durable history. Multiple overlapping allocations require a world-owned
allocation registry; separate standalone states are not independent minting authority. Geological
events, fluid coupling, production retirement, mining labor/rights and world admission must compose
with these invariants before a terrain intervention changes the playable world.

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
