# Production continuation

## Domain ownership

[ProductionSnapshot](../../src/OpenFantasy.Core/Production/ProductionSnapshot.cs) separates a complete
inline history from an explicitly continued owner. `Continue(inventory)` captures the actual supplied
boundary, retains its live lots, recipes, scope and cumulative revision, and opens empty bounded
batch/consumption collections. `ContinueMaterial(inventory, gathering)` explicitly selects ordered
material-operation interpretation; gathering admission is optional for custody-only use.
`ContinueConstruction(inventory, constructionState, gathering)` selects V5 reserved-installation semantics
and exact historical construction batches. Further cuts preserve the selected interpretation. The original owners remain immutable. A cut is representation work;
it creates no item, transfer, material disposition, clock tick or actor resource effect.

The continuation context retains the [material basis](material-continuation-basis.md), exact recipe
admission and consumption interpretation, ordinal and cumulative operation counts at the boundary.
It does not retain a prior root, owner or recursive ancestor chain. Total batch/consumption counts
combine those bases with active records; ordinary operations advance the absolute production revision.
Collection bounds apply to the active window, while raw coverage and live-right bounds remain explicit.
V3 records craft/consumption revisions. V4 also advances production revision for each explicitly
recorded gathering, transfer or delivery operation; its ordered history binds those custody changes.
V5 adds one material operation for a completed construction transform. Partial construction work
advances its own work owner without advancing material history.
V6 adds explicit [terrain representation](terrain-material-continuation.md), selected from an actual
funded registry. It can retain construction capability independently. Refinement/demotion advances
one material operation while preserving crafting and consumption totals; a cut itself adds no operation.
Terrain-capable owners require a qualified registry cut rather than the generic Production cut methods.
This preserves geographic legality before historical records leave the active segment; ordinary
material operations retain their own typed planners and are reconciled with the registry separately.

V7 selects [reserved transport](transport-material-continuation.md) from an actual complete transport
owner, preserving its journal, physical catalog and clock. Successful delivery adds one typed operation
with a cause distinct from its scheduled stage. Generic Production cuts cannot bypass transport
qualification. Optional gathering/construction capabilities compose with V7; terrain V6 does not.

V8 selects [excavation work](excavation-material-continuation.md) from an actual joined dry owner.
Every accepted partial/final mining or joined gathering swing advances the ordered material history.
Partial mining leaves custody and storage revisions unchanged. Shared effort, current terrain and
per-actor opportunity frontiers survive cuts. Static construction backing remains retained; independent
construction mutation, generic gathering and geographic interpretations cannot bypass that joined owner.

The context also pins the bounded storage set and non-custody authority admission. Each storage
retains its capacity and advances its revision for each operation that actually touches that custodian.
A transfer touches source and destination once; a multi-lot delivery also touches each storage once.
Unaffected storages stay exact. A normalized admission hash freezes actors, control/policy
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
V3 accepts no additional post-cut raw issuance. V4 admits gathering only through exact planner-produced
source evidence and its known cursor chain. V8 admits excavation only through the complete finite
site/work/terrain contract, including exact direct retirement after extraction. Capacity alone is not a grant.

Surviving lot identities retain their exact provenance, item pins and revision lineage. Custody,
protection and reservations remain distinct from global output coverage. V3 preserves their captured
values; V4 changes them only through the corresponding typed operation. New operations use the same
selection, control, finite recipe and disposition rules as inline production.

## Ordered custody and source operations

[MaterialOperationHistory](../../src/OpenFantasy.Core/Production/Continuation/MaterialOperationHistory.cs)
binds a bounded sequence of typed craft, consumption, gathering, raw transfer and source-delivery
operations inside the Production owner. It is historical evidence, not a second active inventory.
Raw live lots remain owned by `CountedAuthorityState`; transformed live lots remain in Production.
Craft and consumption entries reference their exact retained records. Operation order and absolute
revisions prevent a final aggregate total from concealing invalid intermediate custody or spending.

