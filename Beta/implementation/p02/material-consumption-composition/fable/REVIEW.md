# Material/consumption composition: independent review

Reviewer: Claude Fable 5.1 (`fable[1m]`, Max effort), 2026-09-25. Isolated worktree over base
`c2547c6` with the 29 candidate files from tested WIP `6e14121`. No commits, no git operations, no
delegation. Only `dotnet` 10.0.401 was executed. This is a conservation/compatibility review of the
standalone geography owners; it asserts nothing about live care, fluids, performance or production gates.

## Scope inspected

All 25 candidate source files and 4 candidate test files, plus the related unchanged owners they
compose with: `ProductionSnapshot`, `ProductionClaimValidation`, `ProductionCodecV1`,
`WatershedRunoffFunding`, `WorldSnapshot` (admission guard at line 62) and the existing
Geography/Terrain test helpers.

## Findings

### F1. Defect, fixed: ordinary reconciliation could silently acquire the consumption profile

`Beta/Runtime/src/OpenFantasy.Core/Production/ProductionRetirementHistory.cs:30` (candidate) only
refused `prior.SupportsConsumption && !next.SupportsConsumption`. The reverse flip passed.

Causal example (registry): a legacy recipe-only registry (`TerrainMaterialRegistryCodecV2` hash)
receives `ReconcileProduction(advancedInventory, new ProductionSnapshot(..., revision + 1, sameRecipes,
[], [], []))`. The candidate is structurally valid, so the registry accepted it, its `ContentHash`
switched to `TerrainMaterialRegistryCodecV3`, and `TerrainMaterialRegistryCodecV2.Encode` then threw for
the same owner. Via `WatershedRunoffCapacityState.PlanReconcile` the same flip re-versions capacity,
reservoirs and transport (`LiquidReservoirState`, `ReservoirTransportState` select their codec from
`Capacity.Production.SupportsConsumption`). The candidate's own test comment
(`ConsumptionGeographyCodecTests.cs:112`) states reconciliation cannot change the interpretation, and
`TerrainMaterialRegistry.AttachProduction` is the only explicit profile composition.

Fix: require `prior.SupportsConsumption == next.SupportsConsumption` (both directions), with an
explanatory comment and message. Regressions:
`ConsumptionReviewTests.cs` → `Legacy_registry_reconciliation_cannot_silently_acquire_the_consumption_profile`,
`Legacy_capacity_reconciliation_cannot_silently_acquire_the_consumption_profile`. Both fail on the unfixed
validator ("No exception was thrown"): `qualification/material-review-defect-witness-unfixed.trx`.

### F2. Untested invariant, test added: forged consumption of held forcing stock

The planner refuses reserved lots (candidate test `Outstanding_runoff_funding_cannot_be_consumed...`), but
no test showed that a *structurally valid* successor (claim retiring the first held unit, remainder lot
still under the hold, `ValidateForAuthority` passing) is refused when the capacity owner re-prepares its
pending budgets. `WatershedRunoffFunding.cs:105-108` rejects it (coverage no longer starts at the funded
interval). Added `A_structurally_valid_claim_on_held_forcing_stock_is_refused_at_capacity_reconciliation`.
Passes on the unfixed and fixed source: it is a guard regression, not a defect witness.

### Verified seams (no defect found)

- Raw issuance before retirement: `ExcavationSnapshot.cs:63-78` now includes consumed raw claims in the
  continuous coverage cursor and rejects any claim on an incomplete site. Not reachable in base: `WorldSnapshot.cs:62`
  rejects every enabled owner, so no world-level test can exercise it (obligation O1).
- Representation custodians: `TerrainMaterialProduction.ValidateDeposit` (36-46) and
  `TerrainMaterialRegistry.ValidateDefinitions` (189-195) apply to batch ingredients and consumed claims
  alike, across every allocation's coarse/fine custodian. Covered by `ConsumptionCompositionTests` 58-99.
