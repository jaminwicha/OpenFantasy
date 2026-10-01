# Independent road-threat delivery review

Status: **terminal**. Reviewer: Opus 5.5 (1M), as dispatched; no delegation, shell, tests or edits outside this file.
Scope: checkpoint `48fb19e176255e5a3c20fd9d92900814df7e5be4`, the files in `DELIVERY_FILES.json`, the reports in
`DELIVERY_CHECKS.json` and `delivery.diff`, read against `ASSIGNMENT.md`, `DESIGN.md` and `RESULT.md`. Paths are relative
to `Beta/Runtime/` unless stated otherwise. Nothing here was executed by me. Where a claim says "observed", I read it
in a retained report or artifact.

## Demonstrated defects (severity order)

### D1 — High (acceptance evidence): road-auth does not authenticate cancel-sends-nothing, retreat boundaries or final positions

- **Where:** `tests/OpenFantasy.Hosting.Tests/Client/RoadThreat/NativeRoadThreatResultTests.cs:89-117`. Compare the
  accepted precedent `tests/OpenFantasy.Hosting.Tests/Client/Retreat/NativeRetreatResultTests.cs:84-116,138`.
- **Expected:** assignment §5 says "Authenticate all native journals, including command absence on cancel and exact
  saved positions". The precedent does this from the journal. Between entry and the end of the retreat walk, it allows
  no player command except the retreat (`:84-92`). It requires each retreat sample to be an `EncounterResolution`
  commit, with the player due and the recorded retreat active, and a `TacticalAdvance` between samples (`:95-108`). It
  checks that the last sample is `record.WorldSequence` (`:116`). It also checks the final feet (`:138`).
- **Actual:**
  - The road test drops all of these checks.
  - Its sample counter (`:109`) increments for *any* player movement after the queue commit, capped at `RouteSamples`.
    On the keyboard route the later detour supplies those movements, so `Assert.Equal(record.RouteSamples, samples)`
    (`:117`) is close to tautological.
  - The class summary (`:19`, "the retreat walked its committed route") is not what the test checks.
  - No route's final player feet are asserted.
  - The probe's own cancel check (`client/scripts/RoadThreat/RoadThreatPlayProbe.cs:212`, `TimingRevision >= timing`,
    same as the precedent's probe) tolerates a Wait/Strike that was sent and already resolved.
  - `tools/check_native_road_threat.py:95-99` only checks the probe's self-reported strings and `CanExecute`, not that
    the inspect and commit timings are equal.
- **Consequence:** these regressions would still pass road-auth:
  - cancel sends a command;
  - retreat samples resolve at the wrong boundaries;
  - the saved final position differs from the reported one.

  The retained run itself looks correct. `keyboard.json` `Openings` shows timing 24/24 for inspect and commit, and the
  final feet (88704, 36224) are Kegga's approach (346,141). The gate does not prove it, though.
- **Minimal correction:** port the precedent's journal checks:
  - Between `entered` and the last retreat sample, the player's commands are exactly `[Strike]` on keyboard and `[]`
    on the other routes, then the recorded retreat.
  - Count samples only for `EncounterResolution` commits whose `state.Retreat.Active.Command.CommandId ==
    record.CommandId`.
  - Assert `samples[^1] == record.WorldSequence`.
  - Assert final feet equal the report's `FinalFeet` and the authored target: `FeetAt(346,318)` for controller and
    pointer, the destination approach for keyboard.

  This is a managed-only change. The retained native saves and manifest
  (`road-native-20261001T191501Z-3648e/native-road-threat-bja0fon4/native-manifest.json`) can be re-authenticated
  without a new native run.

### D2 — Medium: any in-flight player request suppresses the chief's entry, while that same cutoff still moves the player

- **Where:**
  - `src/OpenFantasy.Hosting/Simulation/LocalWorldSession.cs:413` evaluates hostile entry only when `pending is null`.
  - The same cutoff then builds the exploration step with the player's held movement (`:420-435`, movement added at
    `:428`).
  - A pending request refused at preview is dropped, but movement is still selected (`:640-642`).
  - In Core, a rejected or declarative action is not `performedWork`
    (`src/OpenFantasy.Core/Worlds/WorldStepProcessor.cs:321,341,394,407`). Movement is skipped only for actors in
    `worked` (`:663`).
