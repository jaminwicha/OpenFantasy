# Encounter outcome corrections and native completion — result

Status: **corrections implemented; every required gate executed and accepted on one final build**
(`build-20261002T012919Z-c88cf`; every later report's `sourceSha256` equals that build's). Readability is proven for
the outcome panel; a HUD-footer clipping defect after completion was found in the final frames and remains open
(obligation 0). Model: exact
`claude-opus-5-5[1m]`, one unit, no recursive agents, no Git/index operations, no package/toolchain change. Design:
[DESIGN.md](DESIGN.md). No full-phase, network, production, balance, human art/UI or credit-savings claim.

## Changed responsibilities

* **Core.** `Encounters/EncounterOutcomeState` refuses any rules pin but `encounter.outcome.v1` (F1).
  `Worlds/WorldEncounterOutcomeAdmission.Validate` adds the available cross-owner relation: while the lifecycle retains
  the record's own episode archive, the record must name that archive's final hash (F6); its doc now separates
  structural admission from replay authentication.
* **Hosting.** `Simulation/EncounterOutcome/LocalEncounterOutcomeView`: participants carry outcome facts (`Side`,
  `OnStandingSide`) and present `Condition` only for the observer or a participant observed now under
  `LocalRoadThreatView.Observes` (F2); `ObserverIsParticipant`; `IsTerminal` only for a participant (F5).
* **Client.** `EncounterOutcome/EncounterOutcomeText` (every care reason from `LocalCareView`; completion-policy terminal
  text; Accept outcome for an incapacitated winner; decline wording; observer wording; participant lines) (F3/F4/F5);
  `EncounterActionOffers` (no reading offer for an observer; care-aware blocker/detail); `FarmInputRouter` +
  `FarmGame` (`HasOutcomeReadingBesideCare`: a profile-35 reading stays permitted beside admitted care — the old rule
  refused *every* reading while care was presented, which made the deliberate decline impossible natively; default
  false keeps earlier laws); `EncounterOutcomeOverlay` now instantiates the new scene-owned layout
  `scenes/EncounterOutcomeOverlay.tscn`; `NativeEncounterOutcomeEntry` gains the disposable probe invocation with
  background rendering set before window content; new `EncounterOutcomePlayProbe`.
* **Tools.** `check_encounter_outcome_durability.py`: explicit elapsed checks after successful signal and kill waits
  and every operation, re-asserted over the recorded rows (bounds unchanged: run/signal 30 s, kill 10 s, aggregate
  600 s). New `check_native_encounter_outcome.py` (original limits: opening 30, keyboard 720/750, other routes 300/330,
  reopen 90, aggregate 900 s).
* **Tests (new/unaccepted outcome tests only; protected bytes preserved — 597 existing tests, 23 frozen specs, one
  additive link).** Core `EncounterOutcomeOwnerTests` (+foreign-pin case). Hosting `Worlds/EncounterOutcome/`
  `EncounterOutcomeCorrectionTests` (4 journeys), `NativeEncounterOutcomeFixtureTests`, `NativeEncounterOutcomeResultTests`;
  `Client/EncounterOutcome/EncounterOutcomeTextTests` (6); strengthened `EncounterOutcomeJourneyTests` (F2 at Kegga,
  side-zero observer, care-reason and terminal assertions) and `TwoHumanOutcomeWorld.CommitCommand`.
* **Docs.** `docs/architecture/encounter-outcome.md` rewritten timelessly (acceptance closes care, facts vs condition,
  pin/structural vs replay, latest-record bound, overlay scene, no-survivor boundary; progress text removed);
  architecture index row; Runtime README paragraph + native outcome tooling.

## Critic findings

