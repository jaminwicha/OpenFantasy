# Independent encounter-outcome critic review

Status: **complete (read-only, bounded)**. Reviewer: `claude-opus-5-5[1m]`, one pass. I used only Read, Glob and Grep on the
frozen export, plus Write on this file. I did not run any experiment. Every regression below is a **suggestion and was not
run**. This review does not certify the implementer's final source or results: Astra must compare the delivered source
with the frozen export (`sourceHead cdeb919`, captured 2026-10-01T23:49:04Z).

## Verdict

The managed Core/Host law is coherent within its bounded scope. The managed qualification uses genuine committed sequences.
- **Reading** is a separate controller fact. It is allowed for an incapacitated actor and refused for side-zero actors and
  forged deliveries.
- **Completion** is world-owned, actionless and zero-duration. It is withheld while care is pending, and care treatment
  clears readings in the same candidate.
- The initiator release is refused in profile 35 at three layers.
- Profiles 1–34 dispatch is unchanged.

**The delivery is not acceptable as complete.** Four required gates are missing: outcome-fixtures, outcome-native,
outcome-auth and outcome-durability. Separately:
- **F1** (unchecked rules pin) should be fixed before acceptance.
- **F2–F4** must be fixed, or their claims narrowed, before the native journey renders this projection and copy.

## Findings

### F1 — P1 — The outcome rules pin is never validated (lead L3: confirmed by source)
- **Location:** `src/OpenFantasy.Core/Encounters/EncounterOutcomeState.cs:67-86`; `EncounterOutcomeCodecV1.cs:44,51`;
  `Worlds/WorldEncounterOutcomeAdmission.cs:106-131`. `WorldSnapshot.cs:421-424` only requires that a successor keeps its
  predecessor's pin. Compare `EncounterRetreatState.cs:140` and `EncounterActionCodecV1.cs:42`, which both reject a foreign pin.
- **Trigger:** a profile-35 root whose outcome owner carries any other 32-byte pin.
  - It re-encodes canonically, passes `Validate`, and is then interpreted under `encounter.outcome.v1`.
  - A future `v2` root would therefore be silently misread by v1 code.
  - The admission hash frames the constant `Pin`, but nothing re-derives it on open, so the stored pin and the admission
    can disagree without detection.
- **Effect:** the supported-law boundary that the pin exists for is not enforced.
- **Exposure:** the production native selection compares exact genesis bytes, so it is protected. Any path that decodes a
  supplied profile-35 genesis or root is not. Example: `OpenCargoOutcomesFixtureAsync` checks only profile, revision 0 and
  `Find`.
- **Correction:** reject `rulesPin != EncounterOutcomeState.Pin` in the owner constructor. Decode then turns this into a
  `FormatException`. This mirrors the retreat owner.
- **Regression (unrun):**
  - Owner test: constructing the owner with `ContentHash.Compute([7])` throws.
  - Owner test: decoding owner bytes with the pin replaced throws `FormatException`.
  - World test: replace the 32 pin bytes inside a fresh profile-35 root; `WorldSnapshotCodec.Decode` must throw.

### F2 — P2 — The "historical" completed view projects live, unbounded remote condition (lead L1: confirmed by source; live leak not demonstrated)
- **Location:** `Hosting/Simulation/EncounterOutcome/LocalEncounterOutcomeView.cs:99-108,119-125`;
  `client/scripts/EncounterOutcome/EncounterOutcomeOverlay.cs:63-65`.
- **What the code does:** at the Completed stage, only the result, side, readers and sequence come from the record.
  `Participants` reads current `CurrentHp`/`MaxHp` from progression for every positive-side combatant. There is no floor,
  sightline or distance bound.
- **How long it persists:** until the next episode begins. After a completed defeat that is permanent, because there is no
  re-entry. The terminal overlay renders "HP x/y" for every opponent indefinitely.
- **Unbounded during Outcome as well:** `Project` is not gated on the observer being a participant. By contrast,
  `LocalEncounterActionView.cs:48-49` exposes only the observer's own HP.
- **Doc mismatch:** `encounter-outcome.md:77-80` calls this "the record".
- **Why no live leak is demonstrated:** in current content no remote participant's HP changes after completion. The chief
  is static, and `WorldRestorativeActions.cs:78` refuses HP-0 recipients. It becomes a real leak with any later change, such
  as allies in the specified 1–4 player world, regeneration or care.
- **Correction:** at the Completed stage, project record facts plus the observer's own condition. Show other participants'
  current condition only under the same observation bound used by `LocalRoadThreatView`: same floor, clear sightline,
  three-cell bound.
- **Regression (unrun):**
  - After the victory completion, walk to Kegga and assert that no non-observed participant's HP or MaxHp is projected.
  - A projection test where an observer is outside the fight.

