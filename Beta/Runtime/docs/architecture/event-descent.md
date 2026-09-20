# Event-ordered intact descent

## Collision domain

[VerticalMotionScene](../../src/OpenFantasy.Core/Spatial/VerticalMotionScene.cs) retains a finite complete
supplied collision scene: known terrain, stationary structures and actors, and explicitly selected moving
structures and actors. A [motion body](../../src/OpenFantasy.Core/Spatial/VerticalMotionBody.cs) is either
one intact assembly or one corporeal actor. Its volumes, physical bearing surfaces and identity stay intact.
The scene pin includes the complete supplied geometry and body state. The caller remains responsible for
proving that the supplied scene contains every relevant obstacle and owned material.

[VerticalMotionResolution](../../src/OpenFantasy.Core/Spatial/VerticalMotionResolution.cs) advances all
still-falling bodies by the same free-fall distance from their initial rest. It selects the earliest contact,
validates that swept prefix, stops the supported bodies and reinserts their material into the stationary
collision scene. Other bodies continue against that updated scene. Thus one body can land before another,
and a later fall can land on material that stopped earlier. Removing every candidate mover for the entire
calculation would erase those collisions and is not permitted.

Initial upward contacts carry dependants into the same stopping event. Lower top rectangles are physical
surfaces; upper base footprints use conservative colliders. A fractional fringe can enlarge the stopping
closure and cause conservative refusal; it cannot supply a missing full-footprint bearing. Complete-world
assessment additionally checks exact material contact. Actors never supply structural floors. Simultaneous events are resolved as a
set in canonical identity order; newly stopped structures participate together in complete-footprint support
checks. A partial ledge cannot be skipped in search of a more convenient lower floor. An unresolved body
collision, unsupported stopping configuration, missing terrain or exhausted horizon yields a terminal
failure with no candidate result. Existing completed events remain a diagnostic prefix, not published motion.

Event ordering uses exact cumulative subunit descent, not rounded timestamps. Under the shared gravity
and initial-rest interpretation this distance is monotonic in time. A body that stops leaves the moving
set; its displacement does not continue accumulating. The domain does not simulate bouncing, impulses,
rotation, new forces or a body released at a different instant. Those require an explicit response law.

## Work and restoration

An advance performs bounded actual geometric queries. For `N` moving bodies containing `Q` collider
boxes, at most `N` events each perform two queries per remaining collider, and all stopping support checks
use at most `Q` further queries. The bound is `(2*N+1)*Q`. Scene preparation, sorting, collision-scene
reconstruction, array copying and final result construction are outside this query count. A finite query
bound is not a frame-time or production-throughput guarantee.

[VerticalMotionCodecV1](../../src/OpenFantasy.Core/Spatial/VerticalMotionCodecV1.cs) retains the exact scene
pin, real work count and status. Decode reconstructs that prefix from the trusted scene and compares canonical
bytes, checking cancellation between slices. Provisional positions supplied by an archive cannot become
authority. Forward advancement uses the retained immutable progress instead of replaying previous queries.

## Construction-world assessment

[ConstructionDescentAssessment](../../src/OpenFantasy.Core/Structures/ConstructionDescentAssessment.cs)
binds the collision domain to a complete stable coupled world. Candidate terrain retains the complete
region partition, substrate identities and irreversible holes. The retained support graph identifies
affected assemblies; exact riders and independently unsupported actors join the moving set. Every other
body and assembly remains stationary collision material. The owned assembly bound and harmless settlement
policy remain in force.

The assessment pin additionally includes the complete original world, candidate contact scene and retained
load, motion and coupled policies. A later world with identical geometry has a different pin. Its
[cursor codec](../../src/OpenFantasy.Core/Structures/ConstructionDescentAssessmentCodecV1.cs) reconstructs
against those full dependencies. Source custody, calendar, command identity and condition cannot be substituted
under an old geometric cursor.

On successful geometry, one physical-owner transition moves every affected member once. One presence
revision moves each affected actor once. Conditions, identities, original installed backing, member volume
and mass remain unchanged. Complete spatial/component admission and the owned contact/load interpretation
then validate the candidate. Final load resolution is additional unsliced work; geometric work counters
do not hide it behind a latency claim.