| Finding | Disposition | Regression (all executed) |
| --- | --- | --- |
| F1 pin | Fixed at the owner; codec and root decode now refuse. | owner ctor/codec repin cases; full-root repin of genesis, Outcome and completed roots |
| F2 live condition | Fixed: record facts vs present condition; observer's own or bounded observation only. | victory → walk to Kegga: far chief `Condition` null, own present; side-zero observer equivalence with `Observes`; completed-root projection |
| F3 care copy | Fixed: reasons from the care view (no agreement / out of reach / out of date / not covered / unable; pending-unavailable); terminal states policy; incapacitated winner. | pure-client reason tests; real decline journey with Wren `CanAcknowledge`; defeat journey asserts the view-derived reason; native care route |
| F4 care window | Fixed by law statement + presentation, not by weakening ordering: acceptance closes care at the next ready cutoff; committed care before completion still wins. | decline journey: next ordinary unqueued cutoff completes, late care refused; two-human contrast below |
| F5 observer | Fixed: no reading owed/offered, "between others" wording. | two-human journey projects `SecondWolf`; text tests |
| F6 root negatives | Fixed + tested by actual `WorldSnapshotCodec.Decode` of real roots with only the owner field re-framed: side-zero and host readers, stale hash, other episode, future sequence, record naming current episode, count > episodes (Outcome and completed roots), out-of-phase reading, retained-archive hash mismatch, non-resident reader, foreign pin; a well-formed uncommitted reading decodes (structural ≠ replay). | `CompleteRootDecodeRefusesForeignPinsAndImpossibleOwnerContents` |

## Terminal evidence (final build `012919Z`; under `qualification/`)

| Gate | Report | Result |
| --- | --- | --- |
| build | `build-20261002T012919Z-c88cf` | accepted (Godot import/editor clean) |
| outcome-unit | `outcome-unit-20261002T012955Z-3aa41` | accepted, 27 passed (9 Core + 18 Hosting) |
| core | `core-20261002T013523Z-bf7d8` | accepted, 1,773 passed |
| regression | `regression-20261002T013735Z-e54eb` | accepted, 552 passed, 505.5 s of 600 s |
| retained-auth | `retained-auth-20261002T014615Z-d21ee` | accepted, 6 retained native journals |
| outcome-durability | `outcome-durability-20261002T015001Z-b93cb` (+`durability.json`) | accepted, 6 real SIGKILLs (−9) before/after; 2 authentications |
| outcome-fixtures | `outcome-fixtures-20261002T013138Z-6b41d` | accepted (victory/defeat/care + manifest) |
| outcome-native | `outcome-native-20261002T013257Z-b740f` (`outcome.json`, images in `native-encounter-outcome-6tjh618k/`) | accepted, 112 s |
| outcome-auth | `outcome-auth-20261002T013454Z-ce88d` | accepted |
| fixtures | `fixtures-20261002T015032Z-b752f` | accepted, 5 exports |
| road-native / road-auth | `road-native-20261002T015128Z-9a2c8` / `road-auth-20261002T015606Z-dbd43` | accepted |
| native / native-auth | `native-20261002T015650Z-081ab` / `native-auth-20261002T015810Z-7a3a0` | accepted |

No earlier evidence is reused for a current claim; the inherited packet's reports remain baseline only.

**Native routes** (fixture preparation vs native input): *victory* — managed prep earns, buys and walks to (346,318);
native held-Up walk, the wounded chief's own entry, E strike, panel read while the world holds, Continue (E), world
completion, held-key detour to Gearford, Kegga sale through the stall panel, pause. *defeat* — same prep with the
production chief; native strikes until incapacitated, panel states incapacity and the care reason, Accept defeat (E),
terminal panel, **Save and leave** pointer click closes the process (exit 0); a separate paused process reopens the
same save with identical record/HP/feet/count and unchanged bytes. *care* — declared player HP1; managed prep walks
beside the first wolf **and performs the player's own entry**, stopping at the first decision boundary; native G
Waits until the wolf's real bite, Wren's admitted care is shown ("Wren can help now … Accepting declines that care"),
the HUD Accept defeat button is clicked (no E), the world completes and no care survives. Panel bounds (post-draw) are
asserted inside the field, clear of target bar and both sprites, all lines visible. Frames inspected: final run
`care.care-declined.png`; same-layout `victory.victory-outcome.png`, `care.care-outcome.png`, `defeat.terminal.png` from
the preceding accepted build-`011750Z` run. **Observed defect (not checked by the probe):** in the terminal exploration
state the HUD footer repeats the long acceptance message and terminal status and clips them horizontally at the left
edge (visible in `care-declined`/`terminal`); the outcome panel carries the complete readable text. Result tests re-derive each genesis from seed/name/
calibration and compare bytes, replay whole journals, and authenticate command identities (player's own binding, one
entry by the chief/one prefixed player entry, one reading of the exact outcome, one actionless zero-duration
completion, no care request; care available at the declined reading; Kegga sale of the carried lot; defeat keeps it).

## Source calibrations (declared, re-derived in authentication)

