# Paid-world boundary evidence completion — result

Status: COMPLETE FOR PRIORITIES 1, 2 AND 4; PRIORITY 3 UNPROVED (source boundaries only). Opus 5.5 worker.
I edited only the three owned files. No runtime, architecture, old test, fixture or Git state changed.
No runtime defect found.

## Terminal evidence (all under `qualification/`)

| Run | Result |
|---|---|
| `build-20261001T004343Z-dbc61` | accepted; 513 old tests and 23 specs preserved |
| `new-host-20261001T004408Z-42c74` | **FAILED, retained**: 23 run, 19 passed, 4 failed. Both original batch theories put two acceptances by one actor in one batch, and `WorldStepBatch` refuses that at construction (`WorldStepBatch.cs:139-144`, one action per actor). The tests were wrong, not the runtime. I rewrote them; no gate was weakened. |
| `build-20261001T004657Z-c6910` | accepted; preservation exact before and after |
| `new-host-20261001T004709Z-150ab` | **accepted: 23/23 passed** (15 preserved WorldExchange cases plus 8 new); source unchanged; assemblies pinned |

## New cases (all pass in the accepted run)

All of these use a fresh admitted profile-32 world, earn funds through the existing quest, run commands through the real `SqliteWorldAuthority`, and reopen to an exact head.

1. `A_handless_then_slain_wolf_and_a_recovering_issuer_cannot_settle_until_a_ready_human_does`
2. `Battle_defeat_and_pending_care_refuse_whole_steps_then_the_treated_issuer_settles_its_terms`
3. `Two_acceptances_against_one_issuer_in_one_batch_fill_at_most_once` (wolfFirst false/true)
4. `A_saved_gift_and_a_paid_fill_on_one_lot_in_one_batch_block_each_other_without_effect` (paidFirst false/true)
5. `A_held_quest_reward_cannot_share_a_batch_with_a_fill_but_funds_it_in_causal_order`
6. `A_seller_collecting_its_own_fee_is_credited_once_through_hosted_replay`

**Conservation checks.**
- A refusal that publishes a receipt goes through `AssertOwnersUnchanged`: the inventory, production, progression and paid-book hashes are exact, and the world revision advances by one. That one-revision advance is the normal passive time step.
- A whole-step refusal leaves the exact head hash unchanged. The command identity stays unoccupied, and this is checked again after reopening.

## 1. Issuer unavailable at settlement

**Battle.** Tested with an actual entered encounter (test 1) and live combat inside `PrepareDefeat` (test 2). The precondition `Pacing.Encounter != null` is asserted.
- `SubmitPaidExchangeCommand` throws `AuthorityDispatchException` and publishes nothing.
- The source of the refusal is the batch-selection check `WorldCommitPreparation.ValidateDispatch` (`WorldCommitPreparation.cs:72-83`), not the paid resolver.

**Dead issuer: the defeated player (test 2).** At the encounter Outcome with the player at 0 HP (asserted), the whole step is refused. A 0-HP person exists only in Outcome or pending care, and the world admits no advancing step there (`WorldStepProcessor.cs:94-105`). So the resolver's HP check cannot be reached for a person.

**Pending care (test 2).** After a real acknowledgment, with `Care.Pending != null` asserted, the whole step is refused.
- This state only exists inside the encounter outcome. The test cannot separate the pacing refusal from the pending-care rule; both forbid the step before any resolver runs.
- Recovery: after real treatment, victory and the debt-retiring steps, the paid book hash, the ledger revision, every account (balance and revision) and the pinned lot revision equal their values just after issuance. The same terms then settle exactly.

**Readiness / Wait, issuer side (test 1).** After a real victory and release, these preconditions are asserted:
- `GetDebt(player).RemainingDelay > 0` and `!CanAct(player)`;
- `CanAct(helper)`;
- reach in both directions between the two.

The helper's acceptance of the player's quote is refused with `Unavailable` by the resolver's own issuer check (`WorldPaidExchangeResolver.cs:149-152`), and all owners are exact.

Recovery: real exploration steps retire the debt, and the same fixed terms settle. The test checks:
- player −1, helper +1, supply unchanged, ledger revision +1;
- the child lot is in the player's bag;
- the issuer's sibling quote is `Superseded` by this acceptance.

**Dead wolf (test 1).** The wolf is killed by the player's actual strikes (0 HP asserted) while it still holds 1 coin.
- As the counterparty, its acceptance is refused at the caller gate (`WorldPaidExchangeResolver.cs:50`).
- As the issuer, the player's acceptance of its funded bid is refused at the issuer gate, after the player is ready.
- It cannot withdraw its own quote.
- That quote stays open and exact across reopening.
- No recovery exists: nothing in the admitted profile revives an actor. The quote can only expire, and that expiry was not exercised (its lifetime is 1,024 steps).

