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
body clearance, supporting terrain, occupied bodies and installed structures. Earlier contact profiles
preserve feet elevation during ordinary cardinal movement; [stepped navigation](stair-navigation.md)
derives elevation from actual supported treads in its explicit interpretation.
The [constructed-footing profile](constructed-footing.md) adds exact
installed member support and a separate assisted ledge action with full-body sweeps and earned effort.
Local interaction checks equal feet
elevation, bounded centre reach and a clear segment. The
[ground route search](../../src/OpenFantasy.Core/Spatial/GroundRouteSearch.cs) and body checks
support legal local routing, not an unrestricted global pathfinder.

[Local excavation](excavation.md) supplies an explicit dry-world binding for selected cells. Earned
work, whole-cell source issuance and terrain removal publish together, with direct support validation.
The terrain value object's standalone operation remains distinct from that authorized world action.

[Coupled construction support](coupled-settlement.md) is a separate spatial interpretation from actor
footing. `LocalSpatialSnapshot.StructureSupport` defaults to terrain-only bases; explicit profile
seventeen permits complete coverage by terrain and foreign assemblies. A structure never supplies its
own external support. Local checks use conservative collider footprints and exact foreign top patches;
the physical member owner checks each connected component's lowest members, and the retained load
model separately requires exact material contact and an acyclic rated bearing graph. Thus visual overlap
or rounded collider fringes cannot create a missing load path. Earlier profiles keep their original law.

The bounded live consequence joins one intact moving group under a common downward displacement with
its affected riders and the original excavation transaction. This local support interpretation does not
supply natural-rock span mechanics, fracture/debris, arbitrary unequal trajectories, fluid breach behavior
or large-world structural scheduling; those remain distinct physical and publication obligations.

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

## Domain contract: funded runoff budgets

[WatershedRunoffRequest](../../src/OpenFantasy.Core/Geography/WatershedRunoffRequest.cs) pins an exact
prepared network, metre-cell mapping, counted authority, optional Production snapshot, admitted
reservation/owner, material calibration and nonempty half-open `WorldInstant` interval. It includes
one [input](../../src/OpenFantasy.Core/Geography/WatershedRunoffInput.cs) for every basin, including an
explicit dry record, and one [receiver binding](../../src/OpenFantasy.Core/Geography/WatershedRunoffTerminal.cs)
for every terminal. Collections remain bounded by the network's sixteen-basin profile.

The input depth is **effective runoff**, uniformly distributed over the basin's macro columns for the
whole declared interval. It is not total rainfall, a rainfall rate, an infiltration model or a hydraulic
step. Each mapped cell has one square metre of horizontal area. Column volume in microlitres is
`cellsPerColumn² × depthMicrometres × 1,000`. Division by the material's microlitres per counted unit
must be exact; fractional units reject instead of being rounded. Whole-basin source funding equals
that per-column quantity multiplied by the number of columns. All totals use checked integer arithmetic,
including physical volume and mass. The admitting catalog designates the substance and its calibration;
this budget contract does not supply the full fluid phase/reaction taxonomy.

[Funding validation](../../src/OpenFantasy.Core/Geography/WatershedRunoffFunding.cs) requires actual
live raw source intervals with exact item pins, source custodian and reservation, without protected
stock. A claim may span several lots and one lot may fund adjacent disjoint basin claims. Claims cannot
overlap, leave holes or substitute retired production inputs. Every raw lot held by the reservation
must belong completely to the declared inputs; the same reservation cannot also hold derived output.
Geography, physical definitions, inventory and production maintain compatible identity roles and pins.
Source and receiving custodians have the same admitted budget owner and distinct roles. Preparation
validates this basis but does not authenticate a command or grant permission to spend it.

[WatershedRunoffPlanner](../../src/OpenFantasy.Core/Geography/WatershedRunoffPlanner.cs) routes weighted
quantities through the retained acyclic network. Incoming upstream contributions enter the exact
receiving column before its own downstream accumulation. Local drainage ranks and sorted ready-basin
identities determine processing order. Dry basins can carry upstream runoff. Every funded unit reaches
one terminal; terminal totals equal input exactly. Per-column values are potential throughput along
paths, not resident liquid. Summing all path rows counts the same travelling contribution repeatedly
and is not a conservation test. Terrain elevations, source lots and topology remain unchanged.