`CreateWoundedChief(seed, name, 1)` (victory); uncalibrated `CreateEmbedded` (defeat); `CreateCalibratedFresh(player
HP1)` (care, decline journey); `CreateCalibratedFresh(player HP1, first wolf human)` (two-human care contrast);
`CreateCalibratedFresh(both wolves HP1)` (two episodes); `CreateTwoHumanWolves(…, 1)` (root negatives). Qualification
parameters only.

## Proof status

* **Two-human care contrast: proven (managed).** Wren's patient at HP1; human wolf wins with its own abilities; wolf
  reads first; patient requests admitted care; one treatment commit (`revision + 1`) resumes the same episode and
  clears the wolf's reading; `FindReceipt` of the old command returns the original receipt; the old reading is
  unavailable in the resumed Active episode; at the resumed outcome both are awaited, the old hash is
  `RevisionConflict`, both read, world completes. Ordinary unqueued cutoff contrast and deliberate decline with
  identical terminal reopening: proven (managed + native decline).
* **Two episodes in one world: proven (managed)** over the two inherited wolves: count 2, `Last` replaced, view null at
  the second entry, prior reading/entry command identities refuse new content, first receipt retained, zero-duration
  completions with conserved bodies/HP/MP/debts/lots/balances. Using an *episode id* as a command id was not asserted:
  command identities are a per-principal journal namespace and no occupancy rule applies to readings.
* **Q1:** the second human remains direct authority commits under its own binding — not a second session or network.
* **No survivors: open, investigated.** Under the single-target resolver one resolution changes one target and the first
  resolution leaving ≤1 positive side standing moves to Outcome and cancels pending choices; no admitted entry/world
  route yields zero survivors. A simultaneous/area rule would be required; none was added (documented in architecture).

## Compatibility

Profiles 1–34: no codec, byte, journal or meaning change (core 1,773 and retained-auth 6 pass). Profile-35 bytes are
unchanged; the pin and archive-hash checks refuse only roots no lawful completion produces (completion records the
retained archive's own hash). Selection stays explicit; no implicit migration; the lived profile-34 migration
obligation remains as documented.

## Preserved failures (all under `qualification/`)

`build-…004444Z-c8ece`, `…004457Z-3a69f`, `…005454Z-e9ead` (compile errors, fixed); `build-…010533Z-42acd` (Godot
`EditorSettings … android_sdk_path`, environmental; identical retry accepted). `outcome-unit-…004542Z-8423c`,
`…012210Z-74f80`, `…012631Z-cef1b`: two-human harness gave the human wolf the player's `strike` (refused; both then
waited); fixed by choosing the wolf's own abilities like the host controller. `outcome-native-…005621Z-5a904`: panel
298 px overran the encounter field (ends y=337) — real layout finding, fixed in the scene. `…005845Z-25e50`,
`…010653Z-83306`: when the native route walked/entered itself after real-time exploration, Wren's care was
`Unavailable` (helper at 256 subunits, in reach) — cause **not isolated** (helper readiness suspected, unverified);
the care route's entry was then moved into declared preparation. `…010257Z-671e9`: probe threshold 240 < Wren's
exact 256. `…011134Z-09cad`: router refused the reading beside care (product defect, fixed). `…011546Z-658c1`:
same-frame stale care offer check (probe timing, fixed). Superseded accepted chains on builds `011750Z`/`005514Z` etc.
are retained.

## Remaining obligations

0. HUD footer readability after completion: give `FarmEncounterStatus`/the acceptance message a short footer form (the
   panel keeps the full text) and extend the probe's bounds check to the footer labels; re-run outcome-native/auth.
1. Native **accepted-care** route (E → care → treatment resumes → later outcome) — managed only.
2. Isolate why native self-entered care became `Unavailable` after exploration (helper readiness vs other cause).
3. HUD footer shows `[E]` on both the care and accept buttons (existing HUD labels); wording explains it, but a
   distinct accept label/binding is a UI follow-up. No dedicated unit test of the router flag or of the observer's
   empty offer map (covered natively/authenticated and by projection tests respectively).
4. No-survivor resolver proof; lived profile-34 migration; multi-session/network readiness.

## Observed friction

Each native iteration needs build → outcome-fixtures → outcome-native (~4–5 min) because fixtures pin the exact build;
seven native attempts consumed most of that. The 960 s transport never limited a gate. Broad suites ran once each on
the final build (no repeated identical suites).
