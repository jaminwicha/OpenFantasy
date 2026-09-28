# Item consumption

## Domain contract and publication boundary

[ItemConsumptionPlanner](../../src/OpenFantasy.Core/Production/ItemConsumptionPlanner.cs) prepares the
retirement of actual owned item rights into retained consumption history. Its
[plan](../../src/OpenFantasy.Core/Production/ItemConsumptionPlan.cs) contains candidate counted
inventory, candidate production, a rejection reason, the consumption claim and its created identity.
It does not publish state or issue a command receipt.

The operation has no health, mana, consent, reach, anatomy, encounter or time effect. Naming a
recipient records a participant; it does not establish permission to treat that actor or prove that
treatment occurred. No care action, UI, world codec or durable recovery protocol follows from this
standalone material candidate. [WorldSnapshot](../../src/OpenFantasy.Core/Worlds/WorldSnapshot.cs)
rejects consumption-enabled production under profiles through 24, including an empty enabled owner.
The [aftermath-care composition](aftermath-care.md) explicitly admits this owner together with
treatment and [resource history](world-resource-history.md) for profile 25 transactions. Schema-seven
durable authority publishes those complete-world transactions and replays their actual commands,
receipts and archive boundaries. The local session and native controls bind patient acknowledgment
and separate helper treatment. Encoding a standalone material candidate does not grant publication
or enable ordinary item use; each action needs its own permission, eligibility and time contract.
[Ordinary care](ordinary-care.md) binds these operations through its separate profile26 exploration policy.

## Selection, authority and revisions

[ConsumeItemRequest](../../src/OpenFantasy.Core/Production/ConsumeItemRequest.cs) captures the
custodian, expected storage and production revisions, recipient, exact item definition/version and
one [IngredientRequest](../../src/OpenFantasy.Core/Production/IngredientRequest.cs). The ingredient
identifies the raw or transformed lot, its expected revision and a positive quantity.

The planner checks the acting actor against the supplied
[trusted delivery](../../src/OpenFantasy.Core/Authority/TrustedCommandDelivery.cs) and the custodian's
owner. The caller's transport must authenticate that delivery; constructing the value is not
authentication. The recipient must be an admitted actor. Production must explicitly support
consumption, and the request must match current storage and production revisions.

[MaterialLotSelection](../../src/OpenFantasy.Core/Production/MaterialLotSelection.cs) requires the
exact lot kind, custodian, revision and sufficient quantity. Any protection flag or reservation
blocks selection. The selected definition must match the request. Consumption selects a prefix of
the actual source or output interval; it cannot substitute equal-looking stock with different pins
or provenance.

The candidate removes a fully consumed lot or retains its remaining suffix with an incremented lot
revision. It increments custody, source storage and production revisions once, leaves unrelated
contents intact, and appends the claim. Recipient storage receives no new lot. The planner derives
the consumption identity as `EntityId.DeriveSpawned(commandId, 0)` and checks command/created identity
collisions against the supplied inventory and production. These checks do not replace the complete
world's historical identity reservation or durable receipt lookup.

Ordinary selection, authorization and revision refusals retain the original owners and return no
claim or created identities. Checked-arithmetic and supported canonical-size failures also refuse
without a partial candidate. Invalid inventory/production composition remains an error rather than
being treated as an ordinary gameplay selection.

## Retained provenance and complete output accounting

[ConsumedItemClaim](../../src/OpenFantasy.Core/Production/ConsumedItemClaim.cs) retains its identity,
command, acting actor, recipient, custodian and exact
[RetiredIngredient](../../src/OpenFantasy.Core/Production/RetiredIngredient.cs). The ingredient keeps
the selected lot identity/revision, definition pin and either its raw source interval or produced
batch/output interval. It grants no new raw source, output or spendable entitlement.
The claim constructor also enforces the command-derived consumption identity, so archive restoration
cannot substitute a caller-chosen identity for the planner's derivation.

[ProductionClaimValidation](../../src/OpenFantasy.Core/Production/ProductionClaimValidation.cs)
combines live lots, later recipes' retired ingredients and consumed-item claims. Every produced
output interval must have a complete, nonoverlapping partition across those dispositions. A
consumed output cannot reappear live, be consumed twice or disappear from the retained accounting.
Ingredient lineage remains acyclic, and surviving lot suffixes must follow their retired prefixes
and revisions. Later crafting preserves consumption history through
[MaterialTransformCommitter](../../src/OpenFantasy.Core/Production/MaterialTransformCommitter.cs).

