# Saved material proposals: implementation notes

Worker record for the bounded assignment in [ASSIGNMENT.md](ASSIGNMENT.md), begun from
`76209f22ed7d082c8227bdff54c071704014a6bd`. Astra owns integration, commits and acceptance.
The timeless explanation is in [material proposals](../../../Runtime/docs/architecture/material-proposals.md).
No Git mutation, package, network or toolchain change was made.

## Ownership and lifecycle design

| Decision | Choice |
|---|---|
| Owner | `WorldMaterialProposalBook` on `WorldCoordinatedContinuation`: open offers plus current-segment outcomes. It owns no stock, permission or receipt. |
| Interpretation | New named world/step profile **30** and action codec **16**, fresh admission only. |
| Identities | Proposal and split child are derived from the creation command under different ordinals. Creation, terminal command, both results and child stay distinct permanent identities. |
| Saved grant | The sender's submitted `ConsentedMaterialCommand` plus the delivery recorded at creation. Valid for this proposal only. |
| Settlement | The recipient's own later command. No sender command or delivery is manufactured. The shared `ConsentedMaterialTransferStep` decides stock. |
| Evidence | `WorldMaterialProposalSettlement` inside the accepted outcome; production replays its produced movements. |
| Reservation | None. Two offers may pin the same lot; the first settlement makes the other stale. |
| Expiry | Absolute advancing-step count `E`, `C < E <= C + 65,536`. Order per step: actions, time boundary, expiry, movement. |
| Failed acceptance | **Closes nothing.** Only accepted, declined, withdrawn and expired are terminal. |
| Bounds | 16 open, 64 outcomes per segment, bounded frames. Full bound refuses; never evicts. |
| Cut | Reserves one outcome per open and newly created offer. Open offers cross the cut. |
| Host | `SubmitMaterialProposalCommand` authorizes, selects and publishes under one lock. `MaterialProposalService` is the query boundary. |

Profile 30 does not interpret the simultaneous pair. Its pair evidence owner stays empty and exists
only to keep the consented production capability.

## Why the profile is 30 and not 29

The existing accepted `ConsentedMaterialCarePolicyTests` pins profile **29** as refused by joint
archive accounting. My first implementation named the interpretation 29, which made that old
assertion false; the legacy check failed on exactly that case. I did not edit the old test.
I renumbered the new interpretation to 30, so every old gate stays exact and untouched.

Consequences for the lead to decide:

- Number 29 is unassigned and refused everywhere. My Core test asserts 29 and 31 are refused.
- `WorldSnapshotCodecV29.cs` and `WorldStepCodecV29.cs` are comment-only. The permitted commands
  cannot delete files. They should be removed at integration.
- Using 29 instead requires changing the old policy test's example, which is the lead's file:
  `Beta/Runtime/tests/OpenFantasy.Hosting.Tests/WorldStorage/ConsentedMaterialCarePolicyTests.cs:65`.

## Executed checks

All checks ran sequentially through the four permitted runner shapes. Final build:
`qualification/proposal-build-20260929T110523Z.json`.

| Check | Result | Elapsed | Evidence |
|---|---|---|---|
| build | accepted | 82.7 s | `proposal-build-20260929T110523Z.json` |
| core_proposals | 9 passed | 7.8 s | `proposal-core_proposals-20260929T110652Z.json` |
| host_proposals | 19 passed | 64.9 s | `proposal-host_proposals-20260929T110705Z.json` |
| legacy | 36 passed | 16.9 s | `proposal-legacy-20260929T110815Z.json` |

Elapsed times are not performance claims. The legacy check ran in the background once while I
wrote documentation, which is outside the pinned inputs; no source changed during any pinned test.

## Retained failures and corrections

