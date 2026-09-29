# Saved material proposals: worker result

Bounded run for [ASSIGNMENT.md](ASSIGNMENT.md) from `76209f22ed7d082c8227bdff54c071704014a6bd`.
The saved-offer lifecycle is implemented in the complete world and the SQLite Host, and the four
permitted checks pass on one final build. It is **not independently accepted**: broader regression,
process-loss and several listed proofs remain open. Design, failures and limits are in
[IMPLEMENTATION_NOTES.md](IMPLEMENTATION_NOTES.md). No Git mutation was made.

## Decision the lead must review

The new interpretation is **profile 30**, not 29. The existing accepted
`ConsentedMaterialCarePolicyTests` pins 29 as refused by joint archive accounting, and naming the
new profile 29 failed that old gate. I did not edit the old test. Number 29 stays unassigned and
refused. Two comment-only files remain because the permitted commands cannot delete:
`WorldSnapshotCodecV29.cs` and `WorldStepCodecV29.cs`.

## Completed behaviour

- A sender saves an exact offer; the recipient later inspects, accepts or declines it; the sender
  withdraws it; world advancement expires it. Each submission has its own delivery and receipt.
- Acceptance executes the shared raw/produced material decision with body, reach, opportunity,
  physical, liquid and complete-root joins. It consumes both participants' opportunities.
- Offers reserve nothing. A stale exact lot or changed terms refuse without retargeting.
- A refused acceptance closes nothing; the offer stays open.
- Open offers cross an owner-published coordinated cut. Closed identities stay occupied.
- One SQL transaction publishes the root, receipt, identities, archive and head. Recovery replays.

## Checks executed

Final build `qualification/proposal-build-20260929T110523Z.json`; all runs sequential.

| Check | Result |
|---|---|
| build | accepted |
| core_proposals | 9 passed |
| host_proposals | 19 passed |
| legacy | 36 passed |

Three earlier failed runs are retained and explained in the notes: one test defect, one fixture
selection error and the profile-number conflict. No runtime rule, old assertion or bound was relaxed.

## Changed files

New in `Beta/Runtime/src/OpenFantasy.Core/Worlds/`: `WorldMaterialProposal.cs`,
`WorldMaterialProposalBook.cs`, `WorldMaterialProposalCodec.cs`, `WorldMaterialProposalIntents.cs`,
`WorldMaterialProposalResolver.cs`, `WorldMaterialProposalAdmission.cs`, `WorldActionCodecV16.cs`,
`WorldSnapshotCodecV30.cs`, `WorldStepCodecV30.cs`, and the two comment-only V29 files.

Edited in the same directory: `WorldCoordinatedContinuation.cs`, `WorldSnapshot.cs`,
`WorldSnapshotCodec.cs`, `WorldStepCodec.cs`, `WorldStepFrameCodec.cs`, `WorldStepBatch.cs`,
`WorldActionCodec.cs`, `WorldStepProcessor.cs`, `WorldAdmissionValidator.cs`, `WorldIdentityIndex.cs`,
`WorldConsentedMaterialAdmission.cs`, `WorldReservoirBinding.cs`, `WorldMaterialCareContinuation.cs`,
`WorldRestorativeResolver.cs`, `WorldRestorativeActions.cs`, `WorldRestorativeAdmission.cs`,
`WorldCareAdmission.cs`, `WorldResourceAdmission.cs`.

Host: new `Worlds/SqliteWorldAuthority.MaterialProposals.cs` and
`Simulation/MaterialProposalService.cs`; edited `Worlds/SqliteWorldAuthority.cs`,
`WorldJournalRecovery.cs`, `WorldJournalPolicy.cs`, `WorldDatabase.MaterialCareArchives.cs`.

Tests: new `Core.Tests/Worlds/MaterialProposalCodecTests.cs`,
`Hosting.Tests/Care/MaterialProposalWorldFixture.cs`, `Hosting.Tests/Care/MaterialProposalHostTests.cs`.
No existing test, fixture or schema SQL was edited.

Documentation: new `material-proposals.md`; edited `consented-world.md` and
`coordinated-world-continuation.md`. The architecture README does not yet link the new page.

## Incomplete requirements

1. Full Core suite, golden-byte format tests and the broad 195-case Host selection were not run.
   Shared Core files changed, so old-profile exactness is unverified beyond the 36 legacy cases.
2. Same principal controlling two actors is not exercised.
3. No control or session transition exists in the world; that proof remains missing. Reopening with
   unchanged control and refusal of a rolled-over delivery are qualified.
4. Readiness and body refusals have no new executable test. Out of reach does.
5. Expiry across a real encounter is untested; its outcome is recorded at the next exploration step.
6. Outcomes older than the current segment are not returned by the participant query.
7. Exception injection only. SIGKILL, native, power-loss and production scale remain open.
   Exports for process qualification are under the path in the `host_proposals` report.
8. Local trusted deliveries are not a network handshake. UI, NPC decisions, reserved offers,
   exchange and migration of older worlds are unchanged.