**Absent manipulation (test 1).** Before the battle the wolf is alive, ready, reachable both ways, has no manipulation, has an empty bag with capacity ≥ 1, and holds 1 coin. That coin came from a real third-party fee paid out of the player's quest funds. Settlement is refused with the wolf as counterparty and again with the wolf as issuer (`WorldPaidExchangeResolver.cs:166-171`), with exact owners in both cases.

**Caveats.**
- Several gates share the `Unavailable` code. Each refusal is attributed using the asserted preconditions and the resolver's check order.
- **Not proved:** a paid counterparty who is alive but still carrying recovery debt. It uses the same `CanAct` predicate as the dead-caller case, which is proved, but it was not exercised for a paid caller.

## 2. Multi-command ordering (privileged `Commit(batch)`; not a network-authentication claim)

**Two acceptances against one issuer.**
- Each acceptance settles when decided alone from the same root.
- One counterparty submitting both in one batch is refused at batch construction.
- The only other possible counterparty in this world is a handless wolf (2 humans + 2 wolves). With it in the batch, both orders give exactly one fill: the player's.
  - That fill supersedes both siblings in the same sequence.
  - The wolf's acceptance gets `Unavailable`, and its split child is neither created nor occupied.
  - Balances, revisions and `ActorsOf` are exact.
  - After reopening, each retry returns its original receipt bytes and the head is unchanged.
- A race between two valid human counterparties needs a larger admitted roster, which is outside this assignment.

**Gift vs paid on one lot.**
- One actor cannot accept both in one batch (same construction rule).
- With distinct acceptors, each acceptor is the absent party of the other settlement (gift sender = paid counterparty; gift recipient = paid issuer). The issuer rule (`WorldPaidExchangeResolver.cs:150`) and the sender rule (`WorldMaterialProposalResolver.cs:140`) then refuse both, in either order:
  - zero effect: owners, proposal book, lot revision and custodian are all exact;
  - command identities are occupied, and no child lot is created.
- Each settles when decided alone from the same root.
- The gift alone then settles in the next step, and the bid stays open but unfillable.
- The paid refusal is pinned as `Unavailable`. The gift refusal is asserted only as "not None"; its exact code is not pinned.

**Earned reward and fill on the same account.** These can never share a batch:
- the reward needs a held step (the giver's dialogue hold);
- paid intents are admitted only in advancing steps (`WorldStepBatch.cs:83-86`), so the combined batch is refused at construction;
- during the hold, the trusted submission is refused for the whole step.

In separate steps, the reward credits the player and the later fill debits the same account:
- ledger revision +1, account revision +1, progression revision +1;
- the reward has a resource batch and the fill has none;
- reopening reproduces the head exactly.

## 3. Late failure, capacity, protected or reserved stock: NOT PROVED (no behavior test)

**Rollback after a successful paid plan.** `WorldStepProcessor.cs:501-527` restores every owner, including coordination, progression and production, and clears created identities.
- One candidate late failure is a pending runoff receiving obligation (`WatershedRunoffCapacityState.cs:251-268`), which the resolver's capacity check (`SpotMaterialTransferStep.Fits`, `SpotMaterialTransferStep.cs:145-156`) does not cover.
- I did not establish whether a profile-32 world can have a runoff terminal on a buyer's resident storage. Reachability is unknown.

**Insufficient capacity** (`SpotMaterialTransferStep.cs:34-38`). The wolves' bags hold only 1 unit (`EncounterFarmWolf.cs:49`), but the manipulation gate is checked before capacity, so a wolf cannot reach it. Human storage capacities were not established.

**Protected or reserved stock** (`SpotMaterialTransferStep.cs:136-139`; issuance does not check it).
- I found no protected or reserved lot owned by a resident in this genesis, and no command that protects a lot was located.
- Whether a legitimate fresh genesis could express one is not established.

## 4. Seller as fee payee: PROVED (test 6)

Price 2 plus a fee of 1, with the fee paid to the seller's own account.
- Buyer −3 and seller +3. Each account's revision advances once, because the credits are combined.
- Every other account is exact, including the reserve.
- Ledger and progression each advance by one.
- Durable reopening reproduces the head, and a retry returns the original receipt bytes.

## Remaining unproved

- Everything in section 3.
- A paid counterparty who is alive but carrying recovery debt.
- Expiry of a dead issuer's quote.
- Two valid human counterparties racing in one batch, which needs a larger admitted roster.
- The gift refusal's exact rejection code.

Process-kill testing and SQL-tampering reopen were out of scope and not run.
