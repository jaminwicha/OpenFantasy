# Live material transaction evidence

## Authority and observation

[WorldStepProcessor](../../src/OpenFantasy.Core/Worlds/WorldStepProcessor.cs) interprets selected commands
against sequential candidates and admits all coupled owners before publication. Its explicit
`DecideWithMaterialEvidence` entrypoint observes those same decisions for the ordinary-care world
interpretation. Ordinary `Decide` avoids observation work. Neither entrypoint publishes storage;
[SqliteWorldAuthority](../../src/OpenFantasy.Hosting/Worlds/SqliteWorldAuthority.cs) owns durable publication.

The optional observations on [WorldStepDecision](../../src/OpenFantasy.Core/Worlds/WorldStepDecision.cs)
describe accepted gathering, excavation, construction, crafting, transfers, quest delivery and restorative
use. Partial mining and construction are work even when inventory and production do not change. An
acknowledgment, permission revocation, dialogue or movement is not a material action. Passive time,
effort recovery and fluid flow remain their own processes.

Observations contain scope, command, world position and immutable owner hashes. They retain no world
root, mutable candidate or chain of previous observations. A material observation is not an imported
source right, a content grant, a command authorization or evidence of durable publication. Its constructor
is internal to the authority implementation; a hash alone cannot recreate the authority that produced it.

## Transaction positions

The publication sequence names the selected world batch. The action ordinal names the command's
position in that batch's actor ordering, rather than the command-sorted receipt list. Rejected commands
can leave gaps. One world publication can contain multiple material actions; a world revision is not a
count of material operations.

Before/after pins describe the running candidate around one action, after its coupled reconciliation and
complete admission. They distinguish the actual shared inventory, production, gathering effort, terrain,
construction, spatial components, quest, progression, care and liquid owners. The spatial component
digest omits derived installed structures, which the construction pin covers. Later accepted actions see
the preceding candidate. Failed complete admission restores the entire candidate and produces no
material observation, even if an isolated domain planner had accepted its part.

The final `Candidate` belongs to the complete batch. Exploration time, idle recovery, fluid redistribution
and movement follow the action boundary once, so the last action's pins need not equal the final world's
owner hashes. An observation must not replay those passive processes a second time. Aftermath remedy
observation follows the same successful complete-world admission and canonical-envelope check in
[WorldCareProcessor](../../src/OpenFantasy.Core/Worlds/WorldCareProcessor.cs).

A resource-history cut can prepare an equivalent active representation before command interpretation.
Its sealed segment identifies the original closing root; action observations identify the prepared
interpretation used for the action. These are distinct boundaries, even when their gameplay state agrees.
The decision carries the seal and observations together.

Resource-history, pacing and lifecycle hashes are not fields in these material-owner pins. They are
not a complete intermediate-world digest. Authenticating an intermediate resource or scheduling
transition requires an explicit extension; the full input/prepared root hashes and final admitted
world remain the enclosing authority.

## Archive integration contract

These observations establish the live transaction seam for the full graph: gathering, wet excavation,
installed construction/backing, quest funding, combat resources and care retain their actual owners.
They do not borrow a dry donor world or freeze a previous space while live terrain changes.

A continued material protocol must still qualify exact issued and retired intervals, surviving producer
lineage, installed reservations, pending obligations, historical identity occupancy and cumulative
operation counters. The [production frontier](production-continuation.md) and
[coordinated archive contract](resource-archive-continuation.md#extension-contract-coordinated-material-and-care-continuation)
describe those distinct obligations. Host recovery must regenerate the observations from authenticated
journal replay and actual retrieved closing bytes. Comparing an observation hash without verifying
those causes cannot admit a continued root.

Observation is a replay/qualification capability; canonical world, command, receipt and archive formats
retain their own versions and meanings. Requesting observation cannot upgrade a save, turn a rejected
command into an accepted one or enable a previously unsupported owner combination.

## Verified recorded observations

`SqliteWorldAuthority.ReadMaterialEvidenceForHost(sequence)` reconstructs the selected non-genesis
commit under the writer lock. [WorldJournalRecovery](../../src/OpenFantasy.Hosting/Worlds/WorldJournalRecovery.cs)
uses the same receipt, identity, checkpoint, archive and head verification as readiness. Only the selected
batch enables observation; replay continues through the complete source head before returning. Corruption
after the selected commit therefore invalidates the read. Failed storage verification requires recovery.

[RecordedWorldMaterialEvidence](../../src/OpenFantasy.Hosting/Worlds/RecordedWorldMaterialEvidence.cs)
retains the selected commit's before/after hashes, detached action observations and verified source-head
revision/hash. These pins distinguish the original journal boundary from an internally prepared resource
cut. Empty observations are legitimate for a recorded wait or rejected action; genesis and future
sequences are invalid requests. Results retain their original head pins when later commits occur.

The operation is synchronous privileged inspection, with the existing bounded full-journal cost. It must
not run on a player frame or confer player knowledge. It writes no save rows, introduces no schema or
migration, and does not turn a structurally consistent edited database into proof of trusted provenance.

The privileged observation reader remains bound to profile26. The explicit
[coordinated profile27](coordinated-world-continuation.md) uses its own full archive/replay path; this
diagnostic seam does not implicitly acquire nested continued-owner pinning or a new profile binding.
