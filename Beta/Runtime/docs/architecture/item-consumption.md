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
rejects every consumption-enabled production owner, including an empty one, until a complete
care-enabled world profile supplies those bindings.

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
claim or created identities. Checked-arithmetic and supported archive-capacity failures also refuse
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

The [restorative supply tests](../../tests/OpenFantasy.Hosting.Tests/Production/RestorativeSupplyTests.cs)
bind the retained Potion payload and three-berries-to-one-Potion recipe to a separately authored
physical calibration. They exercise raw and crafted consumption, archive restoration, another craft
and reintroduction refusal. The source payload's restorative fields do not cause a health effect,
and the test calibration makes no claim that legacy content supplied physical mass measurements.

See [materials and activities](materials-and-activities.md),
[identity and determinism](identity-and-determinism.md) and
[persistence and history](persistence-and-history.md) for the surrounding owners and publication rules.