- **Trigger (counterexample):**
  - The player walks north on x=346 holding Up, and keeps one non-work request in flight at each 50 ms cutoff
    (`StepInterval`, `:24`). The request can be any command the authority refuses, or a declaration such as a
    disclosure.
  - Chief feet are (88960, 79744) and lane feet x=88704, so the player is in reach for |dy| ≤ 307. That is about 19
    samples, or about 1 s of cutoffs.
  - `TickHostileEntry` never runs during that window. The player leaves reach on the far side with `EncounterCount`
    still 0, while the HUD says "Within the Bandit Chief's reach — it attacks now".
- **Expected:** the critic prompt asks whether input contention can suppress danger indefinitely. DESIGN/RESULT say the
  chief enters at the actual cutoff in reach.
- **Consequence:** danger can be suppressed indefinitely by contention. A human with the native UI cannot plausibly
  sustain 20 submissions per second; a scripted or in-process client can. One coincidental action at the reach boundary
  only delays entry by one sample.
- **Minimal correction:** pick one:
  - Select an eligible hostile entry before the player's pending exploration request. The request would then get the
    ordinary boundary refusal at the next cutoff, as `TickLifecycle` and `RefuseUnrepresentable` already do.
  - At least drop the player's movement from any batch at a cutoff where an eligible entry was deferred.
- **Test:** player in reach, held movement, one refused action per cutoff. Expect entry at the first in-reach cutoff
  and no further player sample.
- **Related doc fix:** the new `docs/architecture/encounter-lifecycle.md` paragraph omits the "no pending player request"
  precondition.

### D3 — Medium: the warning has no observation rule; copy, published data and artifacts disagree

- **Where:**
  - `src/OpenFantasy.Hosting/Simulation/RoadThreat/LocalRoadThreatView.cs:38` projects every hostile scenario with no
    distance, regional-observation (`LocalRegionObserver.ObservationRadiusCells = 24`) or floor predicate. Its
    distance also ignores Z.
  - `:67` classifies every unreachable position as `Watching`.
  - `client/scripts/RoadThreat/RoadThreatWarning.cs:14` selects `Watching`.
  - `client/scripts/FarmEncounterStatus.cs:41-44` prints the warning in every exploration frame.
- **Trigger:** any exploration frame of the threatened world.
  - From genesis at the farm, and at Marla's approach (lane x=97, y=349 per `docs/architecture/cargo-circuit.md:101`,
    about 250 cells from the chief), the HUD reads "Danger ahead · Bandit Chief guards the road". The managed journey
    itself asserts `Watching` there (`tests/.../Simulation/RoadThreat/RoadThreatJourneyTests.cs:39`).
  - The same line still says "ahead" at Kegga, after the chief has been passed.
  - The tooltip publishes the chief's current HP, level and exact distance at any range.
  - `Recovering` (`LocalRoadThreatView.cs:68`) publishes whether the NPC carries readiness debt. Previously only the
    player's own debt was public (`src/OpenFantasy.Hosting/Simulation/LocalEncounterLifecycleView.cs:33`).
- **Expected:**
  - `README.md:757`: the warning shows "while the chief is in view".
  - Assignment §4: the warning must not leak unobserved facts.
- **Observed artifact:** `road-native-…3648e/native-road-threat-bja0fon4/keyboard.warning.png` shows the warning while
  the chief, 7.1 cells away, is off-screen. In `keyboard.engaged.png`, at 385 subunits, the chief first appears at the
  top edge with its head under the HUD bar. No retained frame shows the chief before contact.
- **Notes:**
  - Positions are already public in this "sight-complete" view (`LocalWorldView.cs:41-44,88-96`), so distance itself
    is not new.
  - HP, level and debt state in exploration are new.
  - `LocalRoadThreatView.cs:11` documents `Watching` as "Visible, ready", but the code checks neither visibility nor
    readiness.
