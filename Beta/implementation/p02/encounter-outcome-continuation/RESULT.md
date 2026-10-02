# Encounter completion and deliberate continuation — result

Status: **partial, honest**. The versioned Core/Host rule (profile 35) is implemented and qualified by managed,
committed, reopened sequences and by real process-kill durability at the new reading/completion boundaries; the native
production controls are bound (profile-gated) and build/import cleanly; the existing suites still pass. **Not
delivered:** the rendered native outcome journeys (probe, `check_native_encounter_outcome.py`,
`NativeEncounterOutcomeFixtureTests`/`NativeEncounterOutcomeResultTests`). The `outcome-fixtures`, `outcome-native` and
`outcome-auth` gates were therefore **not run** and are not waived. No balance, production, art/UI-acceptance,
migration, network or credit-saving claim is made.

Design (written first): [DESIGN.md](DESIGN.md). Architecture: `Beta/Runtime/docs/architecture/encounter-outcome.md`.

## Chosen law (profile 35, `encounter.outcome.v1`)

* **Resolved** (unchanged lifecycle): Active resolution leaving ≤1 living positive side → Outcome, from actual HP.
* **Read** (new controller fact): `AcknowledgeEncounterOutcomeIntent(episode, finalArchiveHash)`, lifecycle kind 8.
  Requires Outcome, exact episode + archive hash, the actor's *own* control binding and presence revision, and that the
  actor is a positive-side participant controlled by a player (living or incapacitated). Admitted for an incapacitated
  actor because reading is not a bodily act; grants nothing else. Bystanders/hosts/unrelated humans: `NotAuthorized`;
  wrong archive: `RevisionConflict`; wrong episode, duplicate reader, wrong phase or pending care: `Unavailable`; a
  foreign controller's delivery: refused at dispatch.
* **Completed** (new world boundary): an actionless lifecycle batch at Outcome, selected by the session only when
  `WorldEncounterOutcomes.IsReadyToFinalize` (every required human read this exact outcome, no pending care, no hold).
  `Release` + `EndEncounter` + one `EncounterOutcomeRecord`; zero time; bodies, positions, HP/MP, debts, possessions,
  source intervals, exposure/carry, scenario/episode identity and archive retained; no reward/removal/revival/teleport.
  No living initiator or survivor is needed; zero-required-human episodes complete at their next boundary.
* `ReleaseEncounterIntent` is refused in profile 35 (batch construction and local adapter); the lifecycle view offers no
  release token there. Profiles 24–34 keep the initiator-only release unchanged.
* **Care:** acts only on an unfinalized episode and resumes that same fight (original law, unchanged effects);
  completion and readings are refused while care is pending; treatment clears readings of the vanished outcome; after
  completion aftermath care is refused (completed fights never resume). A completed defeat is the explicit terminal state.

## Changed responsibilities (all within owned paths)

Core: `Encounters/EncounterOutcomeState.cs` (owner, readings, record), `EncounterOutcomeCodecV1.cs`,
`AcknowledgeEncounterOutcomeIntent.cs`; `Worlds/WorldEncounterOutcomes.cs` (determination, required readers, readiness,
evaluation, record), `WorldEncounterOutcomeAdmission.cs` (profile 35 fresh admission, declared calibration admission,
validation), `WorldActionCodecV20.cs`, `WorldSnapshotCodecV35.cs`, `WorldStepCodecV35.cs`; edits to `WorldSnapshot`
(owner field, profile 35 derivation, successor retention), `WorldEncounterLifecycleProcessor` (reading + completion),
`WorldCareProcessor` (clear readings on resume), dispatch/profile lists (`WorldStepBatch`, `WorldStepFrameCodec`,
`WorldStepCodec`, `WorldSnapshotCodec` [pre-screen 26 fields], `WorldActionCodec`, care/resource/restorative admission).
Hosting: profile-35 membership of schema eight, recovery, journal policy, archives, paid/proposal/market routes,
`LocalProfileBindings`; `LocalWorldSession` (reading route, completion cutoff, release refusal);
`Simulation/EncounterOutcome/LocalEncounterOutcomeView.cs`; `LocalWorldView.EncounterOutcome` (omitted when null);
`LocalEncounterLifecycleView` (no release token in profile 35); `Content/EncounterOutcome/CargoOutcomeWorldAdmission.cs`;
`RoadThreatWorldAdmission.FindInProfile` (internal; `Find` keeps profile-34 meaning). Host: `--create-cargo-outcomes`.
Client: `FarmClientSession.OpenCargoOutcomes[Fixture]Async` + `AttachCargo` profile check; `FarmGame.Cargo` selection;
`EncounterActionOffers`, `FarmEncounterStatus`, `FarmHud` (profile-35-gated wording/offers);
`scripts/EncounterOutcome/{EncounterOutcomeText,EncounterOutcomeOverlay,NativeEncounterOutcomeEntry}.cs`;
`scenes/NativeEncounterOutcome.tscn`. Test project: one additive pure-client link (`EncounterOutcomeText.cs`).
Durability: `tests/.../Worlds/EncounterOutcome/EncounterOutcomeDurability{Fixture,Result}Tests.cs` and
`tools/check_encounter_outcome_durability.py` (reuses the Host's existing crash/recovery seams; 30 s per operation,
600 s aggregate).
Docs: Runtime README paragraph, `docs/architecture/encounter-outcome.md`.