Ordinary raw transfer preserves its established rules: exact command/control and revisions, source
ownership, destination deposit permission, prefix splitting and deterministic child identity. Reserved
or equipped stock cannot move; other protection flags survive the transfer. Source delivery instead
requires unprotected, unreserved exact stock, moves the selected prefixes and assigns an admitted
destination reservation. It issues no new material. A whole lot retains its identity at the next
revision; a split retains the source suffix and gives its moved prefix a fresh child at revision zero.
V5 [construction continuation](construction-continuation.md) adds a specific reserved-installation
operation qualified by actual project work. General protection toggles, transformed-lot transfers and
other purpose-qualified reserved-material moves require their own contracts rather than reuse of a
permissive generic mutation.

Terrain refinement/demotion has its own V6 operation because it can split or coalesce several raw
intervals, change their identities and advance both representation storages even when no raw stock
moves. Its qualified evidence binds the actual material prefix and complete allocation/active-cell
observation. Replay must reproduce the entire partition, including unchanged neighbors and all fresh
identities. Ordinary transfer cannot substitute for that geometric operation.

[GatherIssueEvidence](../../src/OpenFantasy.Core/Gathering/GatherIssueEvidence.cs) is produced only by
successful `GatherPlanner.PlanWithEvidence`. It binds the exact accepted command, current world/space,
before/after inventory and gathering hashes, process interval and issued lot. It retains no complete
world, inventory or source-owner chain. Ordinary `GatherPlanner.Plan` does not compute this extra
archival evidence. The material bridge rechecks combined raw/transformed storage and lot bounds and
permanent identities, which the ordinary complete-world caller otherwise supplies.

The optional bounded gathering baseline records the actual source/effort owner at the cut. The bridge
requires an explicitly supplied actual current gathering owner and compares it with the reconstructed
history plus deterministic idle recovery to `world.ProcessIntervals`. `GatheringSnapshot.AdvanceTo`
reproduces repeated idle `AdvanceStep`: effort saturates, a depleted node can regrow once, and its
issued cursor never resets. Current spatial, actor, tool, control and effort checks still run for the
new action. The world/space hash is per-action evidence; movement and time do not require preserving
one old whole-world hash for the entire segment.

An unrelated climb or excavation can spend the same effort owner. If that makes the actual source
owner differ from the supported history, the bridge refuses the mismatch; it must not restore inferred
effort over actual spending. Such cross-owner mutations require explicit joined transitions. The
actual gathering snapshot remains the active owner; its retained baseline is validation evidence.

The V8 excavation adapter explicitly joins mining and gathering under that same effort owner. Both use
its shared action frontier and current spatial regions, and both replay as excavation work operations;
gathering retains its genuine issuing proof inside the work proof. This prevents a generic gathering
entrypoint from spending another action against an older donor space or resetting mining effort.

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

Production V1/V2 retain their original bytes and meanings and refuse a continued owner. V3 preserves
its exact interpretation and refuses the V4 operation history. Opening an
old save does not reinterpret its capacity refusals or migrate it. A V3 component round trip neither
authenticates archived history nor selects a complete-world storage profile.

| Representation | Material records | Required decode context |
| --- | --- | --- |
| V1 | Complete inline batches and live outputs; consumption disabled | None |
| V2 | Complete inline batches, live outputs and consumed claims | None |
| V3 | Active batches/claims and live outputs; absolute totals and context hash | The exact captured continuation context, including its consumption interpretation |
| V4 | Active material operations, batches/claims and live outputs; cumulative counters | The exact material context and actual matching opaque gathering evidence for issuance entries |
| V5 | Ordered material history including reserved construction completion | The exact construction-capable context, original construction batches and matching opaque gathering/completion evidence |
| V6 | Ordered material history including terrain refinement/demotion, with retained optional construction capability | The exact terrain-capable context and matching opaque terrain, gathering and construction evidence as selected |
| V7 | Ordered material history including reserved transport, with retained optional construction capability | The exact transport-capable context and matching opaque transport, gathering and construction evidence as selected |
| V8 | Ordered partial/final mining and joined gathering with exact site, terrain, shared effort and action frontiers | The exact excavation-capable context and matching opaque work evidence; the complete component codec also requires actual-owner dependencies |
| V9 | Complete-world crafting and consumption with exact admitted raw issuance and retained construction | The material context from the actual coordinated world boundary; world journal replay authenticates other material writers |
| V10 | Standalone ordered bilateral raw/produced custody operations | The explicit consented ordered context; independent delivery claims and exact operation replay |
| V11 | Complete-world crafting/use with world-owned bilateral custody evidence | The consented coordinated boundary (context profile 9); complete world admission supplies mandatory produced-custody replay |

