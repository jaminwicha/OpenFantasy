# Independent combat-retreat review (profile 34)

Reviewer: claude-opus-5-5[1m], Max, one request. I only read source; I ran nothing. Paths are relative to `Beta/Runtime` unless they start with `Beta/`.

## Verdict

**Not acceptable as complete yet.** The core shape holds together:
- a typed v19 intent with its own owner and its own V34 root and step codecs;
- each sample walked through `PrepareMove` and published under complete-world admission;
- readiness charged on every sample, including refused ones;
- one transaction that copies actual debts, keeps exposure and carry, and fabricates nothing;
- old-profile behaviour gated on `world.Retreat` and refused at the batch, session and codec layers.

Three problems are in the law itself: P1, P2 and P3. All three are cheap to fix now, because no lived profile-34 save exists and `RulesText`/`Pin` can still change. They are hard to fix later. A fourth problem (P4) is in the native holding UI.

None of the five omissions the worker declared are waived or passed. They are listed below with their root causes.

## Severity-ranked defects (confirmed in source unless marked as a risk)

### P1 — High: queueing and the advisor skip the complete-world admission that each sample must pass
- **Where:**
  - `WorldEncounterRetreats.EvaluateQueue` (`src/OpenFantasy.Core/Worlds/WorldEncounterRetreats.cs:115-122`) checks the route only with `WalkRoute`, which is spatial `PrepareMove`.
  - The advisor checks only `QueryGroundSegment`, `WalkRoute` and the contact test (`WorldEncounterRetreatAdvice.cs:85,96-97`).
  - The resolver applies the whole-root rules per sample through `world.WithState` (`WorldEncounterRetreatResolver.cs:39-51`). Those rules include the reservoir dry-envelope check on actor bodies (`WorldTerrainLiquidBinding.cs:23,39,42-48`), seals and custody.
  - Ordinary movement applies the same root rules through `FitsCompleteCandidate` (`WorldStepProcessor.cs:185-235,661-687`).
- **Trigger:** a route whose sample k is clear for `PrepareMove` but enters a dry envelope, or fails another whole-root rule.
  - Queueing accepts it, and the native wheel offers it.
  - Sample k is then refused as `Unadmitted`, with readiness charged and no movement.
  - The advisor offers the same route again from the new position, so a player or an opted-in NPC can loop.
  - If k=0, a retreat becomes a turn-pass that costs one sample (13 units at speed 100) instead of Wait's 100.
- **Why it matters:** this misses assignment item 2: "revalidate … hazards … at commit; retain coupled whole-world admission".
- **Fix:** write one `TryAdmitSample(world, space, timing)` helper, extracted from `Publish`. Use it:
  - for every prefix in `EvaluateQueue`;
  - for the advisor's verification step;
  - in the resolver.

  Interruptions then come only from real changes after commitment.
- **Proof needed:** a counterfactual root with an envelope on the route. Queueing should return `Unavailable`, the advisor should skip that route, and there should be one resolver `Unadmitted` case.

### P2 — Medium-High: the authority's "clear of contact" also accepts blocked sight, missing cells and a different floor
- **Where:**
  - `IsClearOfContact` is `QueryInteraction(...) != Reachable` (`WorldEncounterRetreats.cs:55-61`).
  - `QueryInteraction` returns `DifferentFloor` (`LocalSpatialSnapshot.cs:146-149`), and `Occluded` or `Unavailable` (missing cell) (`Spatial/TerrainSightline.cs:50-58`).
- **Trigger:** a one-sample route that puts a solid terrain cell or a structure box on the line between centres. It ends the episode while the actor is still within one cell of the wolf.
- **Two contradictions:**
  - The pinned rationale says the margin works "so no one step by either party restores contact" (`EncounterRetreatRules.cs:6-11`). That holds only for range, not sightline.
  - The advisor uses plain open distance (`WorldEncounterRetreatAdvice.cs:91-92,108-118`). So the native UI never offers this route, but a crafted command, or any other client, is admitted.
- **Related:** `min(768, range+256)` leaves less than one cell of margin for ranges above 512 (`EncounterRetreatRules.cs:45-46`). The documented guarantee is false there.
- **Fix:** define "clear" as centre distance strictly greater than the radius (`QueryCenters` gives `OutOfRange`), regardless of sightline or floor, matching the advisor. Change `RulesText` now. Document the long-range margin, or make the cap and the margin consistent.

### P3 — Medium: the "two positive sides" scope is claimed but not enforced
- **Where:**
  - `EvaluateQueue` refuses only when there are no opponents, or when the actor is not the sole living side member (`WorldEncounterRetreats.cs:96-102`).
  - `CreateFresh` accepts the scenarios of any market genesis (`WorldEncounterRetreatAdmission.cs:16-44`).
  - `Disengage` ends the episode for every side and cancels all other choices (`WorldEncounterRetreatResolver.cs:95-110`).