## Old/new compatibility

* Profiles 1–34: no codec, byte, journal or meaning change; their batches refuse the new intent; profile-34 roots never
  decode as 35 and vice versa (tested). `retained-auth` re-authenticated all six retained native journals (cargo,
  trading, gifts, trading-failed, retreat, road) on the final build.
* `WorldSnapshotCodec` pre-screen now admits 26 root fields (each codec still enforces its exact bound).
* New selection is explicit; a profile-34 save cannot be opened as profile 35 or vice versa (tested); no implicit upgrade.
  Retained-save migration obligation: an explicit, replayed, authenticated transition resolving any in-flight Outcome
  under its recorded law first — not implemented.

## Source calibrations (declared, pinned in admission identity)

* `CargoOutcomeWorldAdmission.CreateWoundedChief(seed, name, 1)`: the Bandit Chief starts at 1 HP (source HP 800) so the
  level-3 player's ordinary Strike can win. Qualification only; not balance.
* `CreateTwoHumanWolves(seed, name, 1)`: both inherited Bramble Wolves bound to human control (their existing bindings),
  first wolf at 1 HP. A managed contrast of readiness rules only; no network/device claim.
* Care test: the player starts at 1 HP (`CreateCalibratedFresh`), so the real wolf bite incapacitates near Wren's real
  agreement/stock. The defeat journey uses the uncalibrated production selection and the source chief.

## Terminal evidence (final sources unless noted)

All under `Beta/implementation/p02/encounter-outcome-continuation/qualification/`:

Final build `build-20261001T235121Z-74283` (sources = build `232553Z` + two new durability test files + the new
durability tool; no product, client or existing-test byte differs between them).

| Gate | Evidence | Build | Result |
| --- | --- | --- | --- |
| build | `build-20261001T235121Z-74283/report.json` | final | accepted |
| outcome-unit | `outcome-unit-20261001T235207Z-7dc53/report.json` | final | accepted, 16 passed (8 Core + 8 Hosting) |
| core | `core-20261001T235329Z-02a91/report.json` | final | accepted, 1772 passed |
| retained-auth | `retained-auth-20261001T235719Z-45cc6/report.json` | final | accepted, 6 retained native journals |
| outcome-durability | `outcome-durability-20261001T235129Z-ea663/report.json` (+ `durability.json`) | final | accepted: 6 real SIGKILLs (exit −9) before/after commit at the player's reading, the other human's reading and world completion; 2 managed authentications passed |
| regression | `regression-20261001T232753Z-73570/report.json` | 232553Z | accepted, 545 passed (440 s). The final-build rerun `regression-20261002T000216Z-a028f` was cut by the 960 s transport timeout (no report; may have left its `dotnet test` child running) — **not current on the final build** |
| fixtures | `fixtures-20261001T234013Z-86d56/report.json` | 232553Z | accepted, 5 exports |
| road-native / road-auth | `road-native-20261001T234122Z-6a9dd/report.json`, `road-auth-20261001T234558Z-83c63/report.json` | 232553Z | accepted (rendered road journeys + authentication) |
| native / native-auth | `native-20261001T234639Z-9498d/report.json`, `native-auth-20261001T234801Z-fa832/report.json` | 232553Z | accepted (rendered retreat journeys + authentication) |
| outcome-fixtures / outcome-native / outcome-auth | — | — | **not run (not implemented)** |

