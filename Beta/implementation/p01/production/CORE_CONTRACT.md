# Instant ordinary crafting and persistent provenance

The portable production ledger now prepares real raw/derived ingredient retirement and product/waste output candidates. It uses the same counted custody authority as gathering, with explicit physical calibration and immutable recipe versions. It does not turn crafted outputs into new `AdmittedCountedSource` rows or reinterpret `CountedSourceLot`. The old `MaterialDefinition`, `MaterialRecipeDefinition`, material recipe codec and their frozen fixture bytes are unchanged.

## Definitions and quantities

`ItemPhysicalBinding(Item, Physical)` links an existing counted item definition/version to a distinct `MaterialDefinition` calibration. Source content hashes and physical calibration hashes remain separate. One admitted item pin cannot have conflicting physical calibrations across recipes. `CraftRecipeDefinition(DefinitionId, PhysicalRecipe, Bindings, Allocations)` wraps a material recipe with exactly one abstract completed operation, not elapsed milliseconds, simulation ticks or a timed job. The source ration recipe is instantaneous; the previous illustrative 120-work definition is not activated by this runtime.

`OutputMaterialAllocation(InputItem, OutputIndex, MassPerOutputUnit)` pins the physical composition of every canonical output unit. Each output's allocation column equals its physical mass per unit. Each input's row, multiplied across all output counts, equals the entire consumed input mass. Duplicate allocations, missing rows/columns, wrong versions or overflow reject. `OutputIndex` refers to `PhysicalRecipe.Terms` excluding input terms, retaining canonical role/material order: products precede waste.

The admitted ration interpretation is two 20,000 mg berry portions plus one 50,000 mg wood unit → two 40,000 mg packed rations plus one 10,000 mg offcut lot. Each ration contains 20,000 mg berry and 20,000 mg nonfood wooden packaging; the offcut contains 10,000 mg wood. These are explicit authored physical values. Only the original counted `2 berries + 1 wood → 2 rations` and source payload hash are retained source recipe facts. Waste and packaging interpretation remain separately pinned target content. Later eating must account for packaging and actual nutrition/HP/MP behavior; this increment grants no healing.

Volume is positive per calibrated unit and each recipe input/output `volume × count` uses checked arithmetic. The runtime does **not** sum the occupied volumes of all storage contents, assign volume to uncalibrated tools, or enforce physical storage capacity. Storage admission remains the combined counted quantity of raw and derived lots. It does not impose volume conservation or claim complete physical placement qualification.

## Typed lineage and retirement

`IngredientRequest(Kind, LotId, ExpectedRevision, Quantity)` explicitly selects a source lot or transformed lot. `RetiredIngredient` has two constructors: one retaining a `SourceInterval`, the other retaining a `ProductionOutputInterval`. It also preserves the consumed lot identity/revision and exact item pin. The union cannot reinterpret a derived output as a raw admitted source.

`ProductionBatch(BatchId, CommandId, ActorId, CustodianId, RecipeId, RecipeVersion, Ingredients)` records one complete operation. Its batch ID is exactly `EntityId.DeriveSpawned(command, 0)`. Recipe input quantities must match all retired claims exactly. `TransformedLot(LotId, CustodianId, Definition, Output, Revision, Protection, ReservedBy)` owns a positive interval in one batch/output budget, represented by `ProductionOutputInterval(BatchId, OutputIndex, Start, End)`. Output lots are created at spawn ordinal `1 + OutputIndex`.

Raw retired intervals are disjoint from each other and from all current raw lots, with exact admitted source pins/capacities. A surviving raw remainder retains its lot ID, follows its retired prefix and has a greater revision. The same checks apply to derived remainders. Historical references to one lot cannot switch definition, provenance family/source/output budget or move backward in revisions/intervals.

Every produced output budget is accounted for **completely** by disjoint live and retired derived intervals; gaps, overlaps, missing waste and excess claims reject. A future process may consume an earlier crafted output through the same typed selection and retain the prior batch lineage. The finite batch dependency graph must be acyclic. This supports ordinary recipe chaining without introducing a generic task/process graph, recursive callbacks or newly minted source supply.

