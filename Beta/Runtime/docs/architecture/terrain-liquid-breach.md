# Excavation and terrain-bound liquid

## Geometry and material owners

[TerrainLiquidTopology](../../src/OpenFantasy.Core/Geography/TerrainLiquidTopology.cs) derives a finite
set of liquid cells from known retained terrain inside nominal reservoir envelopes. Solid cells have no
liquid capacity. Every floor, exterior wall and retained divider requires an exact material listed in the
sealing policy. Every open cell has a direct vertical path through known empty terrain to the declared
spill-plane ambient cell. Unknown cells, sealed pockets and roofs do not supply that proof.

The topology hash binds the material domain, sorted nominal geometry hashes, sealing policy, sorted
complete region hashes and derived nominal/open cell sets. Region hashes include generation and edits.
It cannot be substituted between seeds, rules, scopes or terrain revisions. The nominal geometry is a
potential upper bound; `OpenCapacity` counts only current voids. Aggregate equilibrium fill is not an
actual moving water surface or an admission of capacity in solid terrain.

[LiquidCellState](../../src/OpenFantasy.Core/Geography/LiquidCellState.cs) retains an optional topology.
Its domain is the topology's open cells when present. Raw source intervals and current produced-lot offsets
still exhaust the sole reservoir custody/Production owner. `RebindTerrain` changes the proved geometric
context without relocating or relabeling any portion. A newly solid occupied cell therefore rejects.
The same conservative downward/lateral [flow law](liquid-cell-flow.md) spends starting quantities and
starting free space; an incoming unit cannot relay through a newly opened divider in the same step.

## Complete world admission

[WorldTerrainLiquidAdmission](../../src/OpenFantasy.Core/Worlds/WorldTerrainLiquidAdmission.cs) joins an
unplayed construction basis, exact reservoir material, retained sealing policy, unworked extraction sites
and initial terrain-bound placement into profile twenty-one. Earlier profiles keep their own meaning.
[WorldTerrainLiquidBinding](../../src/OpenFantasy.Core/Worlds/WorldTerrainLiquidBinding.cs) rederives the
actual topology on complete admission. It excludes actor bodies and installed members from the entire
potential wet envelope and its spill-plane vents, including dry receiver cells. Terrain-only actor support
and the absence of transport or structural consequence owners keep the interpretation explicit.

The finite geometry proof does not grant excavation permission. The ordinary command still requires the
site owner, trusted control delivery, matching actor/site/terrain/storage/tool revisions, actual tool
custody, manipulation and earned effort. A sealing rule alone cannot authorize a hole.

## Work and publication

[ExcavationReachMode](../../src/OpenFantasy.Core/Terrain/ExcavationReachMode.cs) distinguishes the existing
same-level side face from a raised-bank top face. A raised-bank stance is a centered cardinal neighbor
one metre above the target cell base. [Access](../../src/OpenFantasy.Core/Terrain/ExcavationAccess.cs)
checks the actual actor's reach and a clear segment to one subunit above the solid target's top. The
permission test does not remove the target or teleport the actor to the authored stance.

Before spending partial labor, [ExcavationPlanner](../../src/OpenFantasy.Core/Terrain/ExcavationPlanner.cs)
constructs the eventual complete opening and validates its topology, existing portions and dry occupants.
A hole that destroys the retained outer enclosure refuses. Partial work changes effort and site progress
while retaining the original solid terrain and topology. Completion publishes the original substrate
interval, terrain removal, labor and changed topology with the one command receipt.

[WorldStepProcessor](../../src/OpenFantasy.Core/Worlds/WorldStepProcessor.cs) reconciles material and actual
post-action terrain before complete admission. Failure restores every action-owned component, generated
identity and work flag; ordinary valid movement and selected time remain available. Liquid flow runs once
after actions at the selected calendar boundary. A reading hold advances no unselected liquid intervals.
This finite atomic composition does not implement independent encounter clocks or durable fluid work slicing.

## Fragment and storage certificate

Every normalized liquid portion contains a positive integral number of units. Exact disjoint coverage
means the number of portions cannot exceed total currently allocated units across all vessels and raw/
produced origins. Complete profile twenty-one admission limits that aggregate to 16,384. Retired recipe
ingredients are ancestry, not additional live allocation. This restriction prevents a later legal flow
from intrinsically exceeding the unchanged normalized-portion bound.

[WorldSnapshotCodecV21](../../src/OpenFantasy.Core/Worlds/WorldSnapshotCodecV21.cs) also reserves the full
1,400,000-byte V2 liquid component bound inside the unchanged 16 MiB whole-root bound. The remainder of
the root plus that reserve must fit, even if the current placement is compact. Material, quest or
construction successors cannot spend reserved liquid headroom. Occupancy checks have an explicit finite
dependency budget. These are admission bounds, not claims of production-scale latency or a substitute
for a larger streaming/continuation contract.

## Canonical archives and host boundary

[LiquidCellStateCodecV2](../../src/OpenFantasy.Core/Geography/LiquidCellStateCodecV2.cs) carries the complete
reservoir pin, flow policy, selected boundary/revision, topology hash and canonical 80-byte portion rows.
Decoding receives topology rederived from archived world terrain, not a supplied list of allegedly open
cells. [ExcavationCodecV2](../../src/OpenFantasy.Core/Terrain/ExcavationCodecV2.cs) retains the explicit reach
mode. V1 liquid/excavation encoders reject semantics their archives cannot represent; all-side extraction
retains its existing component hash interpretation.

The direct world archive has twenty fields: the profile-twenty component sequence with V2 liquid placement
at index eighteen and V2 extraction at index nineteen. Decoding joins complete owners and checks canonical
re-encoding. [WorldStepCodecV21](../../src/OpenFantasy.Core/Worlds/WorldStepCodecV21.cs) admits extraction
through the existing discrete-action family; runoff, transport, climbing and consequence continuation
remain excluded. The exclusive schema-three authority retains atomic commits, authenticated retries and
exact historical replay. Privileged liquid diagnostics expose actual open cells, topology and capacity;
they are not a player knowledge projection.

[BreachFarmAdmission](../../src/OpenFantasy.Hosting/Content/BreachFarmAdmission.cs) composes a separate fresh
farm with the original residents, gathering, recipes, quests and construction. Its authored geography has
six cubic metres of water above an intact divider and an empty lower trench. Three owned work actions
extract the original substrate with an actually held starter tool. No initial work, quest reward, fabricated
ore conversion or recurring water supply is implied by this content admission.

## Adjacent contracts

Occupied wet space needs exposure, displacement and body behavior. Structures bearing or sealing water
need load, pressure and failure rules. Mechanical gates, pumps, spill beyond the finite envelope, liquids
with different properties, reactions, phase changes, ecology, offscreen exchange and temporal encounter
boundaries each need compatible state and publication contracts. Presentation consumes permitted committed
geometry and receipts; a Host diagnostic does not establish a native action interface or art acceptance.
