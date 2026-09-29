# Saved-proposal critic review

Read-only source reasoning against the working tree. Nothing was built or executed; every construction below is reasoned, not run.

## Verdict

Not acceptable yet as the complete lifecycle. I found no path that moves stock twice or invents sender authority. Two P1 gaps remain: cut carry is not structurally enforced, and participants lose terminal outcomes at the next cut.

## Confirmed defects

### P1-1 The anchor enforces neither proposal capability nor carried grants
- **Where:** `WorldMaterialCareArchiveAnchor.cs:43-47, 73-95`; `WorldCoordinatedContinuation.cs:22-29`; `WorldSnapshotCodecV30.cs:108-114`; `WorldMaterialProposalAdmission.cs:66-78`.
- **Cause:** the anchor is unchanged and holds only `Production.SupportsConsentedWorld`. `ValidateSuccessor` and the continuation constructor never read `Proposals`. Open-offer admission only requires `CreatedSequence <= world.Revision`.
- **Construction:** seal a real profile-30 root K holding open offer P, then re-encode `Continued` three ways:
  - with an empty book;
  - with an added grant P′ whose `CreatedSequence <= K.Revision`, which K never held;
  - under a profile-28 anchor as a V30 successor. The reverse (V28 bytes under a 30 anchor) is inferred; I did not read the V28 codec.

  `WorldSnapshotCodec.DecodeCoordinated(bytes, anchor)` admits each.
- **Reach:** Host replay from genesis refuses these today, because the head must equal replay (`WorldJournalRecovery.cs:197-206`). The structural contract is the only guard wherever a root is admitted from anchor plus bytes, which is where checkpoint-tail recovery is headed. P′ is fabricated sender consent.
- **Repair:**
  - The anchor constructor captures `closing.Coordination.Proposals?.Open` as a bounded roster (at most 16 × 1,280 bytes); no root or parent is retained.
  - Append the roster hash to the anchor frame only when present, so 27/28 anchor bytes stay exact.
  - `ValidateSuccessor` requires equal capability.
  - Every successor open or outcome proposal with `CreatedSequence <= ClosingWorldSequence` must match a carried entry by `ContentHash`.
  - Every carried entry must appear once in open or outcomes.

### P1-2 Terminal outcomes vanish for participants at the next cut
- **Where:** `MaterialProposalService.cs:59-81`; `WorldMaterialProposalBook.cs:112`; `material-proposals.md:140-141`; `MaterialProposalHostTests.cs:435-446`.
- **Cause:** `Project` reads only the live book and `ContinueAcrossCut` empties outcomes. After a cut `Find` returns null, identical to never-existed. The sender cannot read the recipient's receipt, and `Expired` has no receipt at all.
- **Test gap:** the cut test retrieves only through privileged `ReadMaterialCareArchiveForHost`, and that archive holds only pressure withdrawals.
- **Construction:** sender saves, recipient accepts, a later batch cuts under proposal, care or production pressure. The sender's goods are gone, `Find` is null, and their only receipt says creation succeeded.
- **Classification:** this is lifecycle behaviour, not indexing scale. Cuts are automatic, so the visibility window can be one step.
- **Repair:** add a participant-authorized recorded lookup. Authorize as `ReadForController` does; when absent from the live book, one verified replay pass inspects each retrieved closing root and returns the same filtered view. No new table is needed. Correct the doc sentence that presents a privileged read as a participant path.

### P2-3 Create, decline and withdraw ignore `ExpectedActorRevision`
- **Where:** `WorldMaterialProposalResolver.cs:57-103`; only `:139` checks it.
- **Contrast:** `WorldActionResolver.cs:26`, and `WorldRestorativeResolver.cs:21`, which covers the pure declaration `RevokeCarePermissionIntent`.
- **Effect:** the field is canonical in codec 16 (`WorldActionCodecV16.cs:34`) and then ignored, so any value is accepted. Harm is low, but the divergence appears in neither the notes nor the reference.
- **Repair:** enforce it. Check presence revision once in `Resolve`, after authorization and before proposal lookup. `RevisionConflict` there concerns only the caller's own actor, so it discloses nothing.

