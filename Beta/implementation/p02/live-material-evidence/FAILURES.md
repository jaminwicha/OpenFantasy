# Retained failures

- `qualification/build-01.log`: two integration-test compile errors introduced by Astra, not runtime
  failures: attempted use of internal `GatheringSnapshot.SpendEffort` from Hosting.Tests and a missing
  Terrain namespace for `CellCoordinate`. The expected intermediate effort is now computed as a test
  observation from the actual prior owner, and the namespace is imported. No API visibility or game rule
  was changed to make the tests compile. Subsequent results remain separately recorded.

- `qualification/build-02.log`: xUnit2012 rejected Astra's collection-existence assertion. Replaced
  `Assert.True(collection.Any(...))` with the same predicate in `Assert.Contains`; no threshold changed.

- `integration-01`: 15 passed, six failed. Four Astra fixture failures assumed a direct eastward
  route where terrain blocks it (three berry/mixed cases, one reward-refusal setup). Fixtures now
  follow the existing south/east berry connector or use the actual bounded route search to bring
  the helper to the player. No teleport, terrain edit or movement threshold change.
- One Astra inspection test tried to read the raw database while its exclusive authority lease was
  open. It now compares live journal usage and verifies exact file bytes around a closed-save/reopen
  inspection. The lease remains exclusive.
- Fable's single-writer archive test assumed 255 entries already forced a cut. It now earns entry256
  with an actual accepted finite dose, then tests the real subsequent cut. The two-writer Host test
  passed at255 with its existing whole-batch reservation. No capacity gate changed.

- The first final-verification run reported zero preserved milestones because the initial inventory
  was empty. Its output is retained as `verification-empty-milestone-inventory.json`; it is not a
  completed preservation claim. `milestone-reconciliation.json` now compares all 70 preceding refs
  with the committed prior inventory, plus the care-frontier ref with original HEAD. The verifier
  requires all 71 exact refs and both checkpoint refs, and refuses empty test/frame inventories.
  Runtime/test source and execution binaries are unchanged; the preservation check is rerun separately.