Terminal bindings name an existing counted custodian and a positive physical volume capacity witness.
Several terminals may share a receiver only with the same capacity. Their incoming obligations are
summed before checking existing occupancy plus all incoming units against both counted capacity and
physical volume capacity. Every existing receiver item must use the exact runoff material pin, including
live transformed goods when Production is attached; unknown-volume mixed cargo rejects. Thus a topological
outlet count or a count-limited store cannot masquerade as physical headroom. The supplied volume witness
does not derive reservoir shape, liquid depth, coast connection or bathymetry.

[PreparedWatershedRunoff](../../src/OpenFantasy.Core/Geography/PreparedWatershedRunoff.cs) is an immutable
inspection candidate. `EnsureCurrent` checks the complete request hash, including authority, Production,
calibration, receiver capacity, forcing interval and mapping, before an integrating caller can rely on it.
It does not transfer stock, reserve receiving capacity, schedule a unique forcing event, define arrival
times or integrate an interior portion of the interval. Several inspection candidates can reference the
same held reservation; their existence does not establish globally unique scheduling or allocation.
An omitted Production field explicitly represents a raw-only basis, not permission to discard a known
world dependency. Its integrating owner must supply the complete actual authority.

The [v1 archive](../../src/OpenFantasy.Core/Geography/WatershedRunoffCodecV1.cs) retains a request and
rederived routing witnesses under `geography.watershed.runoff.v1`, bounded to 23,500,000 bytes. The
request frame is bounded to 22,900,000 bytes and contains exact network, mapping, authority, optional
production, reservation/owner, two 12-byte signed-day/normalized-millisecond instants, item ID/hash,
physical definition, input rows and terminal bindings. Input rows are 72 bytes in basin-ID order:
16-byte basin, unsigned-64 depth, 16-byte source, unsigned-64 start/end and 16-byte source custodian.
Dry rows have zero depth and zero funding fields. Terminal bindings use 40 bytes: basin, custodian and
unsigned-64 volume capacity. Column witnesses are unsigned-64 quantities in canonical basin/column
order; terminal witnesses are 40-byte basin/custodian/quantity rows. Decode bounds all fields, revalidates
funding and receiver headroom, reroutes contributions and compares every canonical byte. The privileged
`--inspect-runoff-budget` operator performs read-only inspection of this standalone contract.

Live hydrology requires an owner for finite receiving-capacity reservations, physical source/buffer
storage, flow geometry, stage arrival times, topology changes, local holds and backpressure. Climate,
crust history, coasts and full macro dependency admission remain distinct contracts. A stage must
revalidate its complete lived dependencies before spending stock or capacity. A prepared aggregate
budget cannot bypass encounter Wait, overwrite terrain or stand in for exact-time geological history.

## Domain contract: shared runoff capacity ownership

[WatershedRunoffCapacityState](../../src/OpenFantasy.Core/Geography/WatershedRunoffCapacityState.cs)
composes one exact counted authority, explicit optional Production basis and a fixed
[runoff domain](../../src/OpenFantasy.Core/Geography/WatershedRunoffDomain.cs). The domain pins the
network, mapping, owner, material calibration and complete terminal/capacity bindings. Creation admits
one already prepared budget; subsequent admission supports up to sixteen pending budgets. All requests
must use those exact shared dependencies. Several independently prepared budgets cannot each promise
the same free space: each receiver's existing raw/derived occupancy is counted once, then **all pending
incoming quantities** are added against its counted and calibrated physical capacities. Disjoint forcing
intervals do not imply that the receiver empties between events. Promised outflows do not create space.

`PlanAdmit`, `PlanCancel` and `PlanReconcile` return an immutable
[complete-state plan](../../src/OpenFantasy.Core/Geography/WatershedRunoffCapacityPlan.cs). `ApplyTo`
requires the entire current owner hash. The owner's logical revision advances for capacity-only changes
as well as reconciliations; custody need not change when a capacity hold is added or removed. The revision
is an ordering/precondition field, not an elapsed-time clock. A trusted integrating publisher must install
the returned **whole state** atomically. Inspecting a candidate or applying it to an old immutable copy
does not publish it to a world. Creating several owners against one receiver cannot establish exclusive
world ownership; the world compositor must admit one owner for every overlapping receiver dependency.