For raw material, validation checks exact source definition pins, finite source bounds and
disjointness among live lots and retired claims. **Generic source capacity does not prove that an
interval was issued.** It permits source units that remain unissued and therefore does not establish
complete issued coverage. A complete authority must join consumption with the source's actual
issuance owner and accepted history. Similarly, a structurally valid archive is not proof that its
retirement transition was authorized.

[ProductionSnapshot.ValidateForAuthority](../../src/OpenFantasy.Core/Production/ProductionSnapshot.cs)
joins world/branch scope, actor/custodian participants, shared identity roles, definition/calibration
pins and combined raw/derived lot and storage bounds. Material mass remains attached to the actual
recipe bindings. Retired ancestral recipe inputs are backing for their output; counting both as
live or consumed mass would count the same material twice.

## Composed material evidence

[ProductionRetirementHistory](../../src/OpenFantasy.Core/Production/ProductionRetirementHistory.cs)
enumerates direct recipe-input and item-consumption retirements together. It retains each retirement's
custodian and exact ingredient. A consumed produced output contributes its output interval, not a
second expansion of the raw ancestry already retired by its recipe batches. Successor validation
requires advancing changed state, fixed world/branch and recipe admission, an unchanged consumption
profile in both directions, and byte-identical prior batches and consumed claims. Ordinary
reconciliation cannot silently select a different archive interpretation. These checks establish continuity
of evidence; they do not authenticate the action that created it.

Terrain material composition uses direct **raw** retirements to close its allocated source partition.
Allocated stock must leave all relevant coarse/cell representation custodians before it can retire.
Refinement or demotion retains that disposition rather than recreating the consumed interval. The
shared registry checks all allocations together; moving stock into another allocation's representation
custodian does not bypass extraction. Retired lot revisions precede the complete custody root.

[ExcavationSnapshot](../../src/OpenFantasy.Core/Terrain/ExcavationSnapshot.cs) accounts for live raw
rights plus direct recipe/consumption retirement when validating its exact issued prefix. This
component-level accounting does not enable consumption in an older complete-world profile.

Runoff preparation and receiver-capacity reconciliation include the same direct retirement evidence.
Consuming unreserved received stock can free actual receiver capacity, while reserved funding,
scheduled transport and existing claims remain intact. Reconciliation rejects erased or rewritten
history even when the supplied replacement components are internally consistent. Reservoir projections
and transport candidates continue to derive from that complete reconciled capacity owner.

The archives make the new interpretation explicit:

| Composed owner | Existing archive | Consumption-capable archive |
| --- | --- | --- |
| Terrain material state | V1 raw-only / V2 recipe production | [TerrainMaterialCodecV3](../../src/OpenFantasy.Core/Geography/TerrainMaterialCodecV3.cs) |
| Shared material registry | V1 raw-only / V2 recipe production | [TerrainMaterialRegistryCodecV3](../../src/OpenFantasy.Core/Geography/TerrainMaterialRegistryCodecV3.cs) |
| Runoff request and prepared routing | V1 | [WatershedRunoffCodecV2](../../src/OpenFantasy.Core/Geography/WatershedRunoffCodecV2.cs) |
| Receiver capacity | V1 | [WatershedRunoffCapacityCodecV2](../../src/OpenFantasy.Core/Geography/WatershedRunoffCapacityCodecV2.cs) |
| Reservoir geometry and fill | V1 | [LiquidReservoirCodecV2](../../src/OpenFantasy.Core/Geography/LiquidReservoirCodecV2.cs) |
| Scheduled reservoir transport | V1 | [ReservoirTransportCodecV2](../../src/OpenFantasy.Core/Geography/ReservoirTransportCodecV2.cs) |

Each new archive retains the complete production V2 owner, even when its consumed collection is empty.
An absent or legacy production owner cannot masquerade as that interpretation, and an older codec
cannot drop consumed evidence. Nested tags, bounded reads and canonical reconstruction apply at each
layer. Content hashes select the corresponding explicit archive. Independent component restore does
not grant world publication or silently migrate a retained save.

## Restorative composition

For restorative composition, [RestorativeItemPlanner](../../src/OpenFantasy.Core/Care/RestorativeItemPlanner.cs)
combines this exact dose retirement with a capped resource transition. Its coupled candidate supports
self-use and another recipient without depending on a combat aftermath acknowledgment. Permission,
anatomical delivery, reach, time, encounter consequences and persistence belong to the complete action.
The [actor-resource contract](actor-resources.md) distinguishes those responsibilities.

