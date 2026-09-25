# Reservoir containment

## Runtime binding: a retained fixed-vessel invariant

[WorldSnapshot.ReservoirSeals](../../src/OpenFantasy.Core/Worlds/WorldSnapshot.cs) owns the immutable
[sealing policy](../../src/OpenFantasy.Core/Geography/ReservoirSealPolicy.cs) alongside the exact reservoir,
inventory, Production, local terrain and construction state. Every rule, including an unused rule, refers
to an already admitted material identity and version. A policy calibrates admitted matter; it cannot
create an arbitrary content definition or replace one under the same identity.

[WorldReservoirContainmentBinding](../../src/OpenFantasy.Core/Worlds/WorldReservoirContainmentBinding.cs)
requires every vessel's [boundary assessment](reservoir-boundaries.md) to be complete and contained.
Its nominal capacity envelope is clear of solid substrate, completed construction and corporeal bodies;
the floor and exposed sides have exact continuous coverage under the retained material rules. Missing
terrain, positive gaps, incompatible material, coordinate limits and exhausted work budgets refuse
admission. Empty water stock does not release the nominal envelope for occupation.

The evaluator returns geometric results without hashing a root under construction. Public inspections
add their world, reservoir and policy pins afterward. A successful external inspection using another
policy is diagnostic evidence only. It cannot bypass the root's retained rules or admit a material pin.

This interpretation permits fixed construction and terrain-only actor support. It excludes reservoir
transport, excavation, physical member displacement, structural policies and consequence cursors.
Constructor guards prevent incompatible owners from being discarded by profile or codec selection.
The geometry is a sealing proof, not pressure strength, structural load support or safe immersion.

[Detailed liquid flow](liquid-cell-flow.md) retains this invariant and adds exact spatial material
portions. It requires terrain-supplied enclosure: any construction contributing a sealing face rejects.
The same boundary assessment establishes this restriction without a second geometric calculation.

## Runtime binding: complete actions and publication

[WorldStepProcessor](../../src/OpenFantasy.Core/Worlds/WorldStepProcessor.cs) reconciles each action's
inventory and Production with its resulting reservoir owner before complete candidate admission.
Using the previous reservoir with changed inventory is invalid even when the liquid volume is equal:
filtering and fabrication can change source retirement, derived lots and custody relationships.

Containment preflight uses the selected advancing interval's eventual calendar, experienced time and
gathering boundary so earned construction work is checked coherently. The actual interval is applied
once. Held batches use their held boundary. Failed admission restores the attempted action's component
states and generated identities while preserving earlier accepted actions; an ordinary unavailable
receipt records the refusal. Refused construction does not consume its actor's movement opportunity.

After time advances, every prepared movement is checked against the complete scene and all residents.
Only the last fully admitted and encoded post-time movement root is reusable for publication. A rejected
move restores its prior space; action preflight roots are never reused as final published states.
The sole authority commits state, receipts, identities and history atomically.

## Runtime binding: explicit admission and canonical history

[WorldReservoirContainmentAdmission.CreateGenesis](../../src/OpenFantasy.Core/Worlds/WorldReservoirContainmentAdmission.cs)
accepts only an unplayed, unheld profile-eight basis. It retains every component and derives an admission
hash from the canonical `world.reservoir-containment-admission.v1` frame, prior admission bytes and complete
policy hash. It does not upgrade an existing played save or weaken an earlier profile's interpretation.

[WorldSnapshotCodecV19](../../src/OpenFantasy.Core/Worlds/WorldSnapshotCodecV19.cs) uses one direct
eighteen-field root: tag, profile, inventory, raw spatial components, residents, admission, calendar day,
calendar milliseconds, revision, advancing steps, dialogues, gathering, Production, progression, quests,
construction, reservoirs and seals. Construction decodes before the spatial scene so derived colliders
are present for complete actor admission. The 16 MiB world bound and canonical re-encoding are mandatory.

[WorldStepCodecV19](../../src/OpenFantasy.Core/Worlds/WorldStepCodecV19.cs) selects this interpretation
without changing inherited action formats. The supported action family is the fixed-reservoir family,
including capacity-only runoff controls; scheduled transport, mining and assisted climbing reject.
[Schema three](../../src/OpenFantasy.Hosting/Worlds/WorldDatabaseSchemaThree.cs) remains the storage owner.
Exact recovery replays the retained genesis and accepted batches, checking state hashes, outcomes and
identity history. Altering a head policy and recomputing its local hash cannot rewrite accepted history.

[ComposedWorldOperator](../../src/OpenFantasy.Host/ComposedWorldOperator.cs) exposes
`--create-contained-reservoir-world <world.sqlite> <profile8-genesis.bin> <seal-policy.bin>`.
Ordinary execution, recorded-state inspection and crash-boundary probes use the existing authority.
These are privileged local operators, not player permissions or network authentication.

## Design boundary: fluid and structural composition

An operable gate requires finite port and shutter geometry, exact open/closed poses, swept clearance,
material identity, displaced volume, actor effort and interruption rules. A positive-thickness shutter
cannot fit invisibly between two complete adjacent nominal vessel interiors. Removing an unproved-gate
failure cannot supply those missing physical facts.

Wet structural composition binds actual member poses and condition, stationary liquid mass, support,
capacity changes and conserved spill/breach consequences in one publication order. A breached wall
requires an accounted fluid outcome rather than permanent refusal of all terrain modification.
Occupied wet space additionally requires body volume, immersion, buoyancy, exposure and escape rules.
Full clear-envelope reservation supplies none of those laws. Read-only calibration, fixed containment
and moving fluid therefore have explicit, separate authority boundaries.