Cancellation releases the pending receiving obligation and retains its reservation identity in the
bounded, non-reusable closed set. It does not move, consume or clear the source's `ReservedBy` metadata.
An explicit custody successor may release that source hold after cancellation. Closed identities remain
reserved against re-admission and stale cancellation; the domain/calibration survive even when every
budget is retired. Explicit transport completion also retires its ID; the transport journal distinguishes
completed deliveries from cancellations. The closed set has a 4,096-identity bound within the authority's admitted reservation
catalog. Exhaustion rejects; archival compaction or new reservation admission requires an explicit owner
contract, never silent deletion of history.

Every separately validated custody/Production candidate must pass `PlanReconcile` before publication
under this owner. An otherwise legal delivery may be rejected because it consumes held receiving space.
Reconciliation requires advancing custody with unchanged scope/admission, preserves the presence or absence
of Production and its consumption profile, and retains exact admitted recipes, prior production batches
and consumed-item claims. Changed Production must
advance its own revision. Every active forcing input is re-prepared against the successor to revalidate
actual source reservations, pins, protection, receiver contents and complete aggregate headroom. A missing
source hold or overfull receiver rejects the entire candidate. The same-state operation is idempotent.
This boundary validates composition; it does not authenticate an external command or prove arbitrary
caller-supplied custody changes are legal transfers. Transfer/craft planners remain responsible for their
own operation invariants. The publisher must not install their unwrapped candidates directly.

[Explicit material continuation](liquid-material-continuation.md) derives a new segment from the
actual closing owner and re-prepares every pending request without advancing logical capacity time
or changing held quantities. Ordinary reconciliation cannot replace that continuation context.
Nested V3 geography archives require exact external continuation dependencies; V1/V2 preserve their
inline-history interpretation.

The [capacity v1 archive](../../src/OpenFantasy.Core/Geography/WatershedRunoffCapacityCodecV1.cs) is a
23,500,000-byte bounded standalone frame. Its eight fields are tag, domain, authority, optional Production,
unsigned-64 owner revision, pending rows, closed identities and receiver witnesses. Shared network and
inventory/Production bytes appear once. The domain frame retains network, mapping, owner, item ID/version,
physical definition and terminal rows. A pending row contains reservation ID (16 bytes), two instants
(12 bytes each) and the existing 72-byte input row for every basin. Pending and closed identities use
canonical ID order. A 56-byte receiver witness contains custodian ID and five unsigned-64 quantities:
existing, incoming, projected units, projected microlitres and capacity microlitres. Decode re-prepares
all pending routes, validates the shared owner and compares exact canonical bytes. Empty owners still
validate scope, receiver ownership, calibration, actual occupancy and closed identity admission. The
privileged `--inspect-runoff-capacity` operator reads this archive without mutating a world.

The capacity owner does not independently schedule arrivals or integrate time. The
[gated transport owner](reservoir-transport.md) can atomically complete a whole funded commitment,
move its exact source intervals and retire its incoming hold through an internal completion plan.
Neither path partially consumes a forcing budget, alters geometry or changes topology. The physical wrapper and world binding below
compose its storage and durable ownership. Staged backpressure and encounter Wait require their own
contracts. Known Production cannot be omitted and separately owned capacities cannot be treated as
free by another subsystem. A fixed single-material/network domain is a bounded supported profile;
heterogeneous fluids and interacting networks require explicit compatible composition.

## Domain contract: finite physical reservoirs

[ReservoirGeometry](../../src/OpenFantasy.Core/Geography/ReservoirGeometry.cs) defines an authored,
watertight, open vessel in one world/branch/realm. Each column occupies one square metre at signed X/Y,
with vertical support `[BottomZ, SpillZ)`. Floors, missing-column boundaries and perimeter walls are
assumed impermeable up to the common spill plane. Adjacent vessels remain separated by those walls.
This shape is not evidence that terrain supplies the walls, that a lake reaches the sea, or that an
inlet connects to a watershed outlet. A supported liquid profile interprets the exact material as
incompressible; item names, hardness and a volume calibration do not infer a phase/reaction taxonomy.

