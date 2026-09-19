# Constructed footing and assisted traversal

## Ownership and physical contact

[GroundSupportMode](../../src/OpenFantasy.Core/Spatial/GroundSupportMode.cs) is an explicit spatial
interpretation. `TerrainOnly` preserves grounded occupancy on whole terrain-cell top planes.
`TerrainAndConstruction` also admits actual installed member tops. Clearance remains independent:
every body cell must be loaded and empty, and the complete body must avoid other actors and structure
colliders. Missing dependencies refuse admission or traversal.

[ConstructionDefinition](../../src/OpenFantasy.Core/Construction/ConstructionDefinition.cs) derives
collision envelopes and [bearing surfaces](../../src/OpenFantasy.Core/Spatial/StructureBearingSurface.cs)
from the same exact millimetre members. Only completed projects supply authoritative surfaces through
[ConstructionSnapshot](../../src/OpenFantasy.Core/Construction/ConstructionSnapshot.cs). World admission
compares both sorted colliders and surfaces with those rederived from retained construction. Geometry
alone cannot prove funding, completed labor or ownership of the installed output.

A bearing surface retains exact XY bounds and physical top height in 1/256 millimetre ticks. One spatial
subunit equals 1,000 ticks. The standing plane is the physical top rounded upwards to the spatial grid;
the resulting vertical contact tolerance is strictly less than one subunit (3.90625 millimetres).
Signed rounding works identically below the world datum. XY coverage has no tolerance: even a one-tick
slit remains a gap. Rounded collision bounds cannot enlarge the amount of supporting material.

[FootingQuery](../../src/OpenFantasy.Core/Spatial/FootingQuery.cs) combines intersecting terrain top
faces and matching member surfaces with an exact rectangle-union calculation. Their union must cover
the entire actor footprint. More than 256 relevant patches returns `Unavailable`; no face is silently
dropped to fit the budget. Whole-cell terrain support is considered only at its exact cell-top height.
At such planes, unknown support cells remain unavailable even when another supplied surface overlaps.

Fixed-elevation movement and same-floor route search use the same support query across the entire
swept footprint. Supported endpoints do not permit crossing an unsupported gap. Actors and structures
keep exclusive volume occupancy. Structure bases retain terrain-only support checks, and each authored
assembly retains its positive-area member connectivity. Constructed actor contact does not establish
inter-building support, load capacity, buckling, collapse or debris behavior. Removing a necessary
terrain base is refused, including when an actor stands on the affected completed structure.

## Assisted vertical action

[ClimbIntent](../../src/OpenFantasy.Core/Spatial/ClimbIntent.cs) requests a signed displacement along
exactly one horizontal axis and a nonzero vertical displacement. Each magnitude is at most 256 spatial
subunits (one metre). The vertical displacement also cannot exceed the actor's body height. This is a
bounded assisted ledge traversal, with usable ground locomotion and manipulation required from the
actual body condition. It does not infer a universal climbing skill from species or controller kind.

[PrepareClimb](../../src/OpenFantasy.Core/Spatial/LocalSpatialClimbing.cs) checks the destination and
two full-body axis sweeps. Ascent rises at the source before traversing horizontally at the destination
height; descent traverses at the source height before lowering. The enclosing diagonal box is not used:
it would incorrectly include the ledge being climbed. Endpoint-only checks are also insufficient because
they would miss a ceiling, overhang or actor along the movement path.

The complete projected horizontal corridor must have continuous terrain/member coverage at top planes
between the two endpoint heights. This allows an assisted transition over a riser but refuses a jump
between separated platforms. Ground support is required at both published endpoints. No mid-air actor
state is published, and this discrete path contract does not supply choreography or a continuous
kinematic animation.

[WorldClimbResolver](../../src/OpenFantasy.Core/Worlds/WorldClimbResolver.cs) checks trusted control,
an advancing unheld action opportunity, the expected actor revision and 400 banked effort from the
shared gathering effort account. Success changes actor feet/revision and spends effort together. The
ordinary advancing step contributes its normal clock, personal exposure and effort recovery once;
the successful action suppresses that actor's ordinary movement sample in the same batch. Refusal
spends no climb effort and changes no climb position. An ordinary advancing batch still has its normal
time/recovery behavior. Held interactions cannot climb or earn effort.

Players and hosted NPCs use the same action. Automatic NPC route selection remains same-floor.
Resource anchors and addressed conversation retain their separate terrain-grounded and equal-elevation
contracts; adding constructed actor footing does not silently reinterpret them. General stair routing,
ladders, flight, ramps, cross-floor interaction, ability-specific movement, action offers, controls and
animation require explicit bindings to these authority rules.

## Complete persistence composition

[WorldConstructedSupportAdmission](../../src/OpenFantasy.Core/Worlds/WorldConstructedSupportAdmission.cs)
creates profile eleven from an unplayed, unheld profile-ten dry excavation genesis. Its admission hash
frames `world.constructed-support-admission.v1` with the prior admission hash. Existing saves do not
change contact rules when opened. Successor states retain the same support mode; independent watershed
and reservoir owners remain excluded by the complete dry-world contract.

[Snapshot eleven](../../src/OpenFantasy.Core/Worlds/WorldSnapshotCodecV11.cs) is a direct complete frame.
Its 17 ordered fields are tag, version, authority, spatial components, residents, admission hash,
calendar days, calendar milliseconds, revision, advancing steps, dialogues, gathering, production,
progression, quests, construction and excavation. Spatial version two retains the same scope, regions,
body definitions and actor presences as spatial version one. Derived surfaces are not independently
serialized or spendable.

Decoding retains the spatial bytes until construction has been decoded, derives installed structures
with actor-bearing surfaces, then admits spatial occupancy and the complete world once. It never
constructs an intermediate actor snapshot with its supporting floors removed. Profiles one through ten
keep their original terrain-only interpretations and canonical bytes. Exact re-encoding, the 16 MiB
world bound and schema-three exclusive writer/recovery rules remain in force.

[Action version nine](../../src/OpenFantasy.Core/Worlds/WorldActionCodecV9.cs) uses the ordinary
ten-field actor-action envelope. Its intent frame is `world.action.climb`, version one, followed by
signed 64-bit X/Y/Z deltas. The action is bounded to 512 bytes and accepted only by step profile eleven.
Excavation action eight is accepted by both dry profiles ten and eleven; terrain replacement preserves
the selected contact interpretation and checks actor/structure support before publication.
