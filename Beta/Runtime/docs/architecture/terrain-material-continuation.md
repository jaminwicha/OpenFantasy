# Terrain ownership across material history boundaries

## One shared material owner

The [terrain material registry](../../src/OpenFantasy.Core/Geography/TerrainMaterialRegistry.cs) binds
one counted inventory and Production owner to a fixed terrain basis and complete allocation catalog.
Its single-allocation states are views of that shared ownership. Continuing material history preserves
those facts; it does not admit another deposit, mint raw stock or detach a neighboring allocation.

`PlanContinueTerrain` derives the terrain-capable interpretation from the actual closing registry.
The material context retains a bounded [representation boundary](../../src/OpenFantasy.Core/Geography/TerrainRepresentationBoundary.cs)
of its fixed catalog and exact active-cell bindings. Subsequent cuts preserve the selected capabilities. The observation contains no parent
registry, inventory owner or recursive history chain. A generic Production context cannot supply the
historical geographic qualification that only the closing registry established.

Once terrain capability is selected, generic `ProductionSnapshot.Continue`, `ContinueMaterial` and
`ContinueConstruction` cannot cut the owner independently. The registry supplies the qualified cut.
A materially valid action can violate a geographic role or custodian rule; cutting its Production
alone must not erase the offending record before terrain validation. Ordinary material operations
remain available, and the registry validates their complete candidates before its next boundary.

A cut changes the material interpretation and complete state hash while preserving custody, storage
revisions, live rights, quantities and cell bindings. It records no action and advances no world or
process clock. Plans captured before the cut are stale even when their logical revision is unchanged.
Ordinary reconciliation preserves the same continuation context and complete retained operation
prefix; it cannot silently replace the boundary or drop history.

## Source coverage and historical constraints

Every allocated interval satisfies the same exact partition:

`allocated = coarse + detailed + other live raw + directly retired raw`

Imported direct raw retirement retains its source interval, item pin and retiring custodian. Current
recipe and consumption records contribute only their direct raw ingredients. Recrafting or consuming
a transformed output does not retire its original raw ancestry a second time. Historical lot revisions
are not invented for imported ranges; their original legality is qualified by the actual closing
owner, while current records retain their ordinary revision checks.

The complete registry forbids retiring allocated source intervals in any of its coarse or detailed
custodians, including another allocation's representation custodian. This check applies to imported
ranges and current records. Transferring material out remains necessary before crafting or consumption;
it does not itself prove mining labor, spatial access or excavation permission.

Permanent historical identity lookup remains a caller obligation for new operations. Archiving
records never releases a command, cause, batch or lot identity for reuse. Current registry roles and
retained material roles supplement that lookup rather than replacing it.

## Representation operations

Refinement and demotion are distinct typed material operations. They retain the existing geometric,
funding, protection, reservation and finite-capacity rules. Refinement splits currently coarse-owned
intervals into selected detailed cells. Demotion removes selected active bindings and can coalesce
compatible coarse intervals. It cannot restore previously extracted or retired units.

One accepted operation advances custody and both representation-storage revisions once. Its material
history entry advances the Production revision once while leaving produced outputs, batches and
consumption counters unchanged. Other storages remain exact. An empty active cell can demote without
creating a lot; its binding change, cause and storage revisions are still recorded.

The canonical partition kernel accounts for every affected lot, including coarse neighbors coalesced
outside the moved intervals. Changed lots receive fresh deterministic identities; untouched lots retain
their exact metadata. The typed operation's [opaque evidence](../../src/OpenFantasy.Core/Geography/TerrainRepresentationEvidence.cs)
joins the before/after material and representation facts.
Replay cannot substitute an arbitrary inventory mutation merely because aggregate quantities balance.
Single-state planning cannot bypass the complete registry's cross-allocation checks.

The [material replay verifier](../../src/OpenFantasy.Core/Production/Continuation/MaterialContinuationReplay.cs)
reconstructs inventory and produced lots in operation order. Its preceding-state hash uses the same
canonical writer as ordinary Production encoding, with sorted bounded collections and checked counters.
It observes already replayed values without recursively reconstructing earlier snapshots. Complete-owner
validation, each operation's permission and identity checks, intermediate capacity checks and encoded
byte limits remain necessary; the hash observer cannot grant admission.

## Archives and publication

Explicit terrain state and registry V4 archives retain Production V6 and require the actual supplied
terrain context and matching opaque operation evidence through
[terrain dependencies](../../src/OpenFantasy.Core/Geography/TerrainProductionDependencies.cs).
Registry reconstruction validates the complete
catalog and active bindings, exact source coverage, material replay and canonical bytes. A state view
cannot grant permission to reconstruct an unrelated whole registry. Inline terrain V1/V2/V3 and
Production V1 through V5 retain their own interpretations and reject unsupported terrain continuation.

Opaque component evidence is not durable archive authentication. The integrating world must retrieve
and validate actual closing history and permanent identities before replay can establish authority.
The registry remains separate from lived terrain collision, mining effort/source issuance, reserved
liquid transport and complete-world care/resource continuation. Its standalone plans and archives do
not grant those permissions or migrate an older world profile.