A vessel contains 1–4,096 unique horizontal columns, each 1–1,024 metres deep. Capacity is the checked
sum of column heights times **1,000,000,000 microlitres per cubic metre**. Elevation differences use
`Int128` before bounded conversion; absolute heights never multiply a signed world coordinate into a
narrow quantity. Four-neighbour traversal from one minimum-floor column must reach every column along
nondecreasing floor elevations. This conservative contour profile rejects disconnected minima and
perched pockets that cannot share the assumed equilibrium fill. It does not model siphons, pressure,
sealed cave chambers, aquifers, separate connected pools or history-dependent overtopping.

`Fill(volume)` inverts the vessel's volume/height relation through sorted floor bands. It returns an
immutable [ReservoirFill](../../src/OpenFantasy.Core/Geography/ReservoirFill.cs): actual volume, base
surface as signed whole metres plus unsigned billionths, residual microlitres and each column's volume.
Above one square metre, one microlitre is one billionth of a metre in height. Integer division retains
its remainder; one extra microlitre goes to each of the first eligible columns in global Y/X order.
The base surface therefore describes the common level before those discrete residuals. Empty fill
reports the minimum floor with all column volumes zero. Full fill reports the spill plane. No rounding
creates stock, and input/enumeration order does not change the result. These columns are distribution
witnesses of existing custody, not separate material lots or a new coarse/detail source partition.

[LiquidReservoirState](../../src/OpenFantasy.Core/Geography/LiquidReservoirState.cs) composes a complete
runoff capacity owner with fixed geometry for every receiving custodian and active wet-source custodian.
Additional same-owner mapped stores provide finite buffers. The collection supports 1–256 vessels and
at most 4,096 columns in total. Each geometry has a distinct reservoir identity and custodian; reservoir
identities cannot alias admitted authority, Production, material or macro roles. Scopes must match the
runoff realm. Shared X/Y columns may belong to different vessels only with nonoverlapping half-open
vertical intervals. Vertically stacked vessels are isolated by the declared watertight boundary model.

Every terminal's capacity witness must equal its geometric capacity exactly; counted-unit limits remain
independent. All raw and derived contents of every mapped store use the exact material calibration,
including stock unrelated to a pending runoff input. Actual fills use all current contents. Projected
receiver fills add all pending obligations; promises never appear in the actual fill. Sources do not
add their reserved stock again. Canceling the last commitment keeps all mappings, capacities and current
contents. Newly admitted wet sources must already have mapped geometry. Geometry membership cannot be
implicitly pruned or enlarged by capacity operations.

`PlanApply` wraps a separately prepared capacity-owner operation, validates its complete physical
candidate and returns a [LiquidReservoirPlan](../../src/OpenFantasy.Core/Geography/LiquidReservoirPlan.cs).
Its `ApplyTo` precondition hashes the entire wrapper, including geometry. A capacity-only plan can be
valid while the wrapper rejects an overfilled source/buffer or unmapped new source. The trusted world
publisher must install the complete wrapper atomically and exclusively; publishing an inner capacity or
inventory candidate bypasses this boundary. The wrapper does not authenticate transfers, authorize
geometric changes, create reservation identities, settle flow or repair arbitrary caller-supplied stock.

The [reservoir v1 archive](../../src/OpenFantasy.Core/Geography/LiquidReservoirCodecV1.cs) has a
24,000,000-byte bound and four fields: tag, exact existing capacity-owner archive, geometry collection and
fill witnesses. The collection starts with unsigned-32 count and length-prefixed geometry frames. Each
geometry retains world/branch/realm, reservoir/custodian IDs, signed-64 spill height and 24-byte X/Y/bottom
rows in canonical Y/X order. Geometry order follows reservoir ID. Each projection witness contains the
reservoir ID, existing and incoming units, then actual and projected fills. A fill contains volume,
signed surface metres, unsigned fraction/remainder and one unsigned-64 volume per canonical column.
Decode bounds counts/lengths/total support, reconstructs the whole physical owner, rederives every fill
and compares exact bytes. The `--inspect-liquid-reservoirs` operator is privileged and read-only.

Terrain-supported walls/floors, inlets/outlets, coast connection, actual arrivals, backpressure, phase
changes and topology edits require their own retained contracts. This equilibrium geometry cannot
teleport stock, supply atmospheric water, bypass encounter Wait or substitute for exact lived history.
The full geological design retains porous storage, underdark/cave flooding and heterogeneous liquids;
unsupported configurations require explicit profiles rather than being coerced into this vessel model.

