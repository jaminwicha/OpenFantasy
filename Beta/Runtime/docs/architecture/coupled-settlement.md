# Coupled intact settlement

## Admission and support ownership

[WorldCoupledSettlementAdmission](../../src/OpenFantasy.Core/Worlds/WorldCoupledSettlementAdmission.cs)
admits an unplayed, unheld profile-sixteen world into profile seventeen. The admission pin includes the
prior admission, complete owned load policy and [CoupledSettlementPolicy](../../src/OpenFantasy.Core/Structures/CoupledSettlementPolicy.cs).
No played-world migration or policy-editing action is implicit. Ordinary successors preserve all policies.

[StructureSupportMode](../../src/OpenFantasy.Core/Spatial/StructureSupportMode.cs) distinguishes terrain-only
construction footing from terrain plus foreign construction. Actor footing keeps its separate stepped
interpretation. Full conservative base footprints may combine known terrain and exact foreign top patches;
the assembly's own material cannot supply its external support. Complete connected physical components
retain their supported lowest members. Conservative collision envelopes can refuse fractional fringe
layouts; their rounded edges cannot create physical contact.

Ordinary project placement, access qualification and fabrication remain responsible for earning installed
material. The retained world load policy additionally binds exact physical base/top contact through the
[contact model](structural-contact-assessment.md), retaining authored joints, material and carried weight.
Every admitted stable world requires an acyclic, failure-free complete graph. Read-only counterfactual
policy selection cannot replace this owned policy. An inactive rated foundation requires its exact
completed excavation owner, terrain source and material. Proposed holes are permissible only in a
diagnostic assessment; routing through shared contact arithmetic does not grant that permission to a world.

## Moving group and collision scene

[ConstructionSettlementScene](../../src/OpenFantasy.Core/Structures/ConstructionSettlementScene.cs) captures
the complete job-free world and proposed terrain. Its coupled input pin includes that full world, the
assembly policy and all candidate region hashes. Losing full support beneath an assembly's lowest collision boxes seeds that moving assembly.
A disconnected higher component remains subject to final per-component admission; losing only its support
can conservatively refuse instead of creating a new independently moving fragment.
The original owned bearing graph expands this set upward through supporting-to-supported member links,
including every member of each touched rigid assembly. This moving set is distinct from the conservative
bidirectional diagnostic load neighborhood: stationary alternate supports remain in the collision scene.

The admitted group has one uniform, positive vertical displacement. All members retain their original
relative positions, identities, installed backing and conditions. Exact subunit contact planes and uniform
prior vertical poses are required. A base with a retained exact bearing onto another moving assembly travels with it; rounded collider
fringes do not select carried bases.
External bases independently seek full first contact and must agree on the same drop. Every member still
sweeps against all retained outside terrain, structures and bodies; a partial remaining ledge or stationary
alternate support cannot be skipped. Unequal landing distances, unresolved contact and unknown terrain
refuse the whole candidate.

Riders require complete exact footprint coverage by moving surfaces at their feet; coverage may span
multiple moving assemblies. Partial coverage refuses rather than silently assigning an occupant to a group.
Only those actual riders and moving assemblies are removed from the sweep scene. Every member and rider
requires a clear complete swept trajectory, and all existing harmless-distance and flight-time limits apply.
Actors supply obstacles, never structural landing floors.

The physical owner publishes all member revisions in one `WithMembers` transition. Riders translate once,
keeping body, condition, custody and identity. Settled material is present before independently unsupported
actors seek a landing. The final complete scene passes clearance, exact support and physical admission;
world publication additionally checks the complete new load graph, including newly issued carried material.
A failed final load, anchor or material check refuses the original action without publishing partial motion.

## Durable work and byte contracts

[ConstructionSettlementResolution](../../src/OpenFantasy.Core/Structures/ConstructionSettlementResolution.cs)
uses the same actual base/member/rider/actor work phases as single-assembly settlement. For an admitted
maximum of `A` assemblies (1 through 32), its proved geometric bound is `2*A*32 + 2*64` units: at most
32 base queries and 32 member sweeps per assembly, plus 64 rider sweeps and 64 actor units. Setup scans,
complete-root hashing, array copies, restoration and final complete-load admission are outside this query
budget. The bound is not a wall-clock guarantee.

[Resolution codec V2](../../src/OpenFantasy.Core/Structures/ConstructionSettlementResolutionCodecV2.cs)
retains an input pin, work count and status. Restoration reconstructs the exact prefix from the complete
stable basis and verifies canonical bytes. [Pending codec V2](../../src/OpenFantasy.Core/Worlds/PendingExcavationCodecV2.cs)
retains the original command, cause, revision/hash and continuation count. Dynamic source/result identity
reservations and journal headroom derive from this admitted work bound; earlier profile sixteen retains
its fixed 192-unit meaning and V1 archives.

[World snapshot V17](../../src/OpenFantasy.Core/Worlds/WorldSnapshotCodecV17.cs) is a direct 23-field root:
profile sixteen's component order, the new pending archive, and the appended coupled policy. Spatial V4
explicitly implies stepped actor footing and foreign construction support. Earlier spatial versions refuse
the new construction interpretation. Schema four and commit kind four serve profiles sixteen and seventeen;
ordinary batch encoding and headroom sizing retain the actual profile. Existing root/journal size limits,
exclusive writer and atomic ledger/checkpoint transaction remain in force.

The [consequence owner](consequence-continuation.md) still isolates one excavation, preserves stable state
through every saved work prefix and publishes its sole terminal receipt with one advancing interval.
Session restart resumes pending work before readiness; read-only recovery and history do not advance it.

## Domain boundaries

This interpretation owns intact groups with a shared harmless vertical trajectory. It does not calculate
unequal interacting trajectories, fracture, debris, deforming joints, momentum, harmful impact, long flight
or liquid displacement. Conservatively refusing such geometry preserves those separate ownership contracts.
A separate [event-descent assessment](event-descent.md) resolves successive stopping events against retained
obstacles and reports impact demand. Its diagnostic result does not change this profile's live resolver,
pending-work bound, command authorization or harmless-translation byte interpretation.
A finite local group does not establish world-wide asynchronous simulation, production latency, multiplayer
pacing, rendered motion or presentation quality.
