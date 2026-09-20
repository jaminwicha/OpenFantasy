# Intact structural settlement

## Authority and calibration

[WorldStructuralSettlementAdmission](../../src/OpenFantasy.Core/Worlds/WorldStructuralSettlementAdmission.cs)
admits profile fifteen from an unplayed, unheld structural world. Its admission pin includes the prior
admission and complete [StructuralSettlementPolicy](../../src/OpenFantasy.Core/Structures/StructuralSettlementPolicy.cs).
Ordinary successors retain both load and settlement policies; no command edits either calibration.
Earlier saved profiles retain their interpretation and byte formats.

The settlement policy specifies a maximum fall distance, separate harmless body and member distances,
and gravity in millimetres per second squared. Maximum distance is at most one 256-subunit cell (one metre).
Zero harmless distance disables that kind of fall. These are explicit gameplay calibrations, not inferred
material toughness or medically realistic injury thresholds. A contact exceeding either relevant harmless
distance requires a damage owner and is refused by this interpretation. Member condition and actor injuries
remain unchanged within the admitted harmless band; no healing or condition reset occurs.

`FlightMilliseconds` computes the first integer millisecond whose constant-gravity descent from rest
reaches the requested distance. Integer arithmetic retains the fractional spatial unit. Admission requires
even the maximum flight to fit the selected 1,800-millisecond exploration interval. A settlement consumes
that original interval; it never adds calendar time, resident exposure, labor or effort recovery. This
does not interpret airborne velocity, bouncing, rotation or injury-producing impact.

## Complete excavation candidate

[ExcavationPlanner](../../src/OpenFantasy.Core/Terrain/ExcavationPlanner.cs) keeps its normal authorization,
reach, tool, effort, capacity and source checks. Each work attempt prepares whole-cell removal. In this
profile it passes the complete current physical scene and proposed terrain to
[ConstructionSettlementResolver](../../src/OpenFantasy.Core/Structures/ConstructionSettlementResolver.cs)
before constructing a stable spatial root. Partial labor discards the projected motion and source issuance;
the final swing publishes them only with the completed site, exact raw lot and terminal receipt.

The resolver checks all assemblies for lost lowest-member terrain support. At most one affected assembly
is admitted per removal. Every member retains a uniform vertical placement and disposition; horizontal
translation, partial detachment and rotation require a different collision interpretation. Moving contact
planes must be exact subunit planes, avoiding a rounded collider standing in for a separated physical face.
Existing local counts and aggregate assembly bounds remain in force.

All lowest members must have the same positive first-contact drop and full support. A remaining ledge,
unknown terrain, inconsistent contact, another body or an exhausted search horizon cannot become permission
to skip to a lower surface. Every upper member also needs an unobstructed sweep for that distance. A rider
requires complete exact footprint coverage by the moving assembly at its feet. Members and riders share
one translation, so their relative geometry remains unchanged. Their own group is excluded from those
sweeps; every outside structure and body remains an obstacle. Removing an entire scene of candidate movers
would erase real collisions and is not allowed.

Settled material is restored before other unsupported actors seek contact. Their complete bodies descend
against retained terrain, all settled structures and other actor bodies. Actors do not supply landing floors.
The resulting complete spatial root must pass clearance, exclusive occupancy and ordinary support, and every
exact physical component must retain terrain-grounded lowest members. This interpretation does not admit
cross-assembly resting contact or a structure that merely intersects a convenient ledge.

Moved members keep their identities, installed-lot backing, mass, volume and condition. Their physical
revisions and the physical owner revision advance; riders keep their forms, conditions, inventory and
identity while their poses and presence revisions change. Coupled displacement consumes moved actors'
movement opportunity in the selected step. Final publication also checks gathering anchors, material
ownership and the complete structural load graph, including the excavator's newly carried source material.
Any failure restores all candidate consequences before receipt and spawned identity publication.

Profile fifteen's candidate load check previews the selected time boundary consistently across calendar,
resident exposure, advancing-step count and gathering recovery. This permits the first earned labor step
without pretending time already passed in the retained world. The actual transition still occurs once.
Profile fourteen's prior candidate interpretation remains unchanged for exact historical replay.

## Resting loads and foundations

[ConstructionBearingModel](../../src/OpenFantasy.Core/Structures/ConstructionBearingModel.cs) binds profile
fifteen's uniformly descended assemblies at their current origins. Authored joint geometry survives that
rigid translation; original ratings and retained wear remain authoritative. Base footprints require full
contact with existing rated terrain at the new elevation. Actor weights attach to current member surfaces.
Complete load resolution must remain failure-free; harmless motion does not grant unlimited support strength.

Removing a foundation does not remove its policy declaration, identity or historical pin. Its active contact
is retired only when the exact retained cell is excavated and a completed owned excavation site matches
its cell, terrain source and material. The normal excavation admission also validates source issuance.
Missing terrain or an unexplained hole is refused. A newly exposed lower cell needs its own existing rating;
solidity alone cannot invent bedrock. The model retains the full foundation catalog while its graph contains
only active anchors.

## Persistence and extension contracts

[WorldSnapshotCodecV15](../../src/OpenFantasy.Core/Worlds/WorldSnapshotCodecV15.cs) uses a direct twenty-field
root: profile fourteen's field positions plus the complete settlement policy. The small five-field policy
frame retains its tag and four unsigned-64 calibrations. Encoding is pure; decoding enforces value bounds,
canonical bytes and complete root invariants. The 16 MiB root limit, schema-three exclusive writer, journal
identity rules and terminal receipts remain unchanged. Crash recovery either retains the predecessor or
replays the entire accepted settlement; no partial unsupported root is published.

This atomic bounded interpretation has no durable pending operation. Larger affected closures require a
separate continuation contract retaining stable basis, complete policy, original command and bounded cursor.
Continuations must resume without actor input, add no second time/effort interval and issue the original
command's result and source exactly once. Overload breakage, deformation, debris transformations, harmful
impacts, longer flight, resting on other assemblies and wet displacement retain their separate ownership
obligations. An intact fall does not satisfy those contracts or erase their acceptance gates.