### F3 — P2 — Defeat and care copy states a false reason and contradicts offered care (lead L2: confirmed, extended)
- **Locations and problems, all in `client/scripts/EncounterOutcome/EncounterOutcomeText.cs`:**
  - `:57-60`: shows "No admitted care can reach you" whenever no offer is `CanAcknowledge`. It ignores
    `LocalCareAcknowledgmentView.Rejection` and treats `care is null` (no agreement) as a reach problem.
  - `:76-78`: the terminal text always says no care "can reach you". The actual reason is the policy:
    - aftermath care requires the Outcome phase (`WorldCareActions.cs:17`);
    - ordinary care refuses HP-0 recipients.
  - `:42-43`: the "Accept defeat" detail says "this world has no rescue, revival or respawn". The same screen offers
    "{helper} can help (care)" (`:58-59`, `:70-71`).
  - Nothing warns that reading declines that care permanently. Completion follows at the next 50 ms cutoff
    (`LocalWorldSession.cs:24,413-418`).
  - The CarePending blocker says the helper "is preparing care" even when `LocalCareView` reports pending status
    `Unavailable` (`LocalCareView.cs:58-60`), which is a permanent hold (RESULT gap 4).
  - Latent: an incapacitated winner (`YourSideStands` at 0 HP) is labelled "Continue — returns you to exploration"
    (`:24-25,40-41`), but becomes terminal once completed.
- **Correction:** derive the stated reason from the care view: no agreement, out of reach, rejected, or pending but
  unavailable. After completion, state the completion policy instead of a reach problem. When care is offered, say that
  "Accept defeat" declines it and ends the fight.
- **Regression (unrun):** answers lead Q2.
  - Use the 1-HP calibrated wolf defeat with Wren's offer `CanAcknowledge`.
  - Read immediately, without asking for care.
  - Assert completion, the terminal text naming declined care / completion policy rather than reach, and an identical root
    after close and reopen.
  - Add pure-client tests of each reason, using the already-linked `EncounterOutcomeText`.

### F4 — P2 — "Read the defeat, then still ask for care" is documented and tested, but players cannot reach it
- **Locations:** `docs/architecture/encounter-outcome.md:36-38`;
  `tests/.../Simulation/EncounterOutcome/EncounterOutcomeJourneyTests.cs:323-339`.
- **Why the test passes:** the manual clock never runs an unqueued cutoff between the reading and the care requests. Each
  `Submit` routes its tick to `TickCare` (`LocalWorldSession.cs:406-410`).
- **What a live session does:** the first cutoff after a reading selects completion (`:413-418`, `StepInterval` = 50 ms).
  The offer captured before the reading is stale (RevisionConflict, test `:329-330`). A player would have to read the new
  view and submit care within about one cutoff.
- **Effect:**
  - The architecture claim misleads.
  - The proof covers an interleaving that is not offered to players.
  - Lead Q4's actual contrast is not exercised: one human has read, another human requests care, and treatment clears the
    first reading. The test uses a single human.
- **Correction:** document a reading as final acceptance that declines care, and align the UI with F3. If a care window
  after reading is intended, make it an explicit, tested rule.
- **Regression (unrun):**
  - Read, then run ordinary unqueued cutoffs. Assert completion on the next cutoff and care refused afterwards.
  - Two-human variant: the non-reading human's care clears the other human's reading. This needs a care agreement for that
    human; if the content lacks one, record it as a future obligation.

### F5 — P3 — A non-participant observer is told "You have read this outcome"
- **Locations:** `LocalEncounterOutcomeView.cs:90-92` gives a non-required observer `AwaitingOthers`. Then:
  - `EncounterOutcomeText.cs:29-31` says "You have read this outcome".
  - `:56` says "while you read the result".
  - `EncounterActionOffers` offers the observer a disabled read button.
- **Reachability:** the declared side-zero second wolf would hit this if it had a session. It is latent for the single local
  player, who is always a participant.
- **Correction:** add an observer branch that says the fight is between others and the world waits for its players.
- **Regression (unrun):** project the two-human fixture with observer `SecondWolf` and assert the stage and text.

### F6 — P2 — Missing world-level admission and negative obligations for the new owner (requirement 7)
`EncounterOutcomeOwnerTests` are owner-local. `CargoOutcomeAdmissionTests` cover only selection. No test feeds a profile-35
root to `WorldSnapshotCodec.Decode` with any of these owner contents:
- a reading by a side-zero or host actor;
- a reading outside Outcome;
- a stale archive hash;
- a record naming the current episode;
- `FinalizedCount > EncounterCount`;
- a foreign pin (F1).

`Validate` (`WorldEncounterOutcomeAdmission.cs:113-118`) also does not tie a record whose episode is the retained archive to
that archive's `ActionHash`. This is acceptable only if it is documented as structural, with replay authenticating the
record. Suggested regressions: one decode refusal per case (unrun).