V4 decoding validates canonical component structure and joins the supplied actual source proofs.
The caller must then invoke `ValidateForAuthority(actualInventory)` before using the pair. That
second step reconstructs ordered raw/transformed custody, permissions, storage/lot revisions and
combined capacity at every intermediate operation. A successful decode alone is not admitted
inventory and cannot establish that an omitted issuance interval still matches the actual owner.

V3 retains the 16 MiB production frame limit and the 256 active-batch/256 active-consumption bounds.
V4 adds a 512-operation ordered-history envelope. The optional gathering baseline retains its own
bounded source admission; actual proof objects are required at decode, not reconstructed from a
caller-provided source capacity or digest. This decode dependency does not authenticate Host storage.
Its context carries at most 32 recipes, 256 storage baselines and the separately bounded material
basis. V5 retains these envelopes and at most 32 original source-only construction batches, each
bound to its exact imported producer witness, plus the fixed construction recipe catalog of at most
16 identities. It does not retain recursive ancestors. Raw coverage is normalized before checking its final 16,640-range envelope; temporary merge
input is bounded by the imported and active envelopes together. Neither canonical compaction nor a
small live window implies unlimited archive storage or qualified production throughput.

## Complete-world integration boundary

World admission requires one coordinated interpretation for material retirement, care causes and
resource effects. Terrain/excavation coverage, installed construction ingredients, nested liquid
reservations and new source issuance must recognize the same boundary. Existing world, construction,
terrain, source delivery, excavation and restorative-use entrypoints refuse unsupported
continued owners explicitly; legacy geography encoders cannot discard their context. The explicit
material adapters provide standalone source/custody composition, while the separate V5 construction
adapter qualifies reserved completion and physical candidates. The explicit
[liquid continuation](liquid-material-continuation.md) contracts preserve runoff obligations and
reservoir ownership through cuts, with nested context-bound codecs. The separate
[transport adapter](transport-material-continuation.md) qualifies reserved gate delivery and its complete
journal/clock boundary. These adapters do not enable continued world publication.
The separate [terrain adapter](terrain-material-continuation.md) preserves fixed allocations and
records representation operations under V6. It does not by itself join terrain and liquid histories
or grant excavation issuance, effort spending or complete-world publication.
The [excavation adapter](excavation-material-continuation.md) qualifies dry site work, shared gathering
and exact source coverage, preserving static construction backing. It refuses unjoined load, settlement,
liquid, encounter/care and pending-consequence owners; it does not admit a continued complete world.

The complete protocol must schedule before accepted obligations lose headroom, preserve captured
command meanings, publish archive/receipt/identity/head changes atomically and recover from actual
retrieved bytes. See [resource archive continuation](resource-archive-continuation.md) and
[persistence and history](persistence-and-history.md). Those requirements are not discharged by a
standalone material planner or codec.

The explicit [live material observations](live-material-evidence.md) expose accepted actions from the
complete ordinary-care world transaction, including wet excavation and partial work. They preserve
actual sequential candidates and rollback, but are not a continued owner or an imported material grant.

## Complete-world material interpretation

[Production V9](../../src/OpenFantasy.Core/Production/ProductionCodecV9.cs) is selected only by an
explicit [coordinated world cut](coordinated-world-continuation.md). It keeps craft/consumption revision
meaning and imported output accounting, while gathering, transfers, delivery and work remain with
their existing owners. It creates no `MaterialOperationHistory`. The complete world checks an exact
partition of actual issued raw rights; Host journal replay checks the actions and intermediate custody
changes. A structural lower bound on custody revisions cannot authenticate those changes.

The coordinated identity index retains imported producer roles and original installed backing.
Nested planners receive complete historical identity lookup, excluding only the current command
whose uniqueness the world has already checked and reserved. That exclusion does not clear any other
selected, derived or historical identity. V3–V8 keep their separate capability and decoder restrictions.


The explicit [consented-world interpretation](consented-world.md) extends that coordinated ownership
with bilateral raw/produced movement. It selects V11/context profile 9, retains the craft/use revision
meaning, and supplies exact definition and output-prefix evidence to produced partition replay. The
world owns that evidence and imports actual surviving custodians at each cut; it does not source raw
history from the separate V10 ordered adapter. Older contexts refuse the new capability.
