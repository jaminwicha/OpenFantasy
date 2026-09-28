# Production continuation

## Domain ownership

[ProductionSnapshot](../../src/OpenFantasy.Core/Production/ProductionSnapshot.cs) separates a complete
inline history from an explicitly continued owner. `Continue(inventory)` captures the actual supplied
boundary, retains its live lots, recipes, scope and cumulative revision, and opens empty bounded
batch/consumption collections. The original owners remain immutable. A cut is representation work;
it creates no item, transfer, material disposition, clock tick or actor resource effect.

The continuation context retains the [material basis](material-continuation-basis.md), exact recipe
admission and consumption interpretation, ordinal and cumulative operation counts at the boundary.
It does not retain a prior root, owner or recursive ancestor chain. Total batch/consumption counts
combine those bases with active records; ordinary operations advance the absolute production revision.
Collection bounds apply to the active window, while raw coverage and live-right bounds remain explicit.

The context also pins the bounded storage set and non-custody authority admission. Each storage
retains its capacity and advances its revision by the number of active operations spending from that
custodian. Unaffected storages stay exact. A normalized admission hash freezes actors, control/policy
bindings, sources, reservations and capacities while excluding changing live lots and custody/storage
revisions. Only that digest and bounded storage baselines are retained, not the complete inventory.
Changing those admissions requires its own qualified transition.

This is a structural material contract. The supplied inventory and context require independent
complete-world admission and authenticated history. A cut alone is not durable archiving: before
releasing full prior records, a complete authority must preserve and verify their retrievable bytes.

## Exact conservation across a cut

Imported output intervals must be partitioned exactly among surviving lots, active recipe inputs and
active consumption claims. Old spent prefixes and internal holes are outside the imported budget.
An active recipe proves its full output partition under the ordinary recipe laws; imported producers
are resolved historical dependencies rather than cycles in the active recipe graph.

Direct raw retirement combines the captured coverage with active raw dispositions once. Recrafting
an imported output does not retire its original raw ancestry again. Captured raw live rights must
remain fully accounted by live claims and active retirement, without overlap, loss or resurrection.
The standalone continued owner accepts no additional post-cut raw issuance. Gathering and excavation
need explicit source-owner continuation contracts before admitting new rights; capacity alone is not
such a grant. This boundary does not amend the game's requirement for renewable acquisition.

Surviving lot identities retain their exact provenance, item pins and revision lineage. Custody,
protection and reservations remain distinct from global output coverage. New operations use the same
selection, control, finite recipe and disposition rules as inline production.

## Permanent identities and publication

Continued crafting and consumption require a caller-supplied complete historical identity lookup.
Active state cannot remember every disposed command, batch, claim or lot; pruning their records must
not make those identities reusable. The planners check the requested command and created identities
against that lookup as well as the present owner. The lookup is an authority dependency, not proof
that a caller-provided predicate is honest.

Ordinary successor validation retains one exact continuation context and every active historical
record. A boundary cut is an explicit separate transition; append-only reconciliation cannot silently
accept a different context or discard retained evidence.

## Canonical representation

[ProductionCodecV3](../../src/OpenFantasy.Core/Production/ProductionCodecV3.cs) explicitly represents
continued production and binds its payload to the exact continuation context. Decoding requires that
context and exact canonical reconstruction. The context is obtained from its actual supplied closing
owners; arbitrary observation bytes have no public path to mint an imported grant.

Production V1/V2 retain their original bytes and meanings and refuse a continued owner. Opening an
old save does not reinterpret its capacity refusals or migrate it. A V3 component round trip neither
authenticates archived history nor selects a complete-world storage profile.

| Representation | Material records | Required decode context |
| --- | --- | --- |
| V1 | Complete inline batches and live outputs; consumption disabled | None |
| V2 | Complete inline batches, live outputs and consumed claims | None |
| V3 | Active batches/claims and live outputs; absolute totals and context hash | The exact captured continuation context, including its consumption interpretation |

V3 retains the 16 MiB production frame limit and the 256 active-batch/256 active-consumption bounds.
Its context carries at most 32 recipes, 256 storage baselines and the separately bounded material
basis. Raw coverage is normalized before checking its final 16,640-range envelope; temporary merge
input is bounded by the imported and active envelopes together. Neither canonical compaction nor a
small live window implies unlimited archive storage or qualified production throughput.

## Complete-world integration boundary

World admission requires one coordinated interpretation for material retirement, care causes and
resource effects. Terrain/excavation coverage, installed construction ingredients, nested liquid
reservations and new source issuance must recognize the same boundary. Existing world, construction,
terrain/runoff, source delivery, excavation and restorative-use entrypoints refuse unsupported
continued owners explicitly; legacy geography encoders cannot discard their context.

The complete protocol must schedule before accepted obligations lose headroom, preserve captured
command meanings, publish archive/receipt/identity/head changes atomically and recover from actual
retrieved bytes. See [resource archive continuation](resource-archive-continuation.md) and
[persistence and history](persistence-and-history.md). Those requirements are not discharged by a
standalone material planner or codec.
