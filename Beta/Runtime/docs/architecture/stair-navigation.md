# Ordinary steps and multi-level navigation

## Explicit contact interpretation

`GroundSupportMode.SteppedTerrainAndConstruction` selects ordinary stepping through retained terrain
and completed construction. It extends [constructed footing](constructed-footing.md) through an explicit
fresh world profile; existing single-height interpretations retain their meaning.

The maximum ordinary step and contact drop is the smaller of 64 spatial subunits (250 mm) and one
quarter of the actor's full body height, rounded down. The complete body requires known empty terrain
and exclusive actor/structure occupancy. Its projected footprint must be covered by exact top faces
within that contact drop below the feet. At least one face must have positive exact XY intersection at
the feet height; lower faces alone cannot authorize a floating actor. This does not calculate gait,
pressure or limb loads.

[FootingQuery](../../src/OpenFantasy.Core/Spatial/FootingQuery.cs) retains exact millimetre-derived XY
bounds and outward-quantized `StandingZ`. Raw physical tops need not equal the spatial grid: the
established subunit rounding tolerance remains valid. Terrain tops remain cell-aligned. Relevant patch
budgets and missing dependencies retain conservative refusal rules.

## One movement calculation

[LocalSpatialWalking](../../src/OpenFantasy.Core/Spatial/LocalSpatialWalking.cs) derives candidate
heights from actual surfaces intersecting the destination footprint within the allowed rise/drop.
It tries the current height first, then nearest heights with the lower height breaking a tie. Every
candidate passes endpoint contact and full-body clearance. Client or route waypoints cannot override
the resulting elevation.

Ascent checks a full-body lift and then horizontal traversal; descent traverses and then lowers.
These are swept collision checks inside one ordinary movement, not additional clock advances.
The whole swept footprint requires continuous projected support in the band
`[max(sourceZ,destinationZ) − contactDrop, max(sourceZ,destinationZ)]`. Extending the band from the lower
endpoint could borrow support too far below the higher endpoint. The rule permits flat movement while
straddling treads and conservatively rejects a body spanning incompatible narrow steps.

Tread widths must accommodate the actual footprint and swept move within one rise band. Arbitrary
stair dimensions, detailed foot stance, ramps and ladders require their own physical contracts. Larger
ledges retain the separate assisted climb with manipulation and earned effort. Ordinary steps need
usable ground locomotion and the existing advancing sample; they require neither hands nor climb effort.

`PrepareMove` selects this behavior only for stepped worlds. Control, actor ordering, reading holds,
clock/exposure and suppression after successful work remain owned by the world processor. Movement
queries the current candidate geometry; a stale route never supplies authoritative destination Z.

## Executable route graph

[WalkingRouteSearch](../../src/OpenFantasy.Core/Spatial/WalkingRouteSearch.cs) searches through the
actual ordinary 32-subunit XY transitions. Nodes retain XYZ, distinguishing actors above/below each
other. Every edge uses the same movement calculation and retains its derived height. No macro edge or
authored floor label can skip intermediate movement decisions.

The search alternates forward and reverse frontiers under one expansion budget. Each frontier combines
path cost with an admissible lower bound: the greater of XY Manhattan sample count and the minimum
vertical step count. Distance and XYZ order break ties. Lower-cost predecessors replace earlier ones;
stale queue entries do not count as expansions. Both directions receive deterministic opportunities,
so open ground near a destination's XY does not consume the entire budget before the upper-floor search
can find a staircase leading away from that XY.

Reverse search enumerates candidate supported heights at neighboring XY and replays the forward kernel
from each predecessor. Only a move that produces the exact current XYZ is a reverse edge. Moving from
the current node toward a predecessor is not proof of the opposite direction. Support-plane enumeration
reports missing cells directly: a blocked candidate at the current height cannot hide an unloaded
potential predecessor at a lower height. The first
exact XYZ meeting joins validated edges from both sides; the result does not promise a shortest path.
Waypoints exclude the start and confer no control or movement authority.

`QueryWalkingRoute` defaults to 2,048 expansions and accepts one through 4,096. `ExpansionLimit` is not
proof that no route exists. A separate transition-qualification budget is sixteen times the requested
node budget, limiting reverse predecessor work; exhausting either budget returns `ExpansionLimit`.
The result reports both expanded nodes and evaluated transitions. Missing terrain, blocked endpoints, inability to locomote and overflow
retain separate outcomes. Incompatible XY residues modulo 32 return `Unaligned`; an arbitrary assisted
climb can create such a residue, and routing cannot finish with an implicit snap or smaller sample.

`QueryGroundRoute` selects walking search in stepped worlds while retaining its caller's explicit
budget. Earlier profiles retain their cell-center fixed-height search. Structure placement selects
the matching default and maximum budget: 2,048/4,096 for stepped worlds, 256/256 otherwise. Explicit
smaller budgets remain binding. Access from actors' current elevations uses the same search.

Construction work/site and resource-anchor height restrictions remain separate contracts. Navigation
does not silently authorize upper-floor fabrication, harvesting or inter-building load support.

## Hosted actors and persistence

[LocalConstructionController](../../src/OpenFantasy.Hosting/Simulation/LocalConstructionController.cs)
uses walking routes in profile twelve and proposes ordinary XY samples. The world computes height
again. A builder returning after completed work treats a different home elevation as out of range even
when XY coincides. Other profiles retain their original policy. This commitment/home behavior is not
the complete society or job scheduler.

[WorldSteppedSupportAdmission](../../src/OpenFantasy.Core/Worlds/WorldSteppedSupportAdmission.cs)
requires an unplayed, unheld profile-eleven genesis. Its admission hash frames
`world.stepped-support-admission.v1` with the prior admission hash. It does not migrate a lived save.
[Snapshot twelve](../../src/OpenFantasy.Core/Worlds/WorldSnapshotCodecV12.cs) retains the same 17-field
complete root as profile eleven with spatial version three. Completed structures are restored before
actor admission, including mixed tread contact. The 16 MiB bound and schema-three recovery remain.

Ordinary movement retains its XY-only frame under step version twelve. The saved profile selects the
exact interpreter during replay. Climb action nine is admitted in eleven/twelve; excavation action
eight in ten/eleven/twelve. No separate locomotion clock, synthetic position event or actor copy is used.