- **Minimal correction:**
  - Gate the projection with an explicit observation rule, e.g. same floor and within the regional observation radius
    or sight query.
  - Restrict `Recovering` (debt-derived) to observed contact, or declare it a public contract.
  - Correct the README copy.
  - Add a managed test at the farm, Marla and Kegga. The assignment permits a compile link for the new engine-free
    `RoadThreatWarning.cs`; none was used (`newCompileLinks: 0`).
  - If the project instead rules that this profile publishes everything, the minimum is to fix the README and
    "ahead" wording and document HP/level/debt as public-view facts.

## Plausible risks needing a test or a recorded decision

- **R1 — Attrition win locks the save.** The chief keeps damage across episodes with no regeneration (native run ended
  at 788/800). Only its living initiator can release an Outcome
  (`src/OpenFantasy.Core/Worlds/WorldEncounterLifecycleProcessor.cs:92-93,125-126`). Repeated strike → retreat → heal
  cycles that ever bring the chief to 0 HP would therefore leave a permanent Outcome that nobody can release. A loss is
  documented as terminal; a win is not. Please either document both as terminal or test the path.
- **R2 — Passing during the chief's own recovery.** `CanInitiate` requires the chief to have zero debt. After a 10-step
  retreat the player can be back in reach in about 1.7 s (1.3 s charged + ≥ 8 samples). If the chief's carried ledger
  debt is longer, strike → retreat → walk-through passes it lawfully, and the warning even advertises it ("catching its
  breath within reach"). That is legitimate readiness, but it is an undocumented cheap bypass. Please measure
  `GetDebt(chief)` at disengagement and test a prompt return.
- **R3 — Threat radius and bypass cost.** Danger executes only at attack reach (400 = 1.56 cells), not at aggro range
  140 (1120 subunits, 4.4 cells), and the stance is one cell off the lane. Stepping to x=345 (dx = 512) avoids the
  chief for about 16 extra samples (about 0.8 s). README/RESULT describe a three-cell detour.
  - This is a declared adaptation, but its effect on the challenge (once warned, a one-cell sidestep avoids the chief)
    is not stated.
  - Lead decision against "preserve challenge".
- **R4 — Reopen boundaries.** The managed reopen happens only after the sale, in exploration
  (`RoadThreatJourneyTests`). The native reopen happens only at the end of the keyboard route. Untested boundaries:
  - the chief-initiated Confrontation, Staging and Active phases (player due);
  - mid-retreat;
  - exploration with retained debts;
  - the incapacity Outcome.

  A stateless controller makes these likely fine, but they are unproven.
- **R5 — Untested holds and production entry.**
  - Reading-hold and care holds at the camp have no session test (declared in RESULT).
  - The production `--cargo-threat [SAVE]` create/reopen/refuse path (`FarmClientSession.OpenCargoThreatAsync`) is not
    exercised by any managed or native check, even though `FarmClientSession.cs` is compile-linked. This matches the
    retreat precedent.
- **R6 — Retries and kills.**
  - There is no conflicting-retry case (same id, different bytes) and no durable-refusal case for a host entry.
  - The durability mode is the generic six-kill check, not a kill at the host-entry boundary. Risk is low because no
    new owner or format was added.

## Already declared out of scope

These are declared in RESULT and `road-threat.md`, and they stay open:

- pursuit, aggro, leash or return, approach movement, territory memory;
- rewards, gil, drops, respawn;
- NPC release, rescue or robbery after incapacity (a loss leaves a terminal Outcome; see R1 for a win);
- escort wages, traveling merchants, party retreat, multiplayer, lived-save migration, procedural frontier;
- new art (the chief reuses the Squire sprite);
- physical devices and OS focus (`WindowFocusObserved: false`; focus is injected);
- human acceptance and the complete P02 exit.

All routes walk with held keys. The pointer route opens the wheel with **V** and pauses with **Esc**; the pointer only
selects the sector and clicks close/confirm. This matches the accepted `RetreatPlayProbe.cs:144-146,267-268,399-400`,
so it is a limitation, not a regression.

## Verified without finding

- **Source record.** The `monsters.json` record has exactly the bound fields: id, name, level, six stats, moveSpeed,
  xpReward, gilMin/Max, aggro, leash, attack{power, range, atbCost} and three drops. Pointers, digests and payload hashes
  are bound; executed and retained-only fields are honestly split.
  - Range ×8 with no grace and readiness `atbCost/300` match the accepted Bite
    (`src/OpenFantasy.Hosting/Content/EncounterFarmAdmission.cs:50-52`).
  - The attack's default `requiredFunction = Manipulation` ties it to the transformed humanoid body.
- **Chief admission and older worlds.**
  - One chief has distinct registered identities in every coupled owner.
  - Wolf scenarios gain only a side-0 bystander.
  - A played root is refused.
  - Older worlds keep their genesis and public-view bytes (`RoadThreats` null and `JsonIgnore`). Retained-auth
    (5 recordings) and fixtures (5/5) passed on the final build.
  - The `CargoWaterRebinding` extraction is the same logic.
- **Host entry and holds.**
  - Entry uses the chief's own principal, session and revisions, and the authority re-validates it.
  - Pause, reading hold, care, excavation and encounter phases select nothing.
  - Movement requires `CanAct` (`WorldStepProcessor.cs:664`), so post-retreat player debt cannot be walked through.
- **Native keyboard evidence (observed).**
  - Entry came at the first in-reach sample (385 ≤ 400).
  - HP went 78→42 from the real hit and stayed 42 through the final report. The reopen script compares `FinalHp`
    between the reopen and keyboard reports.
  - Disengagement happened at 659.7 > 656, after 10 samples.
  - No re-entry occurred over 20 steps.
  - The chief stayed at (347,311) with 788 HP.
  - The sale paid exactly +38.
  - Process times: keyboard 94 s, controller 19 s, pointer 21 s, reopen 12 s, aggregate 148 s. The tool's LIMITS
    equal the assignment's 30/720/750/300/330/90/900.
- **Reopen and binding.**
  - Reopen recovers the complete journal (`src/OpenFantasy.Hosting/Worlds/SqliteWorldAuthority.cs:85-107`).
  - All fourteen final reports bind `build-20261001T190858Z-945f2`; unit observed 6/6 passed; earlier failures are
    retained.
  - Delivered `LocalWorldSession.cs` and `LocalWorldView.cs` hashes equal the evaluated source hashes.

Minor code and doc items:

- The nested `LocalRoadThreatViewLine` in `FarmEncounterStatus.cs:10` reads like a Hosting type.
- `road-threat.md:83` claims a name label that neither retained frame shows (low confidence: it may be hover-only).

## Scoped acceptance recommendation

**Accept now, scoped to:**

- the source binding and mapping;
- the fresh composition and older-world preservation;
- the reuse seams (`CargoWorldAdmission` hook, `CargoWaterRebinding`);
- the chief-controlled entry mechanism (subject to D2);
- the managed durable journey: buy → road entry → real damage → retreat → detour → exact sale → complete-journal
  reopen → re-engagement;
- the rendered routes, as synthetic-input evidence only.

**Do not accept the road-threat qualification as complete** until:

1. D1 is fixed and road-auth re-run on the retained manifest.
2. D2 is fixed and tested, or explicitly accepted as a recorded limit with a bounding test. A session change needs a
   rebuild and a re-run of the affected modes.
3. D3 has an observation rule, or the README/"ahead" copy is corrected and the disclosed facts are declared.

R1–R4 should become tests or recorded decisions before the threat is relied on for wider P02 claims.

## Evidence gaps and limitations

- **Missing evidence:**
  - no contention test;
  - no managed warning test;
  - no frame showing the chief before contact;
  - no mid-encounter or Outcome reopen;
  - no reading-hold test;
  - no production-entry test;
  - no physical device, OS focus or human art review.
- **Limitations of this review:**
  - I used Read/Glob/Grep only and executed nothing. I did not open SQLite saves.
  - I viewed two of the rendered frames, not the controller or pointer sets.
  - I relied on scalar report fields and the unit test list.
  - I did not re-read the unchanged `LocalEncounterController` or `LocalNpcController`. The claim that the chief only
    attacks or waits rests on tests and native evidence.
  - The review was done within the 1,800-second window.
- **Counterexamples where execution would settle it:**
  - D2: the contention test described above.
  - D3: at the farm or Marla, expect no warning under an observation rule. Today it shows `Watching` and "Danger ahead".
  - R2: the chief's and player's debts at disengagement.
