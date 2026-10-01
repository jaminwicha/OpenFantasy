# Stock genesis result — terminal

Status: complete for the three assigned deliverables. Test-only work; runtime, old tests, the existing
paid tests, assets, docs, specs and original databases are unchanged. Nothing was staged or committed,
and the branch is unchanged.

## Evidence

| Check | Report | Result |
| --- | --- | --- |
| build | [build-20261001T013323Z-a9ee0](qualification/build-20261001T013323Z-a9ee0/report.json) | accepted, 18.5 s; 513 test pins and 23 frozen specs preserved |
| new-host | [new-host-20261001T013345Z-31513](qualification/new-host-20261001T013345Z-31513/report.json) | **32 Passed**, 0 failed, 12.3 s (25 existing + 7 new) |

There were no failed reports. Each command ran once and passed on its first attempt.

## New files

- `Beta/Runtime/tests/OpenFantasy.Hosting.Tests/WorldExchange/PaidTradeWorldFixture.cs`
- `Beta/Runtime/tests/OpenFantasy.Hosting.Tests/WorldExchange/PaidWorldExchangeTests.BuyerCompetition.cs`
- `Beta/Runtime/tests/OpenFantasy.Hosting.Tests/WorldExchange/PaidWorldExchangeTests.StockConstraints.cs`

## How the fixture is admitted

- The fixture uses `EncounterJourneyAdmission.Create(..., composeFreshFarm)`. The callback runs
  `CareFarmBuilder(farm, CareFarmSources.Load(files), splitStock: true)` and then adds the third person
  and the authored stock. All of this happens before `BreachFarmBuilder` binds water, reservoir and
  liquid cells, and before pacing, the encounter lifecycle and care bind the whole graph. No lived
  snapshot is modified.
- **Third person ("Market trader").** It follows the same joins as `EncounterFarmWolf`:
  - its own IDs for actor, form, body, storage, principal, session and wallet, each checked against
    the farm's identity index;
  - presence, a Host resident, an OwnerOnly pack, gathering work and a construction access/exit;
  - the human body already shared by the player and Wren (the fixture checks they are the same);
  - the player's source-backed squire profile and curve at level 1;
  - an empty wallet, with total supply unchanged.

  Its feet are the player's position +256 Y. The fixture asserts the stance is clear, the person has
  hands that can manipulate, has reciprocal reach to both the player and Wren, and has a reachable
  ground exit.
- **Authored stock.** One new 6-unit berry source in Wren's pack, split into three equal intervals:
  - ordinary (`[0,2)`);
  - `LotProtection.Favorite` (`[2,4)`);
  - `reservedBy` a newly admitted ID appended to `ReservationIds` (`[4,6)`).

  Wren's pack capacity and `AdmittedUnitCapacity` each grow by exactly 6.
  `Production.ValidateForAuthority` passes.
- **Finishing care.** The remedy, agreement, ordinary-use and permission definitions are reused
  read-only from a same-seed `OrdinaryCareWorldFixture` genesis. Before reusing them, the fixture
  checks:
  - same world and branch;
  - revision 0 everywhere, with no acknowledgments, treatments, pending care, uses or resource batches;
  - the same patient, helper, remedy stock and form pins;
  - none of the reused IDs collide with this composition's identity index;
  - total supply and every original wallet equal the original genesis.

  Genesis is then completed with `WorldCareAdmission.CreateFresh`, `WorldRestorativeAdmission.CreateFresh`
  and `WorldPaidExchangeAdmission.CreateFresh` (profile 32).
- **Encoding and persistence.** The fixture checks that encoding then decoding the genesis gives the
  same content hash. Tests only create and reopen it through `SqliteWorldAuthority.CreateNew` and
  `OpenExisting`, using the encoded genesis bytes.
- **Merchant ID ordering.** An optional parameter places the merchant's actor ID before or after the
  player's. The fixture asserts the requested order.

## What the new tests prove

### 1. Two buyers competing for one issuer's quotes

Test: `Two_eligible_humans_accept_one_issuers_quotes_in_one_batch_and_only_the_first_fills`, 4 cases.

**Funding.** The player is funded by the actual quest reward. The merchant's wallet starts at 0 and
receives 1 as a real fee on the player's purchase. Admitted supply and the sum of balances stay equal.

**Before the batch.** Wren issues one quote to each buyer, on distinct lots that would each split.
For both buyers the test asserts:
- distinct current control;
- ready, living, can manipulate;
- reciprocal reach;
- owns its storage and account;
- has funds and pack room;
- the quote pins the current lot exactly.