The ledger records transitive physical provenance rather than assigning every product the whole ingredient entitlement. Raw source units retire once; later output intervals are independent disjoint claims on the newly produced budget, linked back to that retirement through the batch and allocation definition. A partial derived consumption leaves the original lot's remaining output interval; a complete consumption removes its live entry and retains the retirement permanently.

The snapshot verifies local claim consistency, not the historical fact that a caller once owned or possessed a described input. That fact comes from the admitted initial world and authenticated action replay. Loading arbitrary hand-constructed history is not a substitute for the durable authority's hash/replay/admission checks.

## Pure operation and world integration

`InstantCraftPlanner.Plan(inventory, production, delivery, actorId, commandId, request)` returns `CraftPlan(CandidateInventory, CandidateProduction, Rejection, CreatedIds)`. The host must authenticate delivery before using `TrustedCommandDelivery`; the planner itself checks current principal/session control and owned receiving custody before private ingredient/revision details. All inputs must be in the same explicitly named controlled primary inventory, with current storage/production/lot revisions, exact recipe/item pins, sufficient quantities and no protection or reservation flags.

The operation preflights combined raw-plus-derived count capacity, exact input quantities, output lot count and local identity collisions. It then retires selected prefixes, preserves partial remainders with incremented revisions, creates every product and waste lot, and increments production/custody/storage revision once. All related source admission remains unchanged. Selection failure or arithmetic overflow returns the original immutable states and no created IDs. A caller supplying internally inconsistent authority/production state receives a state-composition exception; this is not recast as a gameplay rejection.

The root world processor supplies current sequential candidates after earlier actions, validates body/hold/presence rules, reserves command/result identity and checks every `CreatedIds` member against global/current/historical identity. Any collision discards the whole craft candidate. Only the outer SQLite transaction publishes state, lineage, identity claims, journal and receipt. The pure planner has no retry store, clock, IO, animation or completion timer.

The root also must apply combined capacity checks to gathering and handover, and join every retired gathering-source interval end to the retained node cursor. Checking only live raw lots after crafting would forget consumed units and permit a restored cursor to reissue them. The production component starts with pinned recipes, revision zero, no batches and no derived lots; adding it during explicit profile migration creates no output supply.

## Bounds and canonical decoding

The current component accepts at most 32 recipe definitions, 256 historical batches, 4,096 combined live raw/derived lots, 64 ingredient selections/retirements per operation, and 256 sparse physical allocations per recipe. Bounds reject admission or apply backpressure; they never silently delete history or unused material. Production history pagination and larger qualification budgets require a later explicit representation/version, not pruning old retirements.

`ProductionCodecV1.Encode/Decode` and `EncodeRecipe/DecodeRecipe` use unchanged OFC framing with separately versioned production tags. The complete snapshot includes exact recipes/bindings/allocations, sorted batches and sorted derived lots. Nested retirements retain their explicit provenance discriminator. Integers, pins, reservations and enum widths are strict. Unknown versions/tags, truncation, duplicate/noncanonical order, excessive collections/frames, wrong physical mappings, cyclic history or contradictory budgets fail closed. Every accepted snapshot/recipe re-encodes to identical bytes. Maximum snapshot frame length is 16 MiB; recipe maximum is 128 KiB, with smaller nested bounds applied before collection expansion.

These maxima do not claim that every larger theoretically representable recipe graph or inventory fits production recovery/memory/latency budgets. Existing recovery limits remain enforced separately. World profile dispatch and migration are root-owned; component version one is distinct from OFC framing version one, world profile three and SQLite schema versions.

## Verification scope

New pure tests cover exact recipe counts/composition, prefix retirement, complete raw and derived consumption, chaining, immutable rejection, canonical ordering/copying, owner/privacy checks, reservations/protection, arithmetic failure, combined count capacity, source relabeling, duplicate/gapped outputs, DAG cycles and stale input revisions. Independent Python stdout vectors include the recipe, repacking recipe, empty ledger, first real craft and chained output ledger, plus malformed frames. Decoder tests reject every prefix truncation of an actual crafted state.

The coordinator owns builds, world-level command/hold/identity tests, migration, native interaction and full persistence verification. Source files and test declarations alone do not prove those checks passed. Actual TRX counts and exact reviewed hashes are retained in the adjacent validation/review evidence when available.
