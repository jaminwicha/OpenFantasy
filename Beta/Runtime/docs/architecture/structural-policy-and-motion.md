# Structural policy and vertical motion queries

## Explicit policy asset

[ConstructionLoadPolicy](../../src/OpenFantasy.Core/Structures/ConstructionLoadPolicy.cs) bundles an exact
terrain scope, complete construction-bearing templates, stable terrain anchors and body/item mass
calibration. The immutable asset has a content hash over all values. Catalogs retain the existing bounds:
16 construction definitions, 4,096 declared foundations, 64 body definitions and 256 supplemental items.
Foundations have distinct element IDs, source IDs and cells. Scope, foundation, source and definition
roles cannot alias; shared material definitions must retain one exact pin. Foundation cells must project
into the signed spatial-coordinate range without overflow.

Every template must have an acyclic load order at policy construction, including templates for unbuilt
definitions. A bounded topological traversal checks all member joints. Unsupported or weak acyclic
assemblies remain valid policy inputs whose failure is determined by load resolution. The 4,096-element
graph limit is shared by foundations, installed members and actors; it is not a separate allowance for
each catalog. Binding rejects a composition that exceeds that combined limit.

`Bind(world)` requires the same world, branch and realm. Every admitted construction definition must have
exactly one template, including definitions without an installed project. Complete geometry, pin, span
and joint validation precedes filtering templates to installed assemblies for the existing
[load model](structural-loads.md). The model validates retained terrain sources/materials, actual body
versions and carried item masses. Policy callers cannot substitute member condition; a world with a
physical member owner supplies its retained standing-member condition.

The asset is an authored interpretation, not a finding that all solid terrain is bedrock or that its
capacities represent production balance. Binding by itself remains a read-only assessment. The structural
world profile below gives it explicit root ownership and a complete stable action/movement gate.
An enclosing pending-work owner must pin the full policy hash, not merely the subset of ratings that
happens to affect the current graph. A model bound to an older root with external ratings does not gain
that complete dependency through `InputPin`. In a structural root, `InputPin` includes `WorldHash`, which
already includes the entire owned policy. A pending owner's stable basis must retain that full dependency
while avoiding a changing work cursor. Inactive definitions and future carried goods remain policy inputs.

[ConstructionLoadPolicyCodecV1](../../src/OpenFantasy.Core/Structures/ConstructionLoadPolicyCodecV1.cs)
retains a six-field `construction.load-policy.v1` frame: tag, terrain scope, complete templates,
foundations, body masses and supplemental item masses. Rows use stable identity order; member ordinals
and joint pairs retain their template ordering. Identities use sixteen bytes, content pins thirty-two,
numbers unsigned big-endian 64-bit and cell coordinates signed big-endian 64-bit. Full template rows have
the same encoding used for their version hash. The archive is bounded to 4 MiB and every nested catalog
has its own count/row-byte bound.

Decode requires a separately expected content hash, checks the actual archive against it, validates all
values and requires exact re-encoding. A matching supplied hash does not waive canonical order, field
counts, duplicate roles or numeric limits. Cancellation is observed while reading catalogs. The expected
hash is an integrity dependency, not authentication of an arbitrary caller's policy selection.

## Durable stable-world authority

[WorldStructuralPolicyAdmission](../../src/OpenFantasy.Core/Worlds/WorldStructuralPolicyAdmission.cs)
admits an immutable full policy into an unplayed, unheld physical construction world. Its admission pin
includes the prior interpretation and full policy hash. This explicit fresh-world path does not migrate
existing saves. The root owns `StructuralPolicy`; ordinary successors retain the exact asset and complete
world revision. No actor command changes policy values or substitutes a diagnostic catalog.

[WorldStructuralBinding](../../src/OpenFantasy.Core/Worlds/WorldStructuralBinding.cs) requires stable
complete current loads at root admission. Every live raw/transformed lot needs a resident's primary
inventory or an explicitly builder-carried project custodian. Body mass and carried project stock,
including reservations, feed that builder's load. Installed backing is counted through member allocation
instead; remaining project byproducts stay carried. This separates material custody from physical load
location and prevents delivery from making stock weightless. Unlocated storage requires its own spatial
load contract and is refused. Older diagnostic profiles retain their primary-inventory interpretation.

The binding resolves one failure-free graph pass; any failure refuses a stable root immediately. It need
not compute cascading consequences to establish that the supplied state is unstable. This remains bounded
whole-scene work, including graph construction and canonical hashing, with no claim of constant frame cost.
Full policy catalogs, unused calibration pins, foundations and derived actor-load identities participate
in root identity admission. Foundation and actor-load IDs are genesis-owned roles, not newly issued
command outcomes. They cannot alias a later command, generated lot or physical member.

[WorldStepProcessor](../../src/OpenFantasy.Core/Worlds/WorldStepProcessor.cs) checks each complete action
candidate before receipts and generated identities are accepted. Its check includes every occupant,
current carried goods, production, construction and reconciled physical members. A refused action restores
all of its side effects, including partial work, custody, source issuance, dialogue and component revisions.
Ordinary movement checks current post-action loads after each actor's proposed move, preserving earlier
accepted movement while refusing an unstable destination. The selected advancing step still contributes
one calendar/exposure/effort recovery; a held interaction retains held timing. Final publication repeats
the same complete root admission, so a local action check cannot bypass the saved-world invariant.