`AssertEachSettlesAlone` shows each acceptance succeeds alone from the same root.

**Same-batch outcome.** Both acceptances go into one batch through `SqliteWorldAuthority.Commit`, in
each submission order. Batches put their actions in actor-ID order, so the test asserts that
canonical order. In every case:
- the first eligible acceptance in that order fills;
- the other quote closes as Superseded, naming the winning acceptance and quote;
- the issuer's revision, the ledger and progression each advance by exactly one;
- the loser's balance, lot and pack are unchanged, and actor stats are unchanged;
- the loser's split-child ID is not created;
- both command IDs are kept.

After reopening, the head hash matches and each retry returns its original receipt bytes.

The two fixture variants make each buyer the winner in turn. In both submission orders the winner is
the actor that sorts first by ID, not the one submitted first.

### 2. Small recipient pack, counting raw and produced goods

Test: `A_small_human_pack_filled_by_raw_and_crafted_purchases_refuses_one_more_unit_until_a_sale_frees_space`.

**Filling the pack.**
1. The merchant has a capacity-2 pack that starts empty.
2. The player's earned purchase of 3 berries pays a 3-gil fee into the merchant's wallet.
3. Wren crafts a potion from her remaining berries.
4. The merchant buys one raw unit and the produced potion, so the pack holds 1 raw + 1 produced.

**The refusal.** The player, whose issuer revision is still 0, offers one more unit. Every other
condition holds, and raw contents alone would still fit. The public `SpotExchangeAuthority` reports
`InsufficientCapacity` for this acceptance. The world refuses it as `Unavailable`. Compared with an
independently decided empty step:
- every owner is unchanged except ordinary time and liquid state;
- no child lot is created and the child ID stays unoccupied;
- the refusal receipt is kept, and a retry returns it byte for byte.

**Contrast after freeing space.** The merchant actually sells the potion back to Wren. The same open
quote then settles and fills the pack to capacity. After reopening, the head hash and the refusal
receipt are unchanged.

### 3. Protected and reserved stock

Test: `Protected_or_reserved_stock_cannot_settle_keeps_its_metadata_and_is_withdrawn_while_equal_stock_sells`,
2 cases.

**Setup.** The constrained lot has the same source, item, owner and quantity as the ordinary
interval; only its admitted metadata differs. Issuance pins both lots exactly. Every acceptance
condition is checked, including room in the buyer's pack. Decided alone from the same root, the
ordinary acceptance succeeds and the constrained one is refused. The public component reports
`ProtectedStock` against `None`.

**Refused submission.** Compared with the root:
- only time advances;
- no child lot is created;
- the quote stays open with the same hash;
- the receipt is kept, and a retry returns it exactly.

**Withdrawal.** Wren withdraws the constrained quote. Inventory, production and progression are
unchanged, and no issuer revision is recorded.

**Contrast.** The equal ordinary interval then sells and splits a real child into the player's pack.

**After reopening.** The protection flag or the reservation stays on the lot, and the reservation ID
stays admitted. After reopening, the refusal and withdrawal receipts are byte-exact.

## Findings and remaining gaps

- **Fairness — design property, not a defect.** `WorldStepBatch` stores actions in a
  `SortedDictionary` keyed by actor ID. Within one batch, the lower actor ID always wins a same-step
  competition, whatever order the commands arrived in. That is deterministic. Whether it is fair
  across buyers is a product question; this work does not decide it.
- **World receipts don't name the reason.** The world returns a plain `Unavailable` for both
  protected stock and capacity. The specific reason comes from the public component, run against a
  book holding a single quote at market revision 0. The component cannot replay an issuer's earlier
  fills, which is why the capacity-refused quote is issued by the player.
- **Coverage limits.**
  - Only `Favorite` is tested at the world level. `Equipped`, `Heirloom` and `UserReserved` share the
    same runtime mask but are not tested one by one.
  - The reservation is an admitted ID only. The runtime has no general owner or record for an
    obligation, and no command changes protection after gameplay.
  - The capacity contrast frees space in the same world. A separately admitted, larger fixture was
    not run.
- **Still open:**
  - the same-step competition uses the privileged trusted-host batch, not network-authenticated
    multiplayer;
  - expiry of a dead issuer's quote;
  - control and roster transitions;
  - migration of the retained profile-27 save;
  - native UI and pacing;
  - process-kill safety;
  - scale and latency;
  - phase and production acceptance.