[Reservoir boundary assessment](reservoir-boundaries.md) independently checks the nominal envelope
against retained local terrain and exact completed construction under an explicit sealing policy. It
reports gaps, obstructions and missing dependencies without changing this owner's admission rules.
A successful read-only geometric assessment does not qualify strength, live breach handling or gates.
The [contained fixed-reservoir profile](reservoir-containment.md) separately retains that sealing policy
and makes complete geometry and material-pin checks mandatory at world admission and publication.
It preserves the same stock/capacity owner while excluding transport and displaced physical members.

[Detailed liquid placement](liquid-cell-flow.md) gives an explicitly contained world source-backed
cell intervals and selected-boundary neighbor transfers. It uses these portions for actual local volume;
the equilibrium fill remains an aggregate witness and cannot stand in for a moving front. Detailed
allocation excludes pending runoff commitments and shares the same custody and Production authority.

The [terrain-bound breach composition](terrain-liquid-breach.md) derives actual void capacity from retained
terrain and joins earned extraction to conserved local flow. Its nominal envelope bounds potential water
space; current solid cells have no capacity. Dry bodies and installed members remain outside that envelope
and its declared vents.

## Runtime binding: exclusive reservoir ownership

[WorldSnapshot](../../src/OpenFantasy.Core/Worlds/WorldSnapshot.cs) profile eight owns one complete
`LiquidReservoirState`. Its inventory and Production must match the world's exact material roots.
The world's network is derived from that owner, never admitted as an independent competing copy.
World identity admission registers every reservoir ID and the item/physical definition pins alongside
terrain, bodies, languages, quests and construction. Active and closed runoff reservation IDs cannot
also appear in any quest material or construction project declaration. The inventory reservation catalog
alone does not distinguish purpose; this world composition rule prevents unrelated obligations sharing
one identity, even if each domain is independently valid. A stored reservoir cannot be reused as a command,
receipt or spawned identity. Shared definitions require exact pins across all components.

[WorldStepProcessor](../../src/OpenFantasy.Core/Worlds/WorldStepProcessor.cs) applies the physical check
to gathering, ordinary handovers, crafting, quest delivery and construction material changes. The check
uses the latest accepted wrapper within the batch, before publishing planner side effects. A material
rejection becomes an ordinary `Unavailable` receipt with original inventory and no spent effort,
retired input, production batch, paid reward, installed structure or spawned reservation from that action.
Time and independently accepted movement still follow the normal batch rules. Unchanged material state
retains the same wrapper; accepted changes retain geometry/domain and rebuild actual/projected fills.

Advancing time, opening a reading hold or passing an input interval's end does not settle runoff,
release its source reservation or drop outstanding receiving capacity. Empty/canceled owners retain
all physical mappings. No action in this profile edits geometry or simulates liquid arrivals. The same
transaction owns physical state, custody, Production and receipts; historical
reconstruction uses the exact recorded profile. The [persistence contract](persistence-and-history.md)
defines fresh admission, canonical bytes and outer size limits. The default farm remains its explicitly
admitted profile; this binding does not infer terrain walls, visualize water or insert reservoirs into
played saves.

## Runtime binding: funded runoff commitment controls

[RunoffCommitmentIntent](../../src/OpenFantasy.Core/Geography/RunoffCommitmentIntent.cs) defines explicit
admission and cancellation requests for the physical reservoir profiles. Each pins the complete reservoir hash and the
capacity owner's logical revision. The actor command also pins actor revision and carries its existing
principal/session delivery. [WorldRunoffResolver](../../src/OpenFantasy.Core/Worlds/WorldRunoffResolver.cs)
checks current control and exact domain ownership before inspecting private funding, then compares both
owner pins against the latest accepted wrapper within the batch. Changed capacity with unchanged custody
still makes an old request stale. Rejection returns a durable ordinary receipt and never retries implicitly.

