# Saved-proposal repair: result

Bounded corrective unit for [REPAIR_ASSIGNMENT.md](REPAIR_ASSIGNMENT.md) against
[INDEPENDENT_CRITIC.md](INDEPENDENT_CRITIC.md). Items 1, 2, 3, 5 and 6 are implemented and proven
by the four permitted checks on one build. Items 4 and 7 are **partly open**; the exact cases are
listed below. Nothing here is independently accepted. No Git mutation, no schema, package or
toolchain change, and no pre-existing test, fixture or limit was edited.

## Checks executed

One build, all runs sequential, no failed run in this unit. Evidence is in `qualification/`.

| Check | Result | Elapsed | Evidence |
|---|---|---|---|
| build | accepted | 29.3 s | `proposal-build-20260929T114625Z.json` |
| core_proposals | 9 passed | 5.9 s | `proposal-core_proposals-20260929T114702Z.json` |
| host_proposals | 24 passed | 50.6 s | `proposal-host_proposals-20260929T114713Z.json` |
| legacy | 36 passed | 20.1 s | `proposal-legacy-20260929T114810Z.json` |

Host cases rose from 19 to 24. Elapsed times are not performance claims. Architecture pages were
edited after the checks; they are not pinned inputs. Process exports are under the
`fixtureExportPath` recorded in the `host_proposals` report.

## Completed repairs

Paths are under `Beta/Runtime/src/`.

| Item | Behaviour | Code |
|---|---|---|
| 1 | The anchor captures the capability and the exact open roster from the closing owner. Its hash gains the roster hash only with the capability. A successor must keep the capability and every carried offer exact, and may add only offers created after the boundary. | `OpenFantasy.Core/Worlds/WorldMaterialProposalContinuationContext.cs` (new), `WorldMaterialCareArchiveAnchor.cs`, `WorldCoordinatedContinuation.cs` |
| 2 | `FindRecorded` returns a participant's view of one exact identity after archival. It checks current control first, then the live owner, then one complete verified replay under the lock, pinned to the live head. | `OpenFantasy.Hosting/Simulation/MaterialProposalService.cs`, `Worlds/SqliteWorldAuthority.MaterialProposals.cs`, `Worlds/WorldJournalRecovery.cs`, `Worlds/WorldMaterialCareArchiveReplay.cs` |
| 3, 6 | Admission refuses an `Expired` outcome before its expiry count or before its earliest possible sequence, a creating count above its creating sequence, and a settlement whose participants are not the admitted bindings. Due and stale open offers stay admitted. | `OpenFantasy.Core/Worlds/WorldMaterialProposalAdmission.cs` |
| 5 | All four commands enforce `ExpectedActorRevision` after authorization and before any proposal lookup. | `OpenFantasy.Core/Worlds/WorldMaterialProposalResolver.cs` |
| 6 | The shared approval comment describes recorded-grant and current-binding comparison. | `OpenFantasy.Core/Authority/ConsentedMaterial/ConsentedMaterialApproval.cs` |

`WorldMaterialProposal.HasParticipant` is the one participant rule for the service and the owner.
No table, cache or second ledger was added.

## Executable evidence

Tests are under `Beta/Runtime/tests/OpenFantasy.Hosting.Tests/Care/`.

| Proof | Test |
|---|---|
| Owner-published cut, reopen, then both participants retrieve accepted, declined, expired and withdrawn results by identity. Wrong and rolled-over deliveries are refused for archived, live and missing identities. A third controlled actor and a missing identity read as nothing. The creation receipt stays byte-identical and the lookups publish nothing. | `MaterialProposalHostTests.An_open_offer_spans_an_owner_published_cut_...` (extended) |
| Actual anchor: exact successor decodes; a post-boundary creation and the carried offer's acceptance decode; forgotten, changed and invented grants and a dropped capability are refused with the exact reason. | `MaterialProposalContinuationTests.The_actual_anchor_admits_...` |
| A lived profile-28 anchor refuses a successor that gains the capability; its own successor still decodes. | `...A_consented_anchor_without_the_capability_...` |
| Early expiry by count and by sequence, impossible creation time and substituted settlement participants are refused, each beside an admitted control. | `...Admission_refuses_an_early_expiry_...` |
| Genuinely stale create, decline, withdraw and accept give `RevisionConflict`, for existing and missing offers, with unchanged owners, one elapsed step and positive controls. | `...A_stale_actor_revision_refuses_...` |
| The sender's own action blocks the acceptance in either order. The sender's movement sample is applied alone and spent beside a settlement. | `...A_settlement_spends_the_absent_senders_...` |

Refused roots are real canonical roots with one re-framed owner. The stored binding and every other
field stay the owner's exact bytes; the helper first reproduces the real bytes unchanged.

## Changes the lead should review

- **Profile-30 roots after a cut have new bytes.** The anchor hash now includes the roster hash.
  Roots before a first cut are unchanged. I expect the archived worker fixtures to replay unchanged
  because they hold no cut; I did not verify that.
- **Refusal order changed.** For all four commands a stale revision of the caller's own actor now
  gives `RevisionConflict` before any `Unavailable`, including a hold, an unready actor and a
  missing or closed offer. This is the order of ordinary actions and permission revocation.
- **The extended pressure test runs thirteen complete replays.** That is its added cost.

## Incomplete cases

1. **Item 4, one principal controlling two human actors: not exercised.** The admitted genesis
   builders give every role its own principal, and bindings are fixed at admission. A valid variant
   needs a builder outside my ownership (`Hosting/Content`, pre-existing fixtures). I did not
   re-author a genesis by hand.
2. **Item 4, two recipients of one sender in one batch: not exercised.** The journey addresses two
   actors with owned storage. A third controlled actor exists and serves the foreign lookup; I did
   not establish its storage, hands or reach.
3. **Item 4, body or readiness refusal beside a positive control: not added.** The lived base
   starts after both actors are ready. A not-ready state needs its own encounter journey, and every
   single-command step also advances the delay. Out of reach keeps the worker's existing pair.
4. **Item 7, tactical advance past expiry: not exercised.** It needs a second real encounter after
   saving an offer; the entry helper is private to a pre-existing fixture. No time policy changed.
5. **Item 2, damaged storage under the lookup: no executable proof.** The lookup uses the same
   replay and the same reopen-required path as the other historical reads. Damage made before
   opening is refused by recovery first; I did not damage a database under a live owner.
6. **Item 2, lookup duration is not equalized** between live, archived and missing identities. The
   reference page states this.
7. **Item 1, older anchor bytes.** Only the 36 legacy cases ran here. Golden vectors, full Core and
   the broad Host selection remain the lead's checks.
8. **Core focused selection is unchanged at 9.** The anchor proofs need a complete lived world and
   internal constructors, so they live in the Host tests.

Still open from the original assignment and unchanged here: the simultaneous-pair union, session
scheduling and rebinding, control transition, encounter expiry, process kill, native, NPC, transport
and retained-save proofs.

## Architecture

Updated in the same increment: `material-proposals.md` (command contract, structural admission,
carry across a cut, query boundary), `coordinated-world-continuation.md` (proposal context in the
anchor) and `consented-world.md` (shared approval and capability). The README link is still the
lead's file.
