# Independent review: consented production transfer

**Recommendation: keep WIP. Do not accept as the assignment's integration unit.** A narrowed component checkpoint (approval contract, decision step, plan) is acceptable after the corrections below. V10 / profile 8 should not be frozen as a published format yet.

**Limits of this review:** Read/Glob/Grep only, no tool denied. I had no diff against HEAD, so "old profiles exact" rests on reading current code paths. I inspected no test output or qualification logs and claim no results. Not read: `ProductionContinuationBasis`, `ProductionClaimValidation`, `CommandEnvelope`, the ConsentedCustody sources, most of `ProductionContinuationValidation` and the World files (grep excerpts only).

## Blockers

**B1. V10 is a disconnected branch (assignment item 4 not met).** No state can cross between V10 and the coordinated world in either direction:
- `ProductionContinuationContext.cs:27-30` excludes complete-world, construction and geographic owners from the consented capability.
- `ProductionContinuationContext.cs:45-48` stops a complete-world production selecting it.
- `ProductionSnapshot.cs:236-237` stops a consented segment joining `ContinueWorld`.
- `WorldSnapshot.cs:64` and `WorldCoordinatedAdmission.cs:57` admit only complete-world contexts.

The stated binding ("retain the same operation record, re-decide with the same step") does not hold as written:
- `MaterialOperationHistory.cs:15,25` needs ordered history with consecutive sequences; the complete world has none.
- `MaterialContinuationReplay.cs:33-36` rebuilds raw inventory from the basis and replays every raw change. The world owns raw movement, so the inventory at a gift's position cannot be reconstructed there.
- The record carries no before/after owner hashes, unlike the construction, terrain, transport and excavation evidence. A world binding likely needs them, which changes the `material.consented-transfer` payload.
- `ProductionContinuationValidation.cs:39-43,189-193` checks one remainder per boundary with an unchanged custodian. Split lots under different custodians need that partition validation rebuilt.

**B2. Terms pin global revisions, which defeats the asynchronous offer shape in WORLD_BINDING_REVIEW.** Counterexample: sender proposes at production revision 100; an unrelated actor crafts; the recipient accepts. `ConsentedMaterialTransferStep.cs:48-54` returns `RevisionConflict` although the lot and both storages are untouched. Decide this before freezing `ConsentedMaterialCodecV1` bytes.

**B3. Required tests are missing (assignment item 5).**
- No `MaximumOperations` boundary test (`ConsentedMaterialTransferPlanner.cs:31`) and no combined `MaximumLots` split test (`Step.cs:175`); grep confirms neither appears in the new tests.
- No real installed-construction test. `ConsentedMaterialJourney.cs:78-81` hand-sets `ReservedBy` on a crafted ration, and V10 cannot hold a construction completion at all. The worker discloses this.
- No balanced-mutation negatives on lots: swapped custodians, suffix instead of prefix, moved lots with the operation dropped. By reading, the replay equality at `MaterialContinuationReplay.cs:69-73` refuses these, but nothing proves it for this capability.

## What holds by code reading

- Authentication is decided first (`Step.cs:27-30`), so forged approvals learn nothing about lots or identities and record nothing.
- Whole and split movement keep batch, output index, intervals and flags (`Step.cs:110-130`); reserved and equipped stock is refused.
- A recorded approval cannot be replayed for authority, because its production revision pin is already stale.
- Old codecs fail closed (`ProductionMaterialCodec.cs:26,98`); profile numbers 1-7 are unchanged.
- The journey asserts intervals, custodians, revisions and recraft ancestry across three cuts, not just totals.

## Weaker findings

- `ConsentedMaterialTransferTests.cs:347-353` accepts any `ArgumentException` or `FormatException`. It never asserts that decode succeeded and the owner validation failed, so its name overstates it.
- Post-cut identity refusals (`:82-88`) come from the fixture's own set. Cuts reuse the in-memory context (`Journey.cs:197-215`); no test re-derives segment N+1's context from segment N's decoded bytes.
- `Step.cs:149-166` catches only `CustodyConflictException`. If `ToRawMovementRequest` can throw `ArgumentException`, the planner throws instead of refusing. Unverified.
- The history-full check precedes authentication (`Planner.cs:31`). Minor.
- `MaterialOperationCodec.cs:120,123` decodes stored bytes into `TrustedCommandDelivery`. This is historical data; the replay message "real bilateral approval" should say so.
- No World-side negative test exists for a consented production.

## Uncertain

- Whether NPC actors have control bindings. `CanControl` requires one, so NPC gifting may be inexpressible.
- Whether the terms need actor revision, reach or expiry pins for the world.

## Smallest coherent correction

1. Settle B2 and the evidence-hash question before any bytes are published.
2. Add the B3 tests and tighten the mutated-frame assertions.
3. Build an explicit complete-world consented profile: the world owns the ordered consent evidence, the same step decides it, world validation gains per-lot custody checks keyed by that evidence, and admission selects it by explicit transition.

## Next full-world proof

In a fresh coordinated world:
1. Craft, make a split gift through the world step with reach and control checks, have the recipient consume through ordinary care, cut with `ContinueWorld`, reopen, gift onward, cut again.
2. Include a real construction completion whose installed output is refused.
3. Show the existing world snapshot refusing the new interpretation.

Retained-world migration, Host, native and asynchronous offer proof all remain open.