Admission names an already admitted reservation identity, a half-open interval and an explicit row for
every wet/dry basin, at most sixteen. It cannot supply a replacement network, mapping, authority,
Production ledger, material or receiver set. The resolver rebuilds the request from the current fixed
domain, re-prepares exact source funding/routing and admits it through both capacity and physical plans.
All wet source stock must already be reserved under that identity, with the exact complete intervals.
Neither dry declarations nor existing incoming promises mint water or receiving headroom. The earliest
start is the selected advancing step's publication calendar, including that step's fixed clock increment;
backdated schedules reject. This is a funded obligation control, not permission to create rain or trigger flow.

Cancellation removes only an active pending receiving obligation, advances the capacity revision and
retains its ID permanently in the bounded closed set. Raw/derived stock, source reservations, physical
mappings and the fixed domain remain exact. An overdue obligation can be canceled explicitly; passing its
end alone does nothing. An already canceled ID cannot be admitted again. A separate lawful source-release
operation is required before canceled stock can be repurposed.

Both operations require an advancing batch and no current reading dialogue, including a hold opened by
an earlier action in that same batch. They perform no source transfer, elapsed work or arrival process.
After a capacity-only change, following actions see the accepted new headroom; a handover that precedes
cancellation can still reject. A prior accepted handover invalidates an old complete-owner admission pin.
Actor order is canonical and cannot be supplied as a preferred execution sequence by a caller. Command
and receipt identities are recorded normally; an existing budget ID is not a newly spawned entity.

The existing offline world-step operator and exclusive durable owner execute these requests. The
session rejects them for older profiles before queuing. Public action offers, player-facing controls,
automatic weather scheduling, liquid settlement and encounter-scoped hydraulic timing need their own
bindings. The world snapshot format is unchanged; [action version six](persistence-and-history.md)
adds only the bounded control interpreter while preserving prior command encodings.

## Runtime binding: scheduled physical transfer

[Reservoir transport](reservoir-transport.md) extends physical ownership with explicit normally closed
gravity gates and funded arrival stages in profile nine. It preserves source provenance while moving
actual stock, sharing per-step throughput and retaining blocked/completed/canceled outcomes. Profile
eight retains its non-automatic interpretation. The dedicated contract defines physical assumptions,
clock order, atomic publication and the separation from local terrain/hazard admission.

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
covered exactly once across live raw claims and, when explicitly attached, direct raw retirement in
Production. Aggregate quantities are views of those claims, not a second ledger. Detailed lots fit one active cell each; coarse lots cannot intersect
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
It preserves active cells and rejects missing funded units. An attached production proof remains intact
through inventory-only changes. Raw-only states reject retired coverage; production-backed states
account for it under the composition below. A missing unit can never be recreated by refinement.

[TerrainMaterialCodecV1](../../src/OpenFantasy.Core/Geography/TerrainMaterialCodecV1.cs) uses a
5,710,000-byte standalone archive. The outer frame contains its tag, deposit, unchanged authority
archive and canonical signed-64 X/Y/Z rows (24 bytes each, sorted Z/Y/X). The deposit contains exact dry
terrain bytes, basin/index, depth, item identity/hash, existing physical-material definition bytes,
units per cell, source ID/start/end and coarse/fine custodian IDs. Decode bounds each field, rederives
terrain, validates coverage/bindings and compares canonical bytes. Cancellation returns no partial
state. The `--inspect-terrain-materials` operator reads this archive without writing a world.

This domain composition is separate from lived `TerrainRegionSnapshot`, local collision, the composed
world profiles and their durable history. The finite registry below enforces shared allocation
ownership within its admitted scope; separate standalone states are not independent minting authority. Geological
events, fluid coupling, mining labor/rights and world admission must compose
with these invariants before a terrain intervention changes the playable world.

## Domain contract: shared material allocation registry

[TerrainMaterialRegistry](../../src/OpenFantasy.Core/Geography/TerrainMaterialRegistry.cs) owns one
exact prepared terrain basis, one `CountedAuthorityState` and 1 through 64 immutable
[allocations](../../src/OpenFantasy.Core/Geography/TerrainMaterialAllocation.cs). Each allocation has a
distinct identity and funded slab definition. Initial creation requires every allocated interval fully
in coarse custody. All allocation views reference the same inventory object; source units are never
copied into independent inventories. The registry is a bounded domain composition, not a second
durable world authority or an implicit migration of existing saves.