| Run | Failure | Correction |
|---|---|---|
| `proposal-core_proposals-20260929T105552Z` | 8 of 9. My test expected a flipped final byte to fail decoding. That byte is part of a recorded content hash, so the frame stays canonical. | Test defect. The test now asserts a damaged header is refused and a changed hash decodes to a different owner hash. No runtime change. |
| `proposal-host_proposals-20260929T105640Z` | 17 of 18. My pressure helper required a multi-unit raw lot the helper no longer holds after its real craft. | Fixture selection. The helper pins one unit of any actual unreserved lot, raw or produced. No runtime rule, assertion or bound changed. |
| `proposal-legacy-20260929T110105Z` | 35 of 36. The old policy test pins profile 29 as refused. | Design conflict, described above. Renumbered to 30; old test untouched. |

Intermediate accepted builds and runs are retained beside these. Only the final build's results count.

## Evidence coverage

| Requirement | Test |
|---|---|
| Separate creation and acceptance, real commits, disposal and reopen, raw split | `A_raw_split_offer_is_saved_inspected_and_accepted_...` |
| Produced split, recraft, self-use, producer ancestry | `A_produced_split_offer_keeps_its_producer_...` |
| Wrong deliveries, roles, missing offers, decline, duplicates, altered content, changed terms | `Wrong_deliveries_and_roles_learn_nothing_...` |
| Withdrawal, expiry boundary, lifetime edges | `Withdrawal_and_the_exact_expiry_boundary_...` |
| Competing use, stale exact lot, no reservation | `Competing_use_refuses_the_stale_offer_...` |
| Coupled physical refusal | `Coupled_physical_storage_refuses_...` |
| Out of reach, with positive control | `Participants_who_walked_apart_...` |
| Owner-published cut with a spanning open offer, archive retrieval, identity refusal | `An_open_offer_spans_an_owner_published_cut_...` |
| BeforeCommit and AfterCommit for creation and acceptance, exports | `Injected_loss_publishes_...` (4 cases) |
| Corrupt head, genesis, journal, receipts, identities | `Recovery_refuses_damaged_...` (7 cases) |
| Codec round trips, negatives, profile routing, exact bounds | `MaterialProposalCodecTests` (9 cases) |

The cut is published by the owner under real outcome pressure from withdrawn offers. The test
asserts 60 to 64 retained outcomes and the spanning open offer in the closing root, and it asserts
the exact reservation edge for every batch. I did not separately record the observed outcome count.
Exports for the lead's process qualification are under the path recorded in the `host_proposals`
report. Exception injection is not SIGKILL, native or production proof.

## Incomplete requirements and risks

- **Broader regression not run.** Only the four permitted checks ran. I changed shared Core files
  (`WorldStepProcessor`, `WorldStepBatch`, `WorldReservoirBinding`, `WorldAdmissionValidator`,
  `WorldMaterialCareContinuation`, `WorldCoordinatedContinuation`). Every change is gated on the
  new owner or intents, but the full Core suite, golden-byte format tests and the 195-case broad
  Host selection are unverified by me.
- **Same principal controlling two actors** is not exercised. The fixture has distinct principals.
  Control is checked per actor, so nothing forbids it, but there is no executable proof.
- **No control or session transition exists** in the world; control bindings are fixed at admission.
  Disposal and reopening with unchanged control are qualified, and a rolled-over delivery is refused.
  The proof of an actual transition remains missing. A changed binding refuses the saved grant.
- **Readiness and body refusals** reuse the pair's checks but have no new executable test.
- **Expiry during an encounter.** Tactical boundaries advance the step count without interpreting
  proposals. A due offer is unacceptable at once and reads as expired; its outcome is recorded at
  the next exploration boundary. This path is untested against a real encounter.
- **Older outcomes** are not answered by the participant query after a cut. They are available
  through the caller's own receipts and privileged archive retrieval.
- **A stale offer stays open** until withdrawn or expired and occupies one of 16 open slots.
- **The Host boundary selects single-command steps.** The Core interprets several proposal commands
  in one batch; the pressure test uses that through the privileged batch boundary.
- **Bounds are a development envelope.** The owner adds at most 283,200 bytes to the new profile's
  coordination frame. Maximum-root behaviour and production scale remain separate gates.
- `LocalWorldSession`, native controls, NPC decisions, transport authentication, reserved offers,
  exchange and migration of older worlds are unchanged and remain later work.
