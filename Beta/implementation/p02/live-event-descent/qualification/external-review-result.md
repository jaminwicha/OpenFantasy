## Event descent publication review

I found no substantiated defect in the inspected scope. One hypothesis and several uninspected boundaries are listed separately below.

**Files inspected:**
- `Beta/AGENTS.md` and the work-in-progress paragraph of the handoff
- `PendingExcavation.cs`
- `PendingExcavationCodecV3.cs`
- `WorldConsequenceProcessor.cs`
- `ExcavationPlanner.cs`
- `WorldJournalPolicy.cs`
- Supporting files, relevant parts only:
  - `ConstructionSettlementResolution.cs` (lines 15–129)
  - `ConstructionSettlementResolutionCodecV3.cs`
  - `ConstructionDescentAssessment.cs` (lines 18–122)
  - `ConstructionDescentAssessmentCodecV1.cs`
  - `VerticalMotionResolution.cs` (lines 20–94)
  - `WorldStepProcessor.cs` (lines 36–199)
  - `WorldCommitPreparation.cs` (lines 55–134)
  - `SqliteWorldAuthority.cs` (lines 115–184)

**Checks that held:**
- **Authority to dig and the single source.**
  - `TryBegin` validates the request before any geometry is computed.
  - `ValidateForWorld` repeats that validation against the reconstructed job-free basis.
  - `PlanCore` runs it a third time at terminal publication.
  - The source identity is `DeriveSpawned(commandId, 0)`, reserved once and issued only on the successful terminal plan.
- **Reading hold.** `PlanResolved` hardcodes `held=false`. That is sound because `HasReadingHold` is exactly `Dialogues.Count != 0`, and pending worlds reject holds.
- **Command and time across continuations.**
  - `AccountForPendingCommand` removes the command's outcome at acceptance and re-adds it only at terminal completion.
  - `Continue` requires the terminal receipt to be one of the reserved result identities and to carry the original command hash.
  - The reserved sequence offsets 2 to limit+1 match `state.Revision+1` for continuation counts 0 to limit−1.
- **Stale basis or cursor.**
  - Decoding rebuilds `BeginEventOrdered` from `stable.WithPending(null, revision)` and checks the basis hash and the input pin.
  - It replays the completed work prefix and requires the re-encoded bytes to match exactly.
  - The completed work must equal `ContinuationCount × WorkUnitsPerContinuation`.
- **Work bound.**
  - The event path takes its bound from the actual scene (`VerticalMotionScene.MaximumTotalWorkUnits`), not profile17's linear bound.
  - That bound is fixed for the scene and does not change as work advances.
  - Reservations and journal capacity both derive from it.
- **Journal capacity.** `RequireCompletionCapacity` reserves the final commit sequence, one command outcome, the continuation frames and the larger terminal outcome at acceptance. Preview, commit and recovery all pass the newly pending work into that check.

**Hypothesis, not substantiated:** `VerticalMotionResolution.cs:57–60` throws `InvalidOperationException` when the work bound is reached without completing, instead of failing the resolution. If any scene reached that bound, `Continue` would fault on every attempt. The world would stay pending with its command and reserved interval never resolved, and no `Unavailable` receipt would be issued. Whether a scene can reach the bound depends on the geometric-kernel bound proof, which was outside this review.

**Uninspected boundaries:**
- **Continuation frame width.** I could not confirm that `WorldStepCodec` encodes continuation frames at a fixed width; my single grep found no matches. If the revision or step fields are variable-width, the bytes reserved for future continuations could be under-counted.
- **`WorldSnapshotCodec` profile18 root and mode2 handling.**
- **`WorldJournalRecovery` replay.**
- **`LocalWorldSession` scheduling of continuations.**
- **Identity-index handling of reservations at `WorldSnapshot.cs:248`.**
- **Reservation cost per step.** Reservations are recomputed on every `Advance`. How expensive that is for large event scenes with a small `WorkUnitsPerContinuation` was not measured.