- Erasure/rewrite/downgrade: `ValidateSuccessor` byte-compares every prior batch and claim by identity and
  requires strictly advancing revision and identical recipe admission. Covered by the erase/rewrite/downgrade
  theories for registry and capacity.
- Revision equality/overflow: retirements must precede the custody root
  (`TerrainMaterialProduction.cs:15-20`, `WatershedRunoffDomain.cs:83-89`); claims ≤ production revision;
  quantities use `checked`. Covered by `Fully_retired_lot_revision...` and `A_fully_consumed_lot...`.
- Material quantities: `TerrainMaterialState.ValidateCoverage` (147-154) counts only direct raw retirements
  intersecting the deposit; produced-output consumption adds no raw retirement, so ancestry is never counted
  twice (`Consuming_a_real_produced_unit_does_not_retire_its_raw_ancestry_twice`).
- Codec seams: distinct outer tags per version; nested production is profile-pinned through
  `GeographyProductionCodec` (empty or V1 payload inside a V2/V3 archive → `FormatException`; V2 payload
  inside a V1 archive → `ProductionCodecV1.ExpectVersion(1)` → `ProductionFormatException : FormatException`).
  Each decoder re-encodes canonically; truncation, trailing bytes, an overlong field length, outer relabel and
  cancellation are covered by the shared `Split` assertions for all six owners.
- Legacy bytes/hashes: every public V1 `Encode` passes `consumption: false`, producing the same tag and the
  same production field bytes as HEAD (`[]` for null, `ProductionCodecV1` otherwise); enabled owners throw
  `ArgumentException` in V1 exactly as `ProductionCodecV1` already did. All existing V1 geography/excavation
  codec tests pass unchanged.
- Zero/empty enabled owner: distinct archive and refusal of erasure covered for registry, allocation state,
  runoff, capacity, reservoirs and transport.

### Minor observations (not changed)

- `WatershedRunoffCodecV2.MaximumRequestByteLength` (public, 22 900 000) duplicates V1's private constant and
  is not consulted by the shared reader; drift risk only.
- `GeographyProductionCodec.Encode(null, consumption: true)` throws `ArgumentException`; correct, since an
  enabled archive requires its owner.

## Edits

- `Beta/Runtime/src/OpenFantasy.Core/Production/ProductionRetirementHistory.cs`: F1 fix (one condition,
  comment, message).
- `Beta/Runtime/tests/OpenFantasy.Core.Tests/Geography/ConsumptionReviewTests.cs`: three regressions (new file).
- No other file touched. No docs, projects or other tests edited.

## Evidence (`qualification/`)

| Artifact | Result |
| --- | --- |
| `material-review.trx` | focused `Geography|Excavation` filter, fixed source: 278 passed, 0 failed, 0 skipped (first run 278/0/0; re-run after restoring the fix recorded below) |
| `material-review-defect-witness-unfixed.trx` | review tests only, validator temporarily reverted: 2 failed (F1), 1 passed (F2) |
| `verification.json` | commands, filter, counts, toolchain |

Command: `dotnet test <Core.Tests.csproj> --no-restore -m:1 -p:UseSharedCompilation=false --filter
'FullyQualifiedName~Geography|FullyQualifiedName~Excavation' --logger 'trx;LogFileName=material-review.trx'`.
Restore used `--locked-mode --ignore-failed-sources --disable-parallel` (exit 0).

## Unresolved obligations (out of my ownership)

- O1. Excavation consumption coverage (`ExcavationSnapshot.ValidateForWorld`) is untestable in base because
  `WorldSnapshot` rejects enabled owners. The care-enabled world profile must add a world-level regression:
  consumed raw claim on an incomplete site rejected; on a complete site counted once in coverage.
- O2. Architecture reference: `item-consumption.md` / `world-and-geography.md` should state the geography V2/V3
  archive split and the "unchanged consumption profile" successor rule (same-change hook). Not edited here.
- O3. Full Core + Hosting regression remains with the integration lead; this run covers only the focused filter.
- O4. Complete-world consumption admission stays disabled; nothing here enables it.