Gates on build 232553Z are stale by source hash only through the added durability test/tool files (dependency
comparison for the lead: none of those files is referenced by product, client or the existing tests).

Managed proofs (Hosting, `Worlds.EncounterOutcome` / `Content.EncounterOutcome`; Core `Encounters.EncounterOutcome`):
victory over the wounded chief → Full Wait held for 12 cutoffs → reopen at Outcome (identical root hash) → reading →
completion (zero time, all bodies/HP/MP/debts/lots/balances/positions retained, record) → exact retry returns the
original receipt → changed content under the same command id refused → repeated completion refused → re-reading
`Unavailable` → walk the remaining lane paying retained debt → sell the retained ration at Kegga; real defeat by the
source chief → readable reason + "no admitted care" → reopen → incapacitated reading → completion → terminal view → no
re-entry → close/reopen identical; two-human contrast (bystander `NotAuthorized`, forged player/host deliveries refused,
session refuses foreign actor, stale/wrong readings, one of two readings holds 12 cutoffs, not-ready session holds,
duplicate reader refused, completion lists both readers, incapacitated wolf retained); care (reading then stale care
offer refused, fresh care accepted before completion, completion refused while pending, treatment resumes the same
episode and clears readings, second defeat read and completed, care no longer offered after completion); premature
reading/completion during Active refused; codec canonical/malformed/bounds, action v20, step v35, batch admission.

## Retained failures (preserved, not hidden)

* `build-20261001T224018Z-dd9eb`: duplicate member names in the view (fixed).
* `outcome-unit-20261001T225005Z-72897`: corridor derived from the camp region; raw timing due-actors included side-zero
  humans; human choices captured before automatic choices settled (test harness defects, fixed).
* `outcome-unit-20261001T225407Z-8b2b3`: stale care offer reused after the reading moved the boundary (now asserted as
  a refusal; fresh offer used).
* `regression-20261001T231502Z-ed5fb`: killed at the wrapper's 600 s bound with 235 passed and 0 failed — my four
  multi-minute journeys had joined the Simulation selection. They now live in the permitted `Worlds.EncounterOutcome`
  namespace (file still beside its helper under `Simulation/EncounterOutcome/`; a pure relocation remains for the lead).

* `build-20261001T235024Z-21bb7`: missing `using` in the durability fixture (fixed). `build-20261001T235038Z-5be55`
  and `build-20261001T235059Z-a02ae`: Godot editor startup error `EditorSettings not instantiated yet …
  android_sdk_path` with no `client/` change since the accepted build (environmental; the third attempt was accepted).

## Remaining gaps

1. Native rendered qualification of the outcome world (probe, checker with opening/keyboard/route/reopen bounds,
   fixture export and managed result authentication, rendered images and post-draw visibility) — not implemented.
2. Durability covers the two-human fixture's readings and completion (real kills); the victory/defeat journeys are
   covered by managed close/reopen only. Regression is not yet re-run on the final build (see table).
3. `NoSurvivors` is handled by the same path but unreachable by a real committed sequence under current single-target
   action rules; only codec/record/view paths cover it.
4. A care obligation whose helper later becomes unavailable still holds the unfinalized episode (unchanged care law);
   the native controls only offer care when the care view admits it.
5. After a completed defeat the world keeps running in exploration around an incapacitated player (no death/corpse/
   ghost/rescue rule exists); "Save and leave" closes the session. The overlay's button is unqualified natively.
6. Migration of lived profile-34 saves; multi-session/network authentication of the second human.

## Observed friction

* The wrapper forbids deleting/renaming files, so the journey test file could not be physically moved after the
  regression bound was hit; the namespace was moved instead.
* Long-running journeys (≈1.5–1.7 min each) dominate wall time; native and durability work did not fit the 7,200 s window.
