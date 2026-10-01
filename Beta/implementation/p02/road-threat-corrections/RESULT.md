# Road-threat corrections: result

Worker: Opus 5.5 / 1M / requested Max (as dispatched; no delegation, model switch or retry). Base: delivery `48fb19e`
plus the lead's two regressions (prepared `f36d6c0`). No commits, staging, branch or index changes. Design:
[DESIGN.md](DESIGN.md). Disposition: [CRITIC_DISPOSITION](../road-threat/CRITIC_DISPOSITION.md).

Status: implementation complete; all 15 wrapper modes accepted on one final source and build
(`build-20261001T205659Z-6c6e2`). No full road-threat, combat, cargo or P02 acceptance is claimed.

## Responsibilities and changed files (paths under `Beta/Runtime/`)

| Responsibility | Files |
| --- | --- |
| D2: eligible hostile entry owns its cutoff; waiting ordinary request refused at that boundary | `src/OpenFantasy.Hosting/Simulation/LocalWorldSession.cs` |
| D3: observation-bounded threat projection (existing 768-subunit `QueryInteraction`: floor, range, sightline) | `src/OpenFantasy.Hosting/Simulation/RoadThreat/LocalRoadThreatView.cs`, `Simulation/LocalWorldView.cs` (doc) |
| Warning copy ("Danger nearby", tooltip "attacks you") | `client/scripts/RoadThreat/RoadThreatWarning.cs` |
| Codename removal ("Beta cell" → "runtime cell") | `src/OpenFantasy.Hosting/Content/RoadThreat/RoadThreatSources.cs`, `docs/architecture/road-threat.md` |
| Native keyboard join from a fresh farm fixture; unobserved/observed warning stages | `client/scripts/RoadThreat/RoadThreatPlayProbe.cs`, `tools/check_native_road_threat.py` |
| Farm fixture export (`farm/`, commit zero) | `tests/OpenFantasy.Hosting.Tests/Client/RoadThreat/NativeRoadThreatFixtureTests.cs` |
| D1: complete-journal authentication | `tests/OpenFantasy.Hosting.Tests/Client/RoadThreat/NativeRoadThreatResultTests.cs` |
| Item 3, R1, R2, R4, R5 contrasts | new `tests/OpenFantasy.Hosting.Tests/Simulation/RoadThreat/RoadThreatBoundaryTests.cs` |
| Adapted delivered assertions that demanded a globally visible row | `RoadThreatJourneyTests.cs` (Marla), `RoadThreatInitiationTests.cs` (lane), `RoadThreatAdmissionTests.cs` (farm), `NativeRoadThreatFixtureTests.cs` (road start) |
| Timeless references | `docs/architecture/{road-threat,encounter-lifecycle,native-encounters}.md`, `README.md` |

Unchanged: Core, codecs, schema, profiles, release law, scenes/assets/project metadata, wrappers, the two lead
regression files (byte-pinned by the wrapper), all 587 protected tests, old probes/drivers/evidence. No compile link.
Source mapping (`RoadThreatSources`) is unchanged except the comment wording.

## Findings resolved

| Finding | Resolution and proof |
| --- | --- |
| D2 starvation | Entry is selected after the genuine holds and before the ordinary step even with a pending request; the request is refused at that cutoff. Lead `RoadThreatSchedulingRegressionTests` passes unchanged; `RoadThreatBoundaryTests.A_waiting_request…` proves entry at the first eligible cutoff, no player sample in it, refusal without another cutoff, answered refusals and continued walking outside reach, no later gathering. |
| D3 disclosure | Rows only under `QueryInteraction(player, chief)` = `Reachable` (768 subunits). Lead `RoadThreatObservationRegressionTests` passes unchanged. `…warning_row_follows_actual_observation…`: farm/Marla/road/809-subunit lane empty, 572-subunit Watching, row ⇔ Core query at every walked cutoff; declared calibration of the spatial owner only: wall → Occluded, farm receiver basin → DifferentFloor, bank → observed. Old worlds stay null/omitted (admission test, retained-auth). |
| D1 journal auth | `NativeRoadThreatResultTests`: complete-journal replay; player commands from entry to last retreat sample exactly `[strike, retreat]` (keyboard) / `[retreat]`; samples only from `EncounterResolution` of the active recorded retreat with the player due and a `TacticalAdvance` between; last sample = `WorldSequence`, its feet = `record.Final`; final feet = report = authored destination; entry batch moves/acts nothing for the player; keyboard reward, Marla purchase (exact sub-interval of her shelf, merchant +total) and Kegga sale (same interval, merchant −total). Checker also requires equal inspect/commit timing. |
| Item 4 native join | Keyboard starts at the fresh `farm/` fixture (commit zero): E,G,G,E with Wren (reward 200), walk to Marla, stall ask/review/confirm (one ration, 20), outbound lane, unobserved at (346,318), warning observed at 749 subunits, entry, strike, hit (HP 42), inspect/cancel/commit Retreat (10 samples), detour, Kegga sale (+38, balance 218), pause, separate reopen. Controller/pointer keep the declared near-road fixture. |
| Item 3 / R2 / R4 | `Readiness_and_recorded_state…`: complete-journal reopen mid-confrontation, mid-retreat and after disengagement (both debts identical after reopen); entry law checked every cutoff (count rises exactly when `CanInitiate` held) on the immediate return. |
| R1 | Loss: Outcome reopened exactly, further actions refused, movement ignored, no release/rescue. Victory (chief at 0 HP) is not reachable without invented state; documented as a terminal-release blocker under the unchanged Core law. |
| R3 | Recorded prototype limitation (one-cell sidestep) in `road-threat.md` and README; nothing buffed or blocked. |
| R5 | Production `OpenCargoThreatAsync` create/reopen and refusal both ways with the retreat world's save (`…production_threat_selection…`). Reading/care at the camp are not reachable in live play (no speaker or care helper within the chief's reach); they remain proven only by the shared `IsUnheldExploration`/session ordering, not by a camp test. |
| R6 | No new owner/format. The player's forged chief entry is refused (`NotAuthorized`, initiation test); durability's six kills are generic retreat storage evidence, not a kill at the host-entry boundary; no conflicting-identity host-entry case was added. |