These are terminal refusals that preserve stable authority; they do not create pending consequence work.
They are not the design's physical response to overload. Coupled collapse needs a transaction owner that
intercepts such unstable candidates and publishes all consequences together; a refusal is not a completed fall.

[WorldSnapshotCodecV14](../../src/OpenFantasy.Core/Worlds/WorldSnapshotCodecV14.cs) retains a direct
19-field root, preserving the first eighteen field positions and appending the full canonical policy.
Decode derives physical structures before actors, checks the nested policy frame and then complete
root semantics. The inner hash is computed from embedded bytes; whole-root hashes and journal replay
supply the retained integrity dependency. Inconsistent scope/definition pins, role collisions or unstable
loads fail semantic admission. An otherwise internally consistent replacement calibration can decode, but
changes the root hash and fails comparison with retained authoritative history. Encoding only serializes initialized fields;
it never invokes binding or persists the derived model's world-dependent pin. This avoids recursive
world hashing. The 16 MiB complete-world bound, schema-three exclusive writer and exact history remain
in force. Earlier formats keep their bytes and interpretation. Host diagnostics expose owned policy
hash/catalog counts without selecting replacement ratings.

## Candidate geometry without premature world admission

[VerticalDescentQuery](../../src/OpenFantasy.Core/Spatial/VerticalDescentQuery.cs) reads a copied finite
candidate scene: disjoint terrain regions under exact scope/seed/rules, remaining structure colliders
and corporeal actor bodies. Counts and body sizes use the local spatial limits. Unlike
[LocalSpatialSnapshot](../../src/OpenFantasy.Core/Spatial/LocalSpatialSnapshot.cs), this read-only query
can describe unsupported occupants while their consequences are being prepared. It neither admits a world
nor permits that intermediate geometry to be published.

`QueryActor` uses the complete supplied actor body and excludes only that actor's original occupancy.
Involuntary descent does not require locomotion or manipulation capability. `QueryBox` excludes no
material or actor. The owner must explicitly supply remaining obstacles when a member moves; ignoring
an entire structure ID could hide its other legs, walls or upper floors. Scene completeness, simultaneous
moving-body membership and preservation of their material belong to the enclosing transaction.

Queries preserve XY and body dimensions while translating vertically down by at most 16,384 subunits
(64 cells). Individual axes retain the existing 2,048-subunit limit. Positive range, bounded inputs,
checked endpoint arithmetic and wide extent/contact comparisons prevent unbounded or overflowing
geometry searches. Invalid scene/input catalogs throw; an unrepresentable queried horizon returns
`NumericLimit`. This finite work is not a frame-time or interruptible scheduling guarantee.

## First contact and the swept prefix

Starting penetration or missing starting clearance refuses the query. Candidate contact planes include
every solid terrain top, every remaining structure collider top and every stationary actor head with
positive XY footprint overlap. Nonbearing material, partial ledges and exact zero-drop contact participate.
The highest contact at or below the starting bottom is the first collision. Searching for the highest
*valid landing* would allow a body to tunnel through an earlier invalid contact.

The union of the starting and translated boxes is the exact swept AABB for this vertical motion. Every
terrain cell in that prefix must be present and clear, and it must clear remaining material and other
actors. Unknown terrain is never empty space. Missing terrain below an earlier completely known contact
is irrelevant to motion, except where the existing landing-support query requires that evidence.

At first contact, a stationary actor produces `ActorContact`; actors do not become floors. Other contacts
require exact full-footprint coverage at the same standing plane. The shared footing query unions terrain
and physical construction patches. Rounded collision fringes cannot supply material, and a stair-height
band cannot combine different planes into a false landing. An incomplete ledge or nonbearing collider
returns `IncompleteSupport` at that first contact. The query does not continue through it. At a cell-plane
landing, the existing footing contract requires known terrain below the entire footprint; it retains
that conservative dependency even where construction contributes support.

[VerticalDescentResult](../../src/OpenFantasy.Core/Spatial/VerticalDescentResult.cs) reports a checked
geometric prefix. `ReachedVolume` and `DropSubunits` describe the first contact or clear search horizon.
Only `Landed` proves full geometric footing; `NoContactWithinLimit` is a clear finite descent with no landing,
not a fabricated floor at the range limit. Unknown/invalid starting geometry has no reached volume.
Results do not advance time, apply impact, revise actors, alter inventory or certify load capacity.

## Complete collapse composition

A collapse owner combines the load-failure plan, surviving material, moved members and occupants. After
moving a member, its retained material must re-enter subsequent collision/contact queries at the new
placement; otherwise an occupant can incorrectly fall through the resulting debris. It must also resolve
member-to-actor contact and simultaneous trajectories, not treat a stationary-body refusal as a completed
impact response. Full-body clearance, lower exposed supports and conserved material cannot be inferred
from graph survival alone.

The enclosing contract also owns gravity/time integration, impact injuries and damage, resting load paths,
fracture/debris transformations, natural collateral sources, history and one atomic final publication.
Partial/actor contacts need a coupled response; a clear horizon needs retained airborne motion or another
bounded continuation. Policy and geometry queries cannot authorize unsupported intermediate roots.
Pending work must preserve the final excavation swing/source entitlement and avoid awarding extra world
exposure while solving. Refer to [physical construction](construction-physical.md),
[excavation](excavation.md) and [persistence](persistence-and-history.md) for these retained ownership rules.
