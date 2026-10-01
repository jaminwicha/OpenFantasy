# Retreat complete-admission and archive proofs — RESULT

Status: **partial.** Deliverables 1 and 3 delivered and passing on final source. **Deliverable 2 (archive cut with an
active retreat across it) not delivered**; see Gaps. Model `claude-opus-5-5[1m]`, one request, no agents, no Git, no
production/existing-test/wrapper edits. Owned writes: this file and two new test files (paths below).

| Check (final source) | Evidence (`../combat-retreat-repair/qualification/`) | Result |
| --- | --- | --- |
| build | `build-20261001T170217Z-98c6a` | accepted (preservation 571/23 intact) |
| unit | `unit-20261001T170231Z-07f43` (`unit.trx`) | **20 passed** (all original Retreat* plus 2 new) |

Retained failures: `unit-20261001T170011Z-2c186`: my first deliverable-3 cause assertion was wrong; see 3. `build-20261001T170151Z-32200`:
the known transient Godot `EditorSettings ... android_sdk_path` import error (both dotnet builds passed); the same command passed on rerun.
Earlier passing runs: `build-20261001T165613Z-e59d7` and `unit-20261001T165633Z-cec1d` (19 passed, deliverable 1 only).

## 1. Complete-world admission contrast — delivered

`Beta/Runtime/tests/OpenFantasy.Hosting.Tests/Simulation/RetreatCompletion/RetreatCompleteAdmissionTests.cs`
(`A_sample_movement_supports_but_the_dry_envelope_refuses_is_never_queued_offered_or_walked`, 3.9–5 s).

- **Setup:** source-backed fresh `CargoRetreatWorldAdmission.CreateEmbedded` profile 34, a real session (`RetreatJourneyWorld`),
  real first-wolf entry and the player's first ready boundary with an executable offer. Then a **MARKED COUNTERFACTUAL**
  root built with the public `WorldSnapshot` constructor: only the player's and the real wolf's feet change (revision +1).
  The player is at `BreachFarmLayout.MiningStance` and the wolf one cell north. The real reservoir, liquid, seal,
  excavation, inventory, production and coordination owners are untouched, and full admission runs. Nothing is persisted.
- **Trigger (real terrain):** the breach divider `(116,329,0)` is solid, so `PrepareMove` supports a body stepping south onto it,
  but its spill-plane cell `(116,329,1)` is in the dry envelope. Every southward sample is `Prepared` (`WalkSample`). The first
  refused prefix has the earlier prefix admitted, and the constructor's refusal message is `...wet envelope...`
  (`WorldTerrainLiquidBinding`), which pins the cause.
- **Queue:** `EvaluateQueue` and the processor's `EncounterDecision` both refuse `Unavailable`. Feet, actor revision,
  `RemainingDelay`, decisions, retreat owner bytes, HP and phase are unchanged.
- **Advisor:** `Advise` finds a longer legal route along the bank (`Route.Count > south.Count`, although the advisor prefers fewest
  samples and the south route ends clear of contact). Every prefix is admitted, the end is clear, the host `LocalWorldView` offer
  equals it, and the processor accepts it (`AcceptedCount` 1).
- **After commitment (Unadmitted):** the same straight southward route is committed through the processor where it is legal
  (the same geometry whole cells north, chosen by `EvaluateQueue == None`). One real sample is walked through ordinary
  boundaries (`UntilDue`). A **MARKED COUNTERFACTUAL** rigid translation then carries the committed walk (same command id,
  route, samples and readiness) with the player and wolf, so the next sample lands on the placement refused above.
  This is equivalent to the envelope moving under the route; static geometry makes that impossible in real content.
  `WalkSample` is still `Prepared`. The resolver records `Interrupted/Unadmitted` with no movement (`Final` equal to the
  last admitted feet), exactly one paid sample (`RemainingDelay == SampleDelay`, `ReadinessCharged + delay`), the encounter
  still `Active`, HP unchanged and the observer view showing the reason. The untranslated walk takes its next sample normally.

## 3. Malformed V34 bytes: queued choice left after the first sample — delivered

`.../Simulation/RetreatCompletion/RetreatQueuedChoiceBytesTests.cs`. This uses a real queued root from ordinary play, with the
pinned Action choice verified. Only the trailing retreat field is spliced, at equal length: a continuation with one walked sample,
origin one sample behind (feet equal `ExpectedFeet`) and one sample's readiness. That field is well formed on its own.
`WorldSnapshotCodec.Decode` and `WorldSnapshotCodecV34.Decode` both throw `FormatException`.
The typed constructor's first refusal is `WorldEncounterActionAdmission`: "Each pending action requires exactly one complete
retained request." A started continuation is no longer `IsQueuedRetreatChoice`, so the leftover choice reads as an ability request.
My first assertion expected the retreat owner's own message instead. That was my wrong assumption about which rule runs first,
not a production defect, so I corrected it to the actual exact message. I did not add ally/third-side constructor
refusals: the existing `RetreatContrastTests` already shows those refusals.

## 2. Coordinated material/care archive cut across an active retreat — NOT delivered

Source analysis (`WorldStepProcessor.DecideSelected`, `WorldMaterialCareContinuation.RequiresCut`,
`WorldResourceContinuation.RequiredCapacity`):

- During an active encounter only `EncounterDecision/EncounterResolution/TacticalAdvance` batches exist, with no issues, offers,
  uses, crafts or treatments. In those batches the quote, proposal, transfer, production and care windows require retained+open ≥
  limit. Those owners always cut earlier, at the exploration batch that would overflow, so **they cannot trigger mid-encounter**.
- The only mid-encounter trigger is the resource-history window at an `EncounterResolution` batch, which reserves (1,2).
  That needs `History.Batches ≥ 255` or `ChangeCount ≥ 1021` when a resolution runs after the retreat is committed.
  A retreat sample's own resolution counts, so the closing root would hold the continuation.
- A fresh profile-34 cargo-retreat world only reaches that through about 255 HP-changing events (hits, potion uses, rewards).
  Its fixed composition (unplayed `RestorativeJourneyAdmission` basis, fixed stock and stats) has no such path. No
  stress genesis exists for profile 34 (the existing ones, `materialCareStress` and `enduranceProfile`, are profile 26/27).
  Building one needs a test-only composition through the internal cargo extension. I did not attempt it within the limit;
  it is not shown to be impossible.
- **Unqualified:** a normal coordinated cut (`WithCoordinatedCut`) with a committed retreat in the closing root; stored segment
  contents and publication; continuation ownership; full-recovery equality; exact retry and occupied identities across that cut.
  The existing 6,000-step checkpoint proof does not cover this.

## Other gaps and notes

- No production defect found. Native, physical, art, MP, party and lived-save migration are not claimed. I did not touch Astra's
  `legacy-auth`/rendered-wheel obligations. These new tests are managed-only and use counterfactual roots for the boundary
  contrasts only.
- An ally or third side mid-route stays proven only by the pure rule plus existing constructor refusals (unchanged).