- **Trigger:** with three or more living positive sides, the sole member of side A retreats. The B-versus-C fight ends with no outcome — a free release for B and C — contradicting the "explicit refusal boundaries" in `DESIGN.md` and `encounter-retreat.md`.
- **Related risk:** "sole *living* member" (`WorldEncounterRetreats.cs:34-40`) lets a survivor leave downed allies behind. Those allies never pass through Outcome, so a downed player ally's care aftermath is bypassed. Current scenarios have no allies.
- **Fix:** refuse unless exactly two positive sides have living members. Decide and pin the rule for downed allies: refuse, or document it. Add tests for both.

### P4 — Medium: during a committed retreat the native UI says the world is waiting for a choice and offers Wait/Strike, which the authority then refuses
- **Where:**
  - At each due boundary after the retreat has started, `LocalWorldView.Encounter` has `PlayerIsDue=true` and `PlayerDecisionQueued=false` (`src/OpenFantasy.Hosting/Simulation/LocalWorldView.cs:74-76`).
  - So `FarmEncounterStatus` shows "Choose your action · World waiting" (`client/scripts/FarmEncounterStatus.cs:25-27`), although `HasRequiredDecisions` is false.
  - `LocalEncounterActionView.BoundaryBlock` does not know about the retreat hold (`LocalEncounterActionView.cs:86-99`), so `CanWait` is true and an in-reach Strike is queueable.
  - The processor refuses the choice as `Unavailable` (`WorldEncounterActionProcessor.cs:134-136`). That journals a refused receipt and delays the sample by one cutoff.
- **Known to the authors:** `RetreatSessionTests.cs:151` adds a guard so the test never submits Wait while the player's own retreat is active.
- **No outcome cue:** an `Interrupted` or `Cancelled` record is never shown to the player. Only the probe reads `View.Retreat.Last`. A blocked retreat silently puts the player back at a choice.
- **Fix:** use `WorldEncounterRetreats.HoldsActor` in `BoundaryBlock` and `PlayerDecisionQueued`, or add a new `Retreating` reason. Show "Retreating · step x/y". Show a HUD message when a new terminal `Last` for the observer appears.

### P5 — Medium-Low: the public view shows other actors' committed retreat plans and command ids
- **Where:** `LocalRetreatView` projects `Progress` (destination, route length, readiness) and `Last` (including `CommandId`) for *any* actor (`LocalRetreatView.cs:56-65`).
- **Conflicts with:**
  - `LocalEncounterActionView.cs:29-33`: "No other actor's … pending command … projected".
  - `LocalWorldView.cs:41-44`: commit counters and hashes are excluded.
- **Impact:** harmless with one local player; a leak once there are several players.
- **Fix:** project only the observer's own progress and record, or a reduced public form ("an opponent is withdrawing") with no destination or ids.

### P6 — Low: the shown cost and time are not what the player gets
- **Shown:** the offer displays `TotalDelay = SampleDelay × samples` as "N steps · X s recovery" (`LocalRetreatView.cs:38`; `RetreatOffers.cs:36,53-54`).
- **Actually:**
  - Ticks are 5 units (`EncounterTimingSnapshot.cs:14,130-133`), so a 13-unit sample takes 15 units of tactical time. Sixteen samples take 2.40 s, not the 2.08 s shown.
  - Per cell that is 120 units against Strike's 100. This contradicts "one standard action per full cell" (`EncounterRetreatRules.cs:23`; docs).
  - The unit test with that name only checks a lower bound (`EncounterRetreatContractTests.cs:25-35`).
  - After disengaging, only one sample of debt remains (`RetreatSessionTests.cs:100`). "Recovery" therefore does not mean what it means on the Strike and Wait offers.
- **Also:** a blocked offer labels its target "No safe ground" even when the blocker is Recovering, Queued and so on (`RetreatOffers.cs:35`).
- **Fix:** show the tick-quantised walking time separately from the remaining recovery. Show the destination label only for ground blockers.

### P7 — Low: `LocalWorldView.Retreat` changes every older view's JSON and hash
- **Where:** `"Retreat":null` now appears in every non-34 view (`LocalWorldView.cs:171`). Public hashes are SHA-256 over default JSON (`client/scripts/MarketProbeEvidence.cs:18-19`; `tests/.../Client/Cargo/NativeCargoResultTests.cs:89,146,313-314`).
- **Effect:** the cargo, trading and gift manifests accepted earlier can no longer be re-checked with this binary. The delivery replaced them with fresh runs.
- **Context:** this follows the same pattern as earlier additive fields such as `Markets`, so it is not a new kind of regression. It does conflict with "historical proofs not replaced".
- **Fix:** add `[JsonIgnore(Condition = WhenWritingNull)]`, or record explicitly that `PublicHash` is only valid within one build.