## Canonical archive ownership

The optional `consumedItems` constructor argument explicitly selects production ownership:

| Owner | Constructor argument | Encoding |
| --- | --- | --- |
| Existing production | `null` or omitted | [ProductionCodecV1](../../src/OpenFantasy.Core/Production/ProductionCodecV1.cs); existing bytes and interpretation |
| Consumption-enabled production | Non-null collection, including empty | [ProductionCodecV2](../../src/OpenFantasy.Core/Production/ProductionCodecV2.cs); explicit version 2 and retained consumed collection |

V1 refuses an enabled owner rather than dropping its semantics. V2 retains the complete recipes,
batches and live lots alongside the consumed claims, reusing their established component frames.
Decode rebuilds the bounded owner and requires exact canonical re-encoding; it rejects malformed,
noncanonical, truncated, trailing or over-limit data. `ContentHash` uses the owner's corresponding
codec. Limits include 256 consumption claims, no more claims than the production revision, bounded
individual/collection frames and a 16 MiB production archive. Revision/count consistency is not an
authenticated transition history.

The 256-claim bound is shared by every action that consumes through this production owner, including
aftermath remedies and ordinary restorative use. `ItemConsumptionPlanner` refuses a full collection
with `Unavailable` before preparing material changes. The complete ordinary-care assessment adds a
typed `MaterialHistoryCapacity` explanation and withholds its executable offer; it separately checks
the ordinary-care owner's 256-use bound. This preflight does not reserve either capacity, so the
authority evaluates the progressively updated owners again when selecting the action's effects.

[Resource-history continuation](world-resource-history.md) rotates causal HP/MP/resource batches; it
preserves this complete production owner and its consumed claims. Removing a live lot frees live
storage, but neither that removal nor a resource segment cut frees retained consumption history.
The bound is not a gameplay lifetime quota. Extending consumption beyond it requires an explicit
admitted continuation protocol preserving issued rights, retirement provenance, historical identities
and the exactly-once effects of composed actions. No such extension follows from encoding a component,
increasing a count limit or archiving a resource segment.

Continuation must preserve the coupled conservation proof, not only the consumption count. Consumed
produced intervals close their originating batches' output partitions; subsequent recipes can also
retain claims on those outputs. The separate 256-batch production bound therefore cannot be solved by
rotating consumed-item records alone. Installed construction, excavation issuance and composed
terrain/runoff owners refer to the same provenance. Each affected owner must recognize one explicit
continuation interpretation before any of those records can leave its active state.

A compact material basis must distinguish issued raw rights, live raw/produced claims and irreversible
retirement. It must preserve definition pins, surviving-lot lineage and exact output allocation while
retaining retrievable full historical provenance outside the active window. Counts, an archive hash
or an unverified supplied basis cannot establish those facts. The standalone
[material continuation basis](material-continuation-basis.md) captures surviving lots, exact live
output intervals and direct retirement from the supplied material owners. Repeated capture combines
the imported coverage with active dispositions; it does not expand produced ancestry twice. Capture
itself provides no archive authentication or continued owner interpretation; production V2 validates
its complete retained batches and consumed claims directly.

[Production continuation](production-continuation.md) supplies a separate V3 domain interpretation
with an exact captured context, imported-right partitioning and mandatory historical identity lookup.
V4 adds explicitly ordered source/custody operations to those material windows; consumption records
remain exact dispositions, and source delivery remains a transfer. V5 adds qualified construction
completion while retaining the captured consumption capability; enabling construction does not enable
consumption in a profile that lacks it. Neither interpretation enables
continued restorative use or complete-world publication. Those actions must join their material history
to care/resource closure and verified archive retrieval before existing profile capacities can be extended.

The [restorative supply tests](../../tests/OpenFantasy.Hosting.Tests/Production/RestorativeSupplyTests.cs)
bind the retained Potion payload and three-berries-to-one-Potion recipe to a separately authored
physical calibration. They exercise raw and crafted consumption, archive restoration, another craft
and reintroduction refusal. The source payload's restorative fields do not cause a health effect,
and the test calibration makes no claim that legacy content supplied physical mass measurements.

See [materials and activities](materials-and-activities.md),
[identity and determinism](identity-and-determinism.md) and
[persistence and history](persistence-and-history.md) for the surrounding owners and publication rules.
