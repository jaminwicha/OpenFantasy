# Partition dependency closure

## Current composition and ownership

[WorldSnapshot](../../src/OpenFantasy.Core/Worlds/WorldSnapshot.cs) admits interacting typed owners.
The [complete outcome-continuation codec](../../src/OpenFantasy.Core/Worlds/WorldSnapshotCodecV35.cs)
encodes delegated-control and party-withdrawal worlds through explicit version dispatch. Its nested
owners are not interchangeable optional facets, and a storage format cannot change their legal
combinations. [Typed ownership](partition-ownership.md) defines stable keys and admission cohorts;
[persistence](persistence-and-history.md) defines original history and exact interpretation.

Distinguish four kinds of information:

| Kind | Meaning | Examples |
| --- | --- | --- |
| Current canonical fact | Changes only through its admitted operation and atomic publication | Live counted/transformed holdings; actor condition; project work; active control assignment |
| Immutable definition | Pins the rules used to interpret facts | Body/recipe/blueprint, remedy, market policy, recovery and withdrawal rules |
| Derived view | Recomputed from canonical owners and validated bindings | Installed colliders, occupancy/indexes, liquid topology, due/required encounter selections |
| Historical witness | Preserves a specific past frontier and the context needed to verify it | Continuation storages/batches, patient witnesses, source baseline, archive-bound progression |

A historical storage or progression witness is not a second mutable inventory or stat owner. A
reconstruction may need both current facts and historical evidence; their scopes and revisions remain
explicit. Omitting the witness because a current fact looks similar loses history. Updating the witness
as if it were current state breaks the archive commitment.

## Coupled cohorts

| Operation/domain | Dependencies that remain coupled |
| --- | --- |
| Custody/material transformation | Holder policies, source/output intervals and rights, reservations, combined capacities, production/consumption provenance and historical identity exclusion |
| Care | Patient condition/progression, permission/agreement/request, distinct acknowledgment/treatment/disposition, helper timing, finite consumed resource and carried closed/pending witnesses |
| Trade | Captured quote/acceptance or proposal terms, both custodians/accounts, finite provenance, issuer revision, sibling outcomes and exact expiry; private observations/disclosures retain their audience |
| Encounter/Wait | Shared timing actors and required decisions, actual pending commands, readiness/resource costs, random counters, lifecycle scenario and terminal outcome |
| Retreat/withdrawal | Original command and route, scope/side/required members, each member's actual position/speed/progress and costs, cancellations and terminal reasons |
| Delegated control | Principal/session ownership, offered and accepted permission terms, controller mode/disconnect, acquired assignment and exact witness ancestry |
| Terrain/liquid/construction | Exact terrain, reservoir network/capacity/provenance, seal policy, topology/placement, excavation, installed backing and required access |
| Coordinated history | One archive reference/boundary/admission and matched production/care/resource/geography/proposal/paid/market continuation contexts |

`WorldMaterialCareArchiveAnchor` binds these coordinated contexts to one historical boundary.
The stored binding identifies that boundary; the actual anchor provides the decoding context.
Separating payload storage must not let these frontiers advance independently. Actor control also has
multiple distinct owners: an accepted permission is not an active assignment, and a disconnected
controller does not automatically authorize a different actor.

## Construction qualification and due closure

[ConstructionWorkKernel](../../src/OpenFantasy.Core/Construction/ConstructionWorkKernel.cs) checks
builder/control/ownership, project and custodian policy, actor capability/HP, quest completion, tool,
exact revisions, location/reach, reservation-wide stock and required-access actors before awarding
work. It checks placement before partial progress as well as completion. Completion also admits
spawned identities, finite transformation, combined capacity and installed material backing.

A selective loader must establish the complete read dependency set for those checks. A positive
reservation lookup does not prove that no conflicting stock exists elsewhere. Any absence or global
uniqueness claim requires an admitted, version-bound index/witness or complete relevant data. A
projection cannot create authority merely by leaving out a conflicting holder or blocked access actor.

The operation's write set is not its read cohort, and neither defines its due-process cohort.
An advancing construction step also advances the applicable calendar, gathering and resident exposure.
[ConstructionLocalityState](../../src/OpenFantasy.Core/Construction/ConstructionLocalityState.cs)
retains those owners and follows the ordinary process order. The typed schema14 actor image contains
resident exposure, so affected actor images change even when their holdings and physical presence do
not. Loading only builder/project images cannot implement this law faithfully.

An admitted immutable interval may retain exact dependency witnesses and omit unrelated detailed
payloads, provided the sole writer checks freshness and invalidates the interval whenever an
intervening operation changes a dependency. Final installation admits its larger geometry/material
cohort. Missing, stale, foreign or unsupported dependencies stop preparation before publication;
no silent empty-owner substitution or approximate offscreen reward is valid.

## Extension and qualification contract

Keep current-game nested owners and alternate historical profile interpretations in the mapping when
extending storage. A narrow legacy profile cannot prove support for continued provenance, pending
care, market knowledge, collective withdrawal or delegated control. Whole-world constructors and
original interpreters remain the admission oracle until an exact smaller proof is qualified.

Selective-loading evidence must show actual stored reads and retained-detail absence, exact ordinary
results/receipts, full original history, due-process completeness and atomic faults. A requested-hash
list, interface extraction or lower allocation in an isolated helper alone does not establish that
contract. Full directory, witness/index and archival costs belong in measurement; closed history is
not disposable merely because current work does not read all of it.
