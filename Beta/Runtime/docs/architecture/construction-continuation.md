# Construction across material boundaries

## Ownership and explicit interpretation

Construction work, installed material, physical members and spatial occupancy retain their separate
canonical owners. A material boundary changes historical representation; it does not complete work,
reserve new stock, recreate a building, reset condition or advance a clock. The
[continued production owner](production-continuation.md) must preserve the same live guard lots and
exact historical producer facts used by [physical construction](construction-physical.md).

`ProductionSnapshot.ContinueConstruction` explicitly selects V5 from the actual opaque captured
construction state and closing material owners, with its construction-capable context.
V1–V4 keep their original interpretations and refuse construction-capable owners. Subsequent cuts
preserve that capability and its exact historical backing; an ordinary material cut cannot silently
downgrade the owner or erase installation evidence.

Reserved installation has a distinct material interpretation. Ordinary crafting spends unreserved
inputs and creates unreserved outputs. Construction instead spends the exact project-reserved raw
stock and creates its one reserved installed output. These permissions cannot be inferred from a
recipe match or generalized to ordinary craft, consumption or transfer.

## Current work and actual time

[MaterialConstructionState](../../src/OpenFantasy.Core/Construction/MaterialConstructionState.cs)
captures fabrication and its optional physical owner from an admitted source world. Its bounded
per-builder record identifies the last accepted world revision, advancing step, process interval and
experienced-time boundary. These records qualify fresh work; they are not another simulation clock.
The state retains no prior world, previous state chain or recursive production ancestry. It pins the
source admission and its construction interpretation, so equal world/branch IDs do not authorize
substituting another admitted source configuration.

A work action requires a later actual source-world frame and enough new builder exposure. Calling a
planner repeatedly with an advancing flag cannot create more work from the same frame. Accepted
partial work changes fabrication progress and the builder's accepted boundary, without creating a
material batch, retiring stock or adding physical members. The optional physical owner rebinds its
construction dependency hash while preserving physical revision and every existing member.
Historical command occupation remains
separate from the clock check; an old command cannot become new work after time advances.

## Coupled completion and refusal

[MaterialConstructionPlanner](../../src/OpenFantasy.Core/Construction/MaterialConstructionPlanner.cs)
requires current actor and terrain facts from the actual source-world spatial frame; only the
explicitly continued construction projection can differ. Fresh time cannot make an old actor stance
or stale terrain authoritative. It uses the original construction qualification rules for ownership, quest supply, manipulation,
locomotion, health, tool, exact tokens, work stance, reach, required access and placement. Completion
must qualify all resulting material, fabrication, physical and spatial owners together. Existing
physical members retain their original backing, condition and placement; new completion adds its
members once. Complete stable support and occupancy remain separate from a material recipe proof.

[MaterialConstructionPlan](../../src/OpenFantasy.Core/Construction/MaterialConstructionPlan.cs)
returns the complete candidate owner set. Any refusal preserves the actual supplied inventory,
production, construction state, physical owner and space. It cannot publish a partially spent bill,
a work increment without its required material completion, or an unsupported new structure. Created
command, batch, output, structure and member identities retain their respective authority checks.

## Completion evidence and historical backing

[ConstructionCompletionEvidence](../../src/OpenFantasy.Core/Construction/ConstructionCompletionEvidence.cs)
is produced by actual accepted qualification. It binds the command, current world and spatial context,
material owners, construction change, exact batch, reservation, installed output, structure and time.
It is a bounded proof dependency, not a retained world or a generic right to spend reserved material.
Material replay must reproduce exact reserved input retirement and installed output creation; a
successful component decode still requires validation against the actual inventory and construction.

Installed producers require more than a digest. Construction validates the original completion
command, actor, custodian, recipe/version and every exact source ingredient, alongside the live guard
lot's identity, revision, reservation, custodian and output interval. Historical batch records must
match their captured producer witness. The context retains at most 32 original batches, one per
bounded project, with at most 64 direct ingredients each. Only direct source batches are needed for this
construction interpretation; there is no recursive producer ancestry in those records.

The construction recipe catalog is frozen with V5 admission and retained across every cut. Ordinary
crafting cannot use these installation-only recipes. Every active or imported construction producer,
including generic imported producer witnesses, must belong to one completed project. A project cannot
claim a different batch, share another project's guard or retain raw stock under its completed project
reservation. Installed backing remains one material output even when many physical members allocate
its mass and volume; byproducts remain ordinary carried contents.

## Complete-world and persistence boundary

These are explicit standalone composition contracts. Existing complete-world profiles and their
legacy construction planner retain their continued-production guards. Actual source frames establish
the component's current actor/quest/time context; they do not publish the continued component owners
as a new world or authenticate an archive.

Durable continuation must admit the complete material, care, resource, terrain, construction and liquid
interpretation, retrieve and verify real historical bytes, preserve permanent identity roles and publish
all affected owners atomically. It must also preserve or reconstruct the work qualification boundary
from authenticated state. Opaque in-memory evidence or a bounded historical batch set does not replace
that protocol. See [resource archive continuation](resource-archive-continuation.md).
