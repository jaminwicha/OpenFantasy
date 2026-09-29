## Result

All 18 cases in the focused selection pass on the final build: the ten existing cases plus eight new ones. Two of the four obligations are only partly covered, as noted below. No runtime defect was found.

### Obligations → cases

| # | Obligation | Case | Outcome |
|---|---|---|---|
| 1 | 4,096 combined live-lot bound | `Combined_live_lot_bound_admits_a_split_one_below_then_refuses_splits_and_still_moves_whole_lots` | passed, with a gap |
| 2 | Encounter/lifecycle routing | `Paired_intents_follow_the_actual_encounter_care_and_recovery_owners` | passed |
| 3a | Third actor between recipient and offer | `A_third_actor_ordered_between_acceptance_and_offer_keeps_its_own_outcome_beside_the_applied_pair` | passed |
| 3b | Two independent pairs in one batch | `Two_pairs_in_one_batch_are_decided_independently_with_four_receipts` | passed, **partial** |
| 3c | Wrong-addressed acceptance | `Acceptance_addressed_to_an_offer_for_someone_else_moves_nothing` (2 variants) | passed |
| 4 | Balanced live-lot mutation | `Balanced_live_lot_mutation_is_refused_by_the_custody_partition` (custodian swap, moved suffix) | passed |

**What each proves, and where it falls short**

- **1 — lot bound.**
  - Fresh genesis is admitted at 4,095 raw lots, with the filler source included in the admission hash.
  - An actual craft and a produced split then reach exactly 4,096 combined. At the bound, a produced split and a raw split are each refused `Unavailable` with owners unchanged; whole produced and whole raw lots still move.
  - The root at the bound round-trips through the codec and a decoded archive cut.
  - **Gap:** a genesis authored directly at 4,096 raw lots was not run. I started one below because the craft bound (`MaterialTransformCommitter`) would refuse the craft at 4,096.
- **2 — routing.**
  - A positive control applies before the encounter.
  - The pair is refused with the exact owner message in live combat, Outcome, pending care and resumed combat. The five non-advancing batch kinds refuse it at construction.
  - During post-release debt both receipts are `Unavailable` while the step still advances. After recovery the pair applies.
  - Confrontation and Staging were not observed separately.
- **3a — third actor.** Both existing wolves submit their own actions; one sorts between recipient and sender. Their receipts are asserted equal to the same command decided alone, not against a named rejection.
- **3b — two pairs.** One pair applies and the second (wolf to wolf) is refused exactly as when decided alone. **Two simultaneously applied pairs are missing**: that needs two more reaching, manipulating actors authored at fresh genesis, which I did not do.
- **3c — wrong address.** The offer gets `Unavailable`, the acceptance `NotAuthorized`, nothing moves, and a correctly addressed pair then applies.
- **4 — mutation.** Uses actual crafted and gifted 2/2 lots. The production frame and the reservoir and liquid-cell bindings are rebound consistently, so admission fails at "Live produced lots must equal the partition replayed from retirements and ordered custody evidence."

### Owned files changed

- `CareWorldFixture.cs`: optional `freshRawLotTarget` and `FillerSource`; defaults unchanged.
- `CareRewardJourney.cs`: `PrepareDefeat` overload with a combat observation callback.
- `ConsentedWorldCraftedJourneyTests.cs`: `ConsentedPotionFixture` gains `rawLotTarget` and `BerryLot`.
- New: `ConsentedWorldInterleavingTests.cs`, `ConsentedWorldRoutingTests.cs`, `ConsentedWorldLotBoundTests.cs`, `ConsentedWorldLiveLotMutationTests.cs`.
- `OrdinaryCareWorldFixture.cs` and `CoordinatedLiveWriterJourney.cs` are untouched.

### Checks

All under `Beta/implementation/p02/world-consented-material/qualification/`:

| Run | Outcome |
|---|---|
| `integration-build-20260929T081647Z` | passed, 17.443 s |
| `integration-journey-20260929T081711Z` | **failed**, 16 passed / 2 failed (both mutation variants) |
| `integration-build-20260929T081913Z` | **failed**, analyzer error xUnit1051 in the mutation test |
| `integration-build-20260929T081937Z` | passed, 14.18 s |
| `integration-journey-20260929T081956Z` | passed, 18/18, 18 distinct IDs, 14.596 s |

The two journey failures were my harness, not the runtime. I had rebound the reservoir through `PlanReconcile`, which refuses a production change without advancing custody. I replaced that with the encoded-root rebinding described above.

### Remaining gates

- Two simultaneously applied pairs, with extra actors authored at fresh genesis.
- Genesis authored directly at 4,096 raw lots.
- Confrontation and Staging routing.
- Core and Hosting regressions were not re-run; the shared-fixture edits are additive but unverified against other suites.
- The separate codec/archive review.
- No Host, SQLite, network, native or retained-save claim is made.