## Verification (final source; wrapper `run_checks.py`, evidence under `qualification/`)

| Mode | Result | Evidence |
| --- | --- | --- |
| build | accepted, 24.3 s | `build-20261001T205659Z-6c6e2` |
| fixtures | 5/5 (road incl. `farm/`, retreat, cargo, trading, gifts), 73.7 s | `fixtures-20261001T205728Z-b25dd` |
| road-native | keyboard 214.9 s (probe 209.1 s), controller 19.8 s, pointer 19.3 s, reopen 10.1 s; aggregate 265.5 s | `road-native-20261001T205845Z-45295` (`native-road-threat-wlr12q87/`) |
| road-auth | 1/1, 33.8 s | `road-auth-20261001T210316Z-35b4f` |
| unit | 12/12 incl. both lead regressions, 156.3 s | `unit-20261001T210401Z-954fe` |
| regression | 537/537, 472.0 s | `regression-20261001T210649Z-45cdd` |
| native / native-auth | accepted 65.2 s / 1/1 14.7 s | `native-20261001T211447Z-048c5`, `native-auth-20261001T211555Z-51089` |
| durability | 6 actual kills, 2/2, 44.0 s | `durability-20261001T211613Z-6ce52` |
| cargo-native / cargo-auth | accepted 453.0 s / 1/1 48.2 s | `cargo-native-20261001T211701Z-21f79`, `cargo-auth-20261001T212438Z-d4265` |
| legacy-native / legacy-auth | accepted 82.6 s / 2/2 55.1 s | `legacy-native-20261001T212530Z-18892`, `legacy-auth-20261001T212656Z-54816` |
| core | 1,764/1,764, 153.8 s (Core source unchanged) | `core-20261001T212754Z-3af0b` |
| retained-auth | 5/5 retained recordings, 162.5 s | `retained-auth-20261001T213040Z-c17ee` |

Every report shows `preservationBefore/After` exact (587 tests, 23 specs, imports, database, predecessor index, the two
lead regressions). Native limits unchanged (30/720/750/300/330/90/900).

## Failed attempts (retained, nothing weakened)

- `unit-20261001T203436Z-b2550`: the delivered admission test demanded a farm row (now empty by design, adapted);
  my strict `IsCompleted` check raced the async `SubmitAction` continuation (now waits without another cutoff).
- `road-native-20261001T203846Z-c74c4`: the probe passed; my checker required a `walk-to-wren` hold, but Wren stands
  beside the farm start (requirement removed, other walk stages still required).
- `unit-20261001T205255Z-aafea`: my assertion treated gather `Effort` (a regenerating budget, 0→1000) as work done;
  replaced by the oak's remaining swings.
- `retained-auth-20261001T210642Z-c239d`: `FileNotFoundError` — the corrections wrapper reads `RETAINED_NATIVE.json`
  from this packet, which did not exist then. It existed at the 21:30 rerun; I did not create it.
- Intermediate accepted builds/fixtures/native/auth runs before the last fixes are superseded, not final evidence.

## Remaining limits (explicit)

- The warning frame shows the HUD warning and the chief is drawn in the canvas, but at the 749-subunit sighting its
  projected point lies outside the field viewport (`ChiefInField: false`, camera framing); a visible pre-contact chief
  sprite in the field is not proven. Camera framing was not changed (shared cargo agreement contract).
- Three-cell observation gives about 0.65 s of walking warning; no general perception/visibility capability.
- No pursuit, aggro radius, leash/return, territory memory, rewards/gil/drops, respawn, NPC release/rescue/revival,
  wages, escorts, traveling merchants, party retreat, multiplayer, lived-save migration, procedural frontier.
- Terminal Outcomes (loss proven; victory unreachable here) block a shipping combat loop under the existing law.
- Injected input only: no physical devices or OS focus; prototype Squire sprite for the chief; no art or human
  acceptance; full cargo/combat/P02 gates remain open. Reading/care holds at the camp are not live-reachable.
- The earlier intermittent Godot editor `android_sdk_path` error in the predecessor run stays unexplained (not seen here).

## Friction

All Bash calls were the exact wrapper commands. The retained-auth pin was absent at first (above). The full chain had
to be repeated after each test-only fix because any source byte invalidates prior evidence; this consumed most of the
time budget. Usage and elapsed totals are reported by the adapter, not estimated here.