### P8 — Low: the docs and the code disagree in places
- `encounter-timing.md:20` (`TryConfirmNext`: the first due actor must have a choice) and `encounter-actions.md:50` (every Action decision pairs with a pending request) were not updated. Neither mentions the standing-decision exception (`WorldEncounterActions.cs:20-26`; `WorldEncounterActionAdmission.cs:51-57`; processor lines 65-69).
- `encounter-retreat.md` says the advisor verifies "with the authoritative walk". It does not apply complete admission (see P1).
- The phrase "no committed retreat in the episode" really means "no retreat in flight anywhere"; a retreat may be queued again after an interruption.
- In the resolver, `catch (ArgumentException)` (`WorldEncounterRetreatResolver.cs:47-51`) also turns `Disengage` and record-invariant failures into a gameplay `Unadmitted`, which can then be retried endlessly. Narrow the catch to the `WithState` admission call.

## Required missing proofs
The five declared omissions are still open:
1. **No real process interruption.** Recovery is shown only after a graceful `StopAsync` (`RetreatSessionTests.cs:260-264`).
2. **No checkpoint or archive test.** This path is reachable mid-route:
   - `RequiresCut` has no batch-kind gate (`WorldMaterialCareContinuation.cs:65-99`), so a cut can land on an encounter resolution between samples.
   - `WithCoordinatedCut` carries `Active` across that cut.
3. **No native trade after retreat.** The probe only travels (`RetreatPlayProbe.cs:166-170`).
4. **No tests for `NoGroundLocomotion`, `AllyRemains`, `ContactRemains` or `ControlLost`.**
   - All four are unreachable with current content.
   - Reuse the counterfactual-root pattern from `RetreatSessionTests.cs:291-370`.
5. **First wheel opening captures a stale offer.**
   - Root cause: `FarmGame.cs:567` opens the wheel with the previous frame's `_presented`. The `Present` at line 571 then refreshes, and `FarmQuickWheelModel.cs:124-137` marks the slot as changed.
   - Strike and Wait have the same problem.
   - Fix: read the view once, call `Present(view)` with it, and only then `_wheel.Open(_presented)`.

Additional proofs that are missing:
- **Cancelled choices at disengagement.** No world-flow test shows another actor's queued choice or pending ability being cancelled with its id kept in `CancelledDecisionIds`. That is only round-tripped through the codec (`EncounterRetreatContractTests.cs:93-101`).
- **V34 root codec.** No test touches `WorldSnapshotCodecV34`. Needed:
  - V33↔V34 roots refused when decoded as the other version;
  - a malformed retreat field;
  - `Validate` violations surfacing as `FormatException`: feet off the walked path, a decision still present after the start, a mismatched pin.
- **Second command while a retreat holds.** The comment at `RetreatSessionTests.cs:272` claims a new command is refused; nothing asserts it. A conflicting duplicate (same `CommandId`, different route) is also untested.
- **Native cancel and per-sample journal checks.**
  - The probe's cancel check (`RetreatPlayProbe.cs:128-129`) would also pass if a submission had been sent and rejected.
  - The result test checks only the last sample (`NativeRetreatResultTests.cs:68-79`).
  - Needed: no player command rows between entry and the single retreat command; every sample exactly ±32 at the player's own boundary, with ticks in between.
- **Bystanders and opponents keep their sides, feet and ids through disengagement.** This is claimed in a comment (`RetreatSessionTests.cs:167`) and never asserted.
- **Tests for the fixes:** the P1 refusal at queue time, the P2 occluded destination, and P3 (three sides, downed ally).

## What holds, and the stated scope limits
- **Older profiles behave as before.** Every changed shared path stays inert when `Retreat` is null: `HasRequiredDecisions`, action admission, kind and pending selection in the processor, and the `WithState` default. Retreat requests are refused by the batch builder, the step codec, the session and the dispatcher (`RetreatProfileBoundaryTests`).
- **The validator ties the pieces together.** The queued Action decision is pinned to the command hash, there is no decision after the first sample, and the actor's feet must equal the end of the walked path. Recovery cannot skip a position. Exact retry returns the original receipt.
- **Exit uses existing paths.** Debts copy to the ledger, pacing ends through the same `EndEncounter` as release, nobody's HP, position or actor is fabricated or lost, and every sample is exactly 32 subunits.
- **NPCs follow the same rule.** They use the same intent and admission, and an NPC already holding a retreat is skipped by its controller.
- **Native entry is explicit.** Cross-opening another save is refused, closing the wheel sends nothing (by code), and a changed capture is never retargeted.
- **Stated limits:** no pursuit; one retreat in flight across the whole world, so an opponent's retreat blocks the player's own; terrain-only ground; no party retreat or lived-save migration.

## Consistency opportunities (from actual code paths)
1. **One sample-admission function.** Today queueing (`PrepareMove` only), the advisor (ground segment plus walk) and the resolver (walk plus `WithState`) apply three different strengths of check. One function would close P1.
2. **One "held actor" check.** Use it in `HasRequiredDecisions` (already does), `LocalEncounterActionView`, `LocalWorldView.Encounter` and the NPC controller. This closes P4.
3. **One contact rule.** The advisor's `Margin` and `IsClearOfContact` should be the same function. This closes P2.
4. **One visibility rule.** Retreat projections should follow the existing rule that other actors' pending commands stay private. This closes P5.
