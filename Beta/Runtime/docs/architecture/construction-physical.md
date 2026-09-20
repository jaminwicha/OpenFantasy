# Physical construction and fabrication history

## Separate ownership

[ConstructionSnapshot](../../src/OpenFantasy.Core/Construction/ConstructionSnapshot.cs) records admitted
projects, earned work and the completed material transformation. Its revision counts construction work.
[ConstructionPhysicalSnapshot](../../src/OpenFantasy.Core/Construction/ConstructionPhysicalSnapshot.cs)
records each completed member's condition, disposition and displacement, under its own revision. Damage
or movement does not undo earned work, replace the original definition or manufacture another installed
material lot.

The physical owner binds the exact construction content hash, world and branch. Every member of every
installed assembly has exactly one [ConstructionMemberState](../../src/OpenFantasy.Core/Construction/ConstructionMemberState.cs).
Unbuilt, unknown, duplicated or omitted members are rejected. The complete catalog is bounded by 32
projects with at most 32 members each. Stable member identities derive from the installed structure ID
and canonical member ordinal, independently of placement or condition.

`GetMemberReference` returns the original project, structure, member and sole installed-lot backing,
with exact volume and mass. Mass uses the shared [structural allocation](structural-loads.md), conserving
one installed output across all its members. A member reference is a physical allocation of that output,
not a separate spendable quantity-one lot. Condition, displacement and disposition leave its mass and
volume unchanged. Reclaiming, fragmenting or transforming material requires a corresponding ledger
contract; removing collision geometry is not permission to discard material.

## Member state and atomic changes

A new member has revision zero, full condition, zero displacement and `Standing` disposition. Condition
uses integer parts out of 10,000. Displacement is a signed spatial-subunit vector relative to the original
project site. `Standing` retains the original pose; `Resting` can describe a supplied displaced pose, while
`Falling` removes the member's actor-bearing surface. A disposition describes retained domain state; it
is not proof of a fall path, impact, structural capacity or settled contact.

`WithState` advances one member revision with an actual change. `WithMembers` applies a nonempty unique
subset atomically under one owner revision; every changed member advances exactly once. This allows a
coupled consequence to change several members without inventing separate world commits. Stale revisions,
no-op transitions and numeric overflow are rejected. Each member revision is bounded by its owner
revision; their sum need not be, because one owner transition can change multiple members.

`ReconcileCompletions` retains the exact admitted definition/project catalogs and stable project bindings,
requires monotonic work, and preserves every existing installed source. New completions add their full
pristine member set. Partial work updates the construction dependency pin without fabricating members
or incrementing physical revision. Existing condition and placement survive rebinding. A completed
assembly cannot disappear or acquire different source material through reconciliation.

## Exact geometry projections

Material boxes derive from the original exact millimetre member and displaced site. Exact signed tick
coordinates detect physical interpenetration across the complete member set. Rounded collision boxes
remain a conservative projection: their overlap within one assembly does not imply overlapping material.
Members remain grouped by their original structure identity, preserving the union interpretation and
avoiding false collisions from splitting adjacent rounded envelopes into unrelated structures.

Every member supplies collision geometry, including zero-condition and falling material. Only positive-
condition `Standing` or `Resting` members supply exact top surfaces. A surface is still only a contact
candidate; load rating and whole-actor footing require their separate checks. Existing 32-box and
2,048-subunit aggregate-span limits remain explicit. Unsupported projections fail rather than dropping
parts to fit. Widely separated fragments require an explicit collision-grouping extension.

At complete stable-world admission, `ValidateStableGeometry` partitions each assembly into exact
positive-face connected components. Every component's lowest physical members require direct terrain
support under the existing conservative collider rule. The outward-rounded bottom can differ from the
physical bottom by less than one subunit; this is an explicit quantization bound, not a new force law.
Testing only the original assembly's aggregate minimum height would incorrectly permit detached floating
parts. `Falling` states are valid domain values but are excluded from this stable world profile; an airborne
publication requires its own complete motion and scheduling contract. Cross-assembly resting support is
not inferred by grouping disconnected geometry under an old structure ID.

The physical owner validates supplied material and geometry. It does not validate trajectories, terrain
clearance, actor displacement, force balance or natural-strata stability. A complete world also admits
its [local occupancy](constructed-footing.md); actor bodies retain full support and exclusive space.
Publishing an unsupported intermediate pose cannot be justified by merely marking a member `Falling`.

## Runtime completion transaction