Allocations cannot intersect in physical support or reuse any interval of the same counted source.
Adjacent source intervals can share one coarse lot. Definition checks use identity and content hashes:
one counted item has one exact physical calibration, and a physical definition ID has one exact version.
All definitions use the registry's exact terrain archive, including network, mapping and retained window.
Individual refinement queries may use other windows of that same network and mapping. The deposit's
full macro support remains authoritative even when it extends beyond the retained window.

Coarse custodians may be shared, as may detailed custodians. A custodian cannot play both roles across
allocations. Existing source, lot, capacity and owner constraints apply to the combined inventory.
The 4,096-active-cell limit is shared across the entire registry, including empty active cells whose
stock has moved elsewhere. A transfer into another allocation's custodian does not attach its source
units to that allocation's cells. Those units remain attributed to their original source interval and
count as other custody for the originating allocation; they are not part of the receiver's allocation.

[TerrainMaterialRegistryPlanner](../../src/OpenFantasy.Core/Geography/TerrainMaterialRegistryPlanner.cs)
delegates interval partitioning to the existing material planner. It adds every allocation and physical
definition identity to the cause/spawn collision checks, together with shared macro/custody roles and
the caller's historical-occupancy predicate. The selected operation creates one candidate inventory;
every allocation is revalidated against it before publication. Unselected active-cell bindings remain
unchanged. [Registry plans](../../src/OpenFantasy.Core/Geography/TerrainMaterialRegistryPlan.cs) pin the
complete registry hash and revision, so another allocation changing invalidates a stale plan even when
the selected slab alone appears unchanged. Reconciliation likewise validates one advancing custody
result with unchanged admission across all allocations. It is not an authorization or receipt service.

The immutable network owns a lazy content hash and a shared
[material-role index](../../src/OpenFantasy.Core/Geography/WatershedMaterialIdentities.cs). These derived
caches contain no mutable authority, generation randomness or accepted decisions. They do not change
canonical bytes. Deposits query the shared index instead of copying the macro identity set. Binding
validation probes the bounded inventory's identities against that index, avoiding a full network scan
per slab. Cache initialization is thread-safe; lifetime follows the immutable network instance.

[TerrainMaterialRegistryCodecV1](../../src/OpenFantasy.Core/Geography/TerrainMaterialRegistryCodecV1.cs)
normalizes the archive into one terrain field, one unchanged authority field and an allocation list,
following the `geography.terrain-material-registry.v1` tag. The total archive limit is 6,250,000 bytes;
the allocation-list field is at most 640,000 bytes. Its big-endian unsigned-32 count precedes rows sorted
by allocation identity. Each row contains a 16-byte ID, unsigned-32 binding-byte length, binding bytes,
unsigned-32 active-cell count and signed-64 X/Y/Z triples in Z/Y/X order. The binding is the existing
deposit's ordered fields without repeated terrain, under `geography.terrain-material-binding.v1`, with
an 8,192-byte limit. Cumulative active-cell counts are checked before row allocation. Unknown versions,
oversized counts/lengths, duplicate/decreasing identities, noncanonical cells and trailing bytes reject.
Decode reconstructs shared objects, revalidates ownership and compares the complete canonical archive.
The single-deposit v1 interpreter and its exact bytes remain supported.

The privileged `--inspect-material-registry` operator reads normalized archives without writing a
world. Dynamic allocation admission/removal, durable world ownership, regional streaming, cross-branch transfer and publication into lived terrain require explicit integrating
contracts. Two separate registries do not establish disjoint ownership merely by each validating
internally; their integrating world must own the complete allocation scope.

## Domain contract: terrain and production composition

`AttachProduction` explicitly binds an immutable `ProductionSnapshot` to an existing
[registry](../../src/OpenFantasy.Core/Geography/TerrainMaterialRegistry.cs), retaining its terrain,
admission, source intervals and active cells. All allocation views share the same counted inventory
and production proof. [Shared evidence](../../src/OpenFantasy.Core/Geography/TerrainMaterialProduction.cs)
validates Production against actual authority once per composition and indexes direct raw retirements.
Those retirements include both recipe ingredients and explicit item-consumption claims. Produced
output consumption adds no second copy of its already-retired raw ancestry.
Raw and transformed lots share the 4,096-live-lot ceiling and each custodian's actual counted capacity.
Production and live derived revisions cannot exceed the custody root; retired ingredient revisions
precede it. Geographic and allocation identities cannot alias production roles. Shared item/physical
bindings require exact identity and version agreement, including the physical calibration per item.