### P2-4 Local impossible states are admitted
- **Where:** `WorldMaterialProposalAdmission.cs:41-62`.
- **Missing checks:**
  - `Expired` requires `ExpiresAtAdvancingStep <= world.AdvancingSteps`.
  - `Expired` requires `WorldSequence - CreatedSequence >= Expires - CreatedAdvancingStep`. This assumes at most one step per commit, consistent with `WorldAdmissionValidator.cs:22`.
  - `CreatedAdvancingStep <= CreatedSequence`.
  - Accepted settlements get no participant check, unlike pair evidence (`WorldConsentedMaterialAdmission.cs:45`).
- **Safety of these checks:** none rejects due-but-open records. They also hold during pre-boundary candidate validation, because expiry outcomes are created only after the boundary (`WorldStepProcessor.cs:575-579`).

### P3-5 Cleanup
Delete the comment-only `WorldSnapshotCodecV29.cs` and `WorldStepCodecV29.cs`.

## Risks checked, no defect found

| Risk | Finding |
|---|---|
| Sender opportunity spent twice | No. The first settlement adds the sender to `worked` (`WorldStepProcessor.cs:530-534`); the second refuses (`Resolver.cs:128`). Rollback clears `performedWork` (`:500`). Both participants' movement is skipped (`:602`). |
| Conflict with the sender's own action | Acceptance refuses when the sender has any action in the batch (`Resolver.cs:127`), even a refused one. Conservative and order-independent. |
| Grant reuse | No invented authority. The recorded delivery is compared for equality with the current binding (`Resolver.cs:126`, `ConsentedMaterialApproval.cs:48-51`). That type's "verified current delivery" comment no longer matches and should be reconciled. |
| Same principal, both actors | Nothing forbids it; keys are per actor and per `(principal, command)`. Untested. |
| Older bytes | Every join is an explicit allow-list; the only range checks are `< 3/4/5`. Unproven until the golden suites run. |

## Profile 30

- **Skipping 29 is sound.** Numbers are nominal and every join names its profiles. 29 is refused at `WorldStepBatch.cs:52`, both codecs, journal policy, recovery and schema selection. The old test's 29 is an arbitrary example, so the rationale is convenience. Keeping 30 preserves every pinned test file at the cost of a gap; no test evolution is needed.
- **Excluding the pair is acceptable only as a named sibling interpretation**, not a successor of 28.
  - A world picks one consent route at genesis.
  - V30 bytes carry an always-empty evidence owner that acts as a capability flag.
  - Admitting both would need joint opportunity accounting between `WorldMaterialPairing` and the proposal checks, so exclusion is the smaller sound choice.
  - Record the owed composition in the handoff.

## Intentional limitations to keep explicit

- Every Host submission, refusals included, is its own advancing step (`SqliteWorldAuthority.MaterialProposals.cs:37-51`). Any controller can advance shared time and other people's expiry clocks. This must join the host-selected step before real multi-actor sessions.
- Session is part of the binding. A legitimate same-principal rollover would strand open offers until withdrawn or expired. No rebind exists, so that proof stays owed.
- Submissions during holds or encounters throw dispatch refusals rather than returning receipts.

## Acceptance boundary

Accept as "fresh profile-30 saved offers through the single-command Host boundary" after P1-1, P1-2 and P2-3, with P2-4 in the same change. Keep open: same-principal control, control transition, encounter expiry, process kill, and the full Core and golden-byte regression.

## Highest-value contrasting tests

1. **Anchor:** the real successor is admitted; a dropped grant, a fabricated pre-boundary grant and both cross-profile successors are refused. 27/28 anchor hashes stay byte-identical to existing vectors.
2. **Outcome after cut:** accepted, declined and expired offers are each retrieved by both participants after a cut and reopen. A non-participant and a wrong delivery get the same refusal as a missing identity.
3. **Core multi-command batch:** two recipients with one sender; sender action plus acceptance; sender movement plus acceptance; first acceptance rolled back, then the second succeeds. Assert receipts, positions and the book.
4. **Stale revision** on create, decline and withdraw, with positive controls.
5. **Tactical advance past expiry:** the record stays open, reads Expired, acceptance is refused, and the outcome is recorded at the next exploration step.
6. **Same principal** controlling both actors through the Host boundary.
7. **Profile-30 root carrying pair evidence** is refused at admission.