The assessment API alone grants no publication authority. Counterfactual holes do not earn labor or
extraction rights. Its actor loads include the original carried goods; a publishing transaction must
check newly issued ore and other custody/time changes as well. The
[durable coupled resolver](coupled-settlement.md) retains its common-trajectory interpretation.

## Explicit live interpretation

[WorldEventSettlementAdmission](../../src/OpenFantasy.Core/Worlds/WorldEventSettlementAdmission.cs) selects
profile eighteen only from an unplayed, unheld, job-free profile seventeen. Its admission pin retains
the preceding interpretation, all four load/settlement/execution/coupling policy hashes and the explicit
[event mode](../../src/OpenFantasy.Core/Structures/ConstructionDescentMode.cs). Ordinary successors and
pending-work successors preserve that mode. Loading an older save does not convert it.

[ConstructionSettlementResolution](../../src/OpenFantasy.Core/Structures/ConstructionSettlementResolution.cs)
provides the common continuation contract. `BeginEventOrdered` delegates to the immutable assessment;
`BeginCoupled` retains the original common-trajectory algorithm. The live excavation owner admits only
an eligible, controlled original command, then persists private geometric progress while the published
terrain, members, bodies, inventory and clock remain unchanged. Terminal publication applies the actual
excavation work, source issuance and one interval through complete world admission. That admission rechecks
loads using the resulting carried goods. A successful assessment is therefore necessary but not sufficient
for a successful excavation. Partial labor retains its complete-hole preflight without publishing the hole.

[Root V18](../../src/OpenFantasy.Core/Worlds/WorldSnapshotCodecV18.cs) retains twenty-four fields: the
preceding component layout, a [V3 pending frame](../../src/OpenFantasy.Core/Worlds/PendingExcavationCodecV3.cs)
and final event-mode field. The [V3 resolution codec](../../src/OpenFantasy.Core/Structures/ConstructionSettlementResolutionCodecV3.cs)
reconstructs the actual scene before checking the claimed work bound and exact prefix. The old linear
coupled bound cannot stand in for `(2*N+1)*Q`. Legacy cursor codecs refuse event work. Schema four, ordinary
receipts and the exclusive journal transaction remain unchanged.

The [consequence owner](consequence-continuation.md) derives possible terminal receipt identities and
remaining journal capacity from the actual event-scene bound and retained execution budget. Before accepting
work, the host must reserve enough commits, outcomes and journal bytes to finish even its longest admitted
prefix; root encoding remains independently bounded. A small query budget can consume more journal space
and does not grant an exception to those limits. Preparation, reservation generation, replay and final
complete-load admission remain outside the query slice and require separate performance qualification.

This live interpretation still selects one existing excavation site and removes one cell only when its
work is earned. It does not synthesize simultaneous remote holes or detached fragments to demonstrate
different landing distances. Broader removal or breakage mechanisms must own their costs, source rights
and release geometry before supplying such a scene to the same event kernel.

## Impact demand and extension boundary

[StructuralImpactDemand](../../src/OpenFantasy.Core/Structures/StructuralImpactDemand.cs) records positive
descents using actual assembly mass or the actor's body plus carried mass. Assembly mass excludes riders;
actors are counted separately. For mass in milligrams, gravity in millimetres per second squared and descent
in subunits, energy in picojoules is `mass * gravity * descent * 1000 / 256`. The exact numerator and fixed
denominator retain fractions instead of rounding away energy. Flight time is the retained policy's first
integer millisecond reaching the exact distance. It does not advance world time or decide event order.

The harmless policy determines whether the intact candidate may preserve condition. Exceeding that band
produces an explicit unresolved response; a kinetic-energy number grants no injury or fracture permission.
Static bearing capacity, terrain hardness and transmitted load are not impact toughness. The actor body
schema does not supply positioned limb collision geometry, and an installed member's mass share is not a
separate salvage entitlement. Fracture, severed joints, conserved located debris, anatomy-specific injury,
long flight, simultaneous changed forces and liquid displacement need their respective owners before live
publication can use those outcomes.

## Host inspection

[ConstructionDescentOperator](../../src/OpenFantasy.Host/ConstructionDescentOperator.cs) reads an exact
recorded commit and proposes removal of one existing incomplete excavation site. It reports the original
world/policy pins, actual work, stopping events, impact demands and admitted candidate geometry. It does
not execute the player's excavation command, resume a pending job or modify the save. Historical inspection
cannot silently substitute the current head or interpret completed sites as fresh extraction.