[WorldConstructionPhysicalAdmission](../../src/OpenFantasy.Core/Worlds/WorldConstructionPhysicalAdmission.cs)
creates profile thirteen from an unplayed, unheld profile-twelve dry genesis. Its admission pin includes
the previous interpretation and canonical initial physical owner. Normal loading does not migrate saves.
The root requires exact fabrication ownership, stepped support and the complete dry excavation profile.
Physical revision cannot exceed committed world revision, and successors retain the same owner presence.

[WorldStepProcessor](../../src/OpenFantasy.Core/Worlds/WorldStepProcessor.cs) reconciles physical state in
the ordinary candidate action gate. Completion publishes material, work, geometry and all member
identities together. Newly derived identities are checked against current and historical roles and other
created identities before accepting the action. Any collision restores the complete action candidate,
including physical state, material, construction, geometry and work expenditure. The selected advancing
step still contributes its ordinary calendar/exposure/effort recovery once. Receipts retain the refusal.

Candidate action admission rechecks stable component contact before issuing created identities or a
successful receipt. Excavation also checks its proposed complete hole before spending even partial work.
Removing terrain beneath a separated higher component therefore produces an ordinary refusal; the
legacy aggregate assembly minimum cannot defer that failure until final world construction. This
support refusal preserves the world until a complete collapse consequence can be published.

[WorldAdmissionValidator](../../src/OpenFantasy.Core/Worlds/WorldAdmissionValidator.cs) registers each
physical member as a distinct role. Original structure, material, batch and completion-cause roles remain
intact. Construction validates its historical live reserved output and production provenance, then compares
spatial geometry with the physical projection. Profiles without a physical owner retain their original
full-definition geometry contract.

The command path creates physical members through real construction completion and preserves them
through other actions and travel. Damage/displacement transitions are domain operations; they are not
actor commands in this profile. The [static load binder](structural-loads.md) consumes retained condition
for standing assemblies, rejects external condition overrides, and refuses detached material requiring a
new contact interpretation. Neither completion reconciliation nor that standalone model is a live
structural-load gate over every action and movement by itself. The separate
[structural world profile](structural-policy-and-motion.md#durable-stable-world-authority) owns the full
immutable policy and validates all current action/movement loads. It charges builder-carried project stock
and byproducts to their builder and installed backing to members exactly once. Stable-load refusal preserves
pre-action state and records a terminal rejection receipt, without queuing an executable action or consequence
job. Physical consequences still require complete atomic collapse publication.

## Canonical persistence and extension boundary

[ConstructionPhysicalCodecV1](../../src/OpenFantasy.Core/Construction/ConstructionPhysicalCodecV1.cs) stores
`construction.physical.v1`: construction hash, world ID, branch ID, physical revision and a canonical
structure-ID/member-ordinal collection. Each row stores structure ID, ordinal, member revision, condition,
signed displacement XYZ and disposition. The row identity and original mass/volume are derived from
trusted construction. Decode requires that exact construction snapshot, validates all physical invariants
and exact re-encoding, and keeps a 256 KiB/1,024-member bound.

[WorldSnapshotCodecV13](../../src/OpenFantasy.Core/Worlds/WorldSnapshotCodecV13.cs) uses an 18-field direct
root, retaining the first seventeen field positions of profile twelve and appending physical state.
Decode reads fabrication and its physical owner before deriving structures and admitting spatial version
three actors. It never validates an intermediate actor state against obsolete intact geometry. Older
world and spatial formats keep their bytes and meanings. The 16 MiB complete-world bound and schema-three
exclusive writer, receipt/identity journal, replay and recorded history remain in force.

Live collapse requires a further complete consequence contract: durable force policy, affected dependency
closure, pending work without extra simulation time, terrain removal, actual fracture/debris ownership,
recomputed actor contact, checked motion/landing, material and actor effects, and one atomic publication.
Pending-job basis pins must exclude their own changing cursor. Revalidating only mining is insufficient:
movement, carried goods, crafting, gathering and transfers can change loads. Collision grouping, resting
material load paths, trajectory/impact laws and wet containment must remain explicit rather than inferred
from this lifecycle representation.

The [vertical descent query](structural-policy-and-motion.md#first-contact-and-the-swept-prefix) checks
complete supplied bodies against remaining material and occupants without admitting an unsupported
intermediate world. Physical ownership must retain and reintroduce each moved member; a query's selected
obstacle set is not permission to erase material or publish a landing without load/impact checks.