## Lead dispositions

| Lead item | Disposition |
| --- | --- |
| **L1** | Confirmed by source (F2). P2: no demonstrable live change in current content. |
| **L2** | Confirmed, and extended with the contradictory "no rescue" copy and the pending-unavailable wording (F3). |
| **L3** | Confirmed (F1). I rate it P1 because it is the explicit pin and format boundary and costs one line to fix. |
| **Q1** | Single-session only. `TickAsync` returns before routing when the *local* session is not ready (`LocalWorldSession.cs:400`), and `IsReadyToFinalize` consults only readings (`WorldEncounterOutcomes.cs:49-57`). Once every required human has read, any ready writer completes, even if the other human has disconnected. A missing reading holds indefinitely. Details below. |
| **Q2** | Not proven (F3 regression). |
| **Q3** | No world completes two episodes. Untested: `Last` replacement, `FinalizedCount = 2`, the Completed view clearing at the next entry, and old records or readings not colliding. Counts are unbounded `ulong`; no cap was found. |
| **Q4** | Atomic clearing is implemented (`WorldCareProcessor.cs:140-145`) but proven only through the F4 interleaving. Untested: an exact retry of the old reading command after treatment returns its original receipt, leaves `Acknowledgments` empty and does not satisfy the new outcome. |

Q1 in detail:
- "Not-ready session holds" in RESULT means only that this session's own not-ready state suppresses all of its cutoffs.
- The second human is direct authority commits under the wolf's inherited host binding (`TwoHumanOutcomeWorld.cs:48-75`,
  `CargoOutcomeWorldAdmission.cs:41-49`). No second session or view is exercised, which is also why F5 is untested.

The no-survivor (coverage question) is acknowledged as unreachable and is a missing proof (RESULT gap 3). It does not count
as a pass.

## Missing required gates and proofs (separate from the defects above)
- **Required gates not run:** outcome-fixtures, outcome-native, outcome-auth, outcome-durability. None of these exist yet:
  - rendered win and loss runs;
  - post-draw visibility checks;
  - exact native commands;
  - whole-journal authentication;
  - process kills before and after publication at the reading and completion boundaries.
- **Fixture authentication obligation:** `OpenCargoOutcomesFixtureAsync` accepts any decoded fresh profile-35 genesis that
  passes `Find`. The future result tests must re-derive the declared calibration (seed, name, rows) and compare genesis
  bytes. Otherwise an edited fresh root could pass as a "declared calibration". This is the same pattern as the existing
  threat and retreat openers, so it is an obligation, not a regression.
- **Also untested:** reading commands with occupied identities (episode ID or a prior cause ID).
- **Out of scope:** lived profile-34 migration, network/device and multi-session readiness, balance and art.

## Verified as delivered (source and tests)
- Completion branch: `WorldEncounterLifecycleProcessor.cs:33-40`. The test asserts unchanged calendar, steps, ticks,
  positions, HP/MP, debt, lots and balances, plus the record.
- Reading control: `:107-117`.
- Release refusal: `WorldStepBatch` 35 guard, `LocalWorldSession.cs:300-301`, lifecycle view.
- Codecs: V35 root = V34 fields + owner. V34 and V35 cross-decode is refused. Action v20 dispatches only the new intent.
- Care: aftermath care requires Outcome (`WorldCareActions.cs:17`).
- No hostile re-entry against an incapacitated player (`EvaluateEntry` requires that participants can cooperate).
- Evidence: build, outcome-unit (16 passed), core, regression and retained-auth reports are all `accepted`, exit 0. Their
  recorded hashes for `LocalWorldSession.cs`, `WorldEncounterOutcomes.cs`, `LocalEncounterOutcomeView.cs` and
  `EncounterOutcomeJourneyTests.cs` match the frozen export.

## Inspected
- **Packet:** ASSIGNMENT, DISPATCH, SOURCE_INPUTS (changed-file arrays), DELIVERY.diff (whole), RESULT_INPUT.
- **Implementation assignment:** ASSIGNMENT, LEAD_SOURCE_FINDINGS, LEAD_DESIGN_REVIEW.
- **New Core:** owner, codec, intent, outcomes, admission, root/step/action codecs.
- **Changed processors and dispatch.**
- **Hosting:** `LocalWorldSession` (cutoff routing), `LocalEncounterOutcomeView`, `LocalCareView`,
  `LocalEncounterActionView`, `CargoOutcomeWorldAdmission`.
- **Client:** outcome text, overlay, entry, and the diffs to the offers, HUD, status and session.
- **Architecture:** `encounter-outcome.md`.
- **New tests:** all of them.
- **Evidence:** the frozen `report.json` and TRX test names.
- **Not read:** native or retained-native journals, the research archive, and baseline specs beyond the assignment.