Each allocation clips live raw source intervals and direct raw retirement intervals to its original
budget. Inline history supplies the original `RetiredIngredient` records; explicit
[terrain continuation](terrain-material-continuation.md) additionally retains imported ranges with
their item pins and retiring custodians. Sorted claims cover the budget exactly once without holes or overlaps:

`allocated = coarse + detailed + other live raw + retired raw`

`RetiredQuantity` measures original source units, not output units or mass. Recrafting a derived output
adds a production dependency without counting its ancestral raw ingredients again. Consumed intervals
never become coarse stock on demotion or refinement. Empty active cells can demote; completely depleted
cells cannot acquire material through refinement. A formed object retains its output entitlement even
when its item definition matches the original raw material.

[TerrainMaterialCraftPlanner](../../src/OpenFantasy.Core/Geography/TerrainMaterialCraftPlanner.cs) calls
`InstantCraftPlanner` with trusted delivery, actor, command and exact request. It preserves control,
recipe, revision, protection/reservation, capacity and retirement checks, and adds registry and external
historical identity guards for the command and every generated identity. It rejects crafting requests
addressing any terrain representation custodian. Composed state validation also rejects batches that
consume allocated raw intervals in any registry coarse/fine custodian, including a direct planner
result or imported archive. Transferring stock out is necessary before consumption; this condition
does not itself prove excavation labor, spatial access or permission.

`ReconcileProduction` binds a separately authorized inventory/production result atomically. Inventory
scope and admission stay exact and its root advances; changed production bytes require an advancing
production revision and unchanged recipe admission. Every retained batch remains byte-identical;
history cannot be dropped or rewritten even if a replacement snapshot balances independently.
Production cannot be detached through reconciliation. Representation operations preserve the proof
and reserve its retired-lot, command, batch and definition identities as well as live roles.
[Craft plans](../../src/OpenFantasy.Core/Geography/TerrainMaterialCraftPlan.cs) pin the entire registry
hash and custody revision, including all production bytes and other allocations. Rejections return the
original immutable candidate. Plans and reconciliation supply neither durable receipts nor world admission.

The explicit [registry v2 codec](../../src/OpenFantasy.Core/Geography/TerrainMaterialRegistryCodecV2.cs)
uses `geography.terrain-material-registry.v2` with terrain, authority, the unchanged normalized allocation
list, then exact `ProductionCodecV1` bytes. Its maximum is 23,100,000 bytes; the production field retains
its 16 MiB bound and the allocation list retains its 640,000-byte bound. The
[single-state v2 codec](../../src/OpenFantasy.Core/Geography/TerrainMaterialCodecV2.cs) uses
`geography.terrain-material-state.v2` with deposit, authority, active rows and production; its maximum
is 22,500,000 bytes. Both require production proof, validate complete composition and compare canonical
re-encoding. Existing v1 codecs preserve raw-only bytes and explicitly reject attached production;
no encoder silently drops evidence. Content hashes dispatch to the matching explicit profile.
The privileged `--inspect-material-production` operator reads registry v2 without changing a world.

Consumption-capable inline production uses explicit registry/state V3 archives. Inline runoff requests,
routing, capacity, reservoir and transport owners use their corresponding V2 archives; each retains the
complete production V2 evidence even when its consumed collection is empty. Qualified
[continued liquid owners](liquid-material-continuation.md) instead use explicit geography V3 frames
with nested Production V3/V4/V5 dependencies. Qualified terrain state/registry V4 frames instead retain
Production V6 and its typed representation evidence; arbitrary generic contexts and reserved transport
remain unsupported.
Ordinary successor reconciliation
preserves the selected interpretation in both directions. See
[composed material evidence](item-consumption.md#composed-material-evidence) for the exact version table,
history-retention contract and complete-world publication boundary.

This standalone composition reuses the production ledger's finite recipes, batches and provenance.
Explicit terrain continuation carries those rights across bounded material windows. It does not bind
terrain registries to the live world, authorize extraction, supply geological profiles, authenticate
archived history or establish global ownership across independently admitted registries.

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
