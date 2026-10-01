# Road threat result (Bandit Chief at the cargo-lane camp)

Worker: Opus 5.5 / 1M / requested Max (as dispatched). Starting revision 223160e, branch `codex/runtime-foundation`.
No staging, commits, provider retry, fallback or delegation. Design: [DESIGN.md](DESIGN.md). Timeless reference:
[road-threat.md](../../../Runtime/docs/architecture/road-threat.md).

## Delivered (real application increment)

| Responsibility | Files |
| --- | --- |
| Source binding (digest-pinned files, pointers, raw-field checks, payload hashes, retained-only fields) | `src/OpenFantasy.Hosting/Content/RoadThreat/RoadThreatSources.cs` |
| Fresh genesis: one chief from `/spawners/33` in every coupled owner; hostile scenario; wolf scenarios gain a bystander | `Content/RoadThreat/RoadThreatGenesisExtension.cs`, `RoadThreatWorldAdmission.cs` |
| Reuse without duplication (behaviour/bytes unchanged) | `Content/Cargo/CargoWaterRebinding.cs` (extracted), `CargoGenesisExtension.cs`, `CargoWorldAdmission.cs` (internal genesis hook) |
| Host-controlled hostile entry under the chief's own control/readiness/sightline | `Simulation/RoadThreat/LocalRoadThreatController.cs`, `LocalWorldSession.cs` (`TickHostileEntry`) |
| Public warning projection (omitted from public encoding when absent) | `Simulation/RoadThreat/LocalRoadThreatView.cs`, `LocalWorldView.cs` |
| Native `--cargo-threat` entry, opening, warning text, HUD status | `client/scenes/NativeRoadThreat.tscn`, `client/scripts/RoadThreat/{NativeRoadThreatEntry,RoadThreatWarning,RoadThreatPlayProbe}.cs`, `FarmClientSession.cs`, `FarmGame.Cargo.cs`, `FarmEncounterStatus.cs` |
| Native checker | `tools/check_native_road_threat.py` |
| Tests | `tests/.../Content/RoadThreat/RoadThreatAdmissionTests.cs`, `Simulation/RoadThreat/{RoadThreatJourney,RoadThreatJourneyTests,RoadThreatInitiationTests}.cs`, `Client/RoadThreat/{NativeRoadThreatFixtureTests,NativeRoadThreatResultTests}.cs` |
| Docs | new `docs/architecture/road-threat.md`; index; `cargo-circuit`, `encounter-lifecycle`, `encounter-retreat`, `native-encounters`, `cargo-presentation`; `Runtime/README.md` |

No Core file, codec, schema, profile, root field, scene/asset/project file, protected test or protected probe changed.
`Hosting.Tests.csproj` is unchanged (no compile link was needed). Old worlds keep their genesis bytes (retained
fixtures still match) and their public view bytes (retained recordings authenticate).

## Semantics and source mapping (exact)

* `monsters.json` `/monsters/33` (file digest `8a3599…1493`, the wolf binding's witness) and `world.json` `/regions/16`,
  `/spawners/33` (digest `9f46be…668d`). Executed: name, level 7, stats 800/0/27/0/15/10, attack power 15, range
  50 units × 8 = 400 subunits (legacy `UnitsPerTile=32`, 256 subunits/cell, same scale as Bite), ATB 480 → readiness
  480/300. Retained only: aggro 140, leash 400, moveSpeed 4, xp 1400, gil 260–480, three drops, respawn 3600.
* Stance: authored tile (347,311) inside the spawner/camp rectangle and the existing corridor, one cell east of the
  lane crossing x=346; humanoid body from the world's admitted definition (explicit transform). Empty owner-only store,
  zero wallet, host control, gathering slot, construction exit, lifecycle debt row, resource history.
* Scenario `bandit_chief_road`: initiator chief (side 2, attack), target player (side 1, Strike), every other resident
  side 0; entry range 400. Chief is a side-0 bystander in both wolf scenarios. Profile 34 (retreat law unchanged).
* Initiation: unheld exploration cutoff, no pending player request, chief alive with zero debt, all positive
  participants alive, `QueryInteraction(chief, player, 400) == Reachable`; command under the chief's principal/session
  and revisions; authority re-validates; a would-be refusal is not committed. Reads only the selected root.
* No reentry loop: entry ≤ 400 < clear-of-contact 656, so a completed retreat cannot be followed by automatic entry;
  walking back within reach re-initiates (proved after reopen). Chief never moves, is never erased or healed.
* Danger: hits of 30–39 on the early Squire; a level-3 player (after Wren's reward) went 78 → 42 natively; staying to
  fight incapacitates (Outcome holds, no release/rescue/reward/loot). Bypass three cells west is paid by steps.

## Verification (wrapper `run_checks.py`, final coherent source; build `build-20261001T190858Z-945f2`)

| Mode | Result | Evidence (`qualification/…`) |
| --- | --- | --- |
| build | accepted, 40 imports | `build-20261001T190858Z-945f2` |
| unit | 6/6 passed, 88.1 s | `unit-20261001T191210Z-c9f3e` |
| fixtures | 5/5 (road, retreat, cargo, trading, gifts), 75.8 s | `fixtures-20261001T191342Z-cf257` |
| road-native | keyboard + controller + pointer + paused reopen passed, 149.3 s | `road-native-20261001T191501Z-3648e` |
| road-auth | NativeRoadThreatResultTests passed, 32.8 s | `road-auth-20261001T191733Z-3741e` |
| regression | 531/531 passed, 339.2 s | `regression-20261001T191832Z-3e32f` |
| native / native-auth | passed 61.7 s / 1 passed 13.6 s | `native-20261001T192415Z-5e194`, `native-auth-20261001T192520Z-dee4b` |
| durability | 6 actual kills, 2 passed, 30.8 s | `durability-20261001T192539Z-d370e` |
| legacy-native / legacy-auth | passed 97.4 s / 2 passed 121.1 s | `legacy-native-20261001T192613Z-2cf3b`, `legacy-auth-20261001T192754Z-7b090` |
| retained-auth | 5 retained recordings passed, 160.1 s | `retained-auth-20261001T190925Z-c9c9d` |
| cargo-native / cargo-auth | passed 446.1 s / 1 passed 38.5 s | `cargo-native-20261001T193034Z-8b5c5`, `cargo-auth-20261001T193803Z-5368f` |
| core | 1,764/1,764 passed, 101.7 s (Core source byte-unchanged) | `core-20261001T193847Z-bac79` |

All fifteen modes are accepted against the same final source and build report, with `preservationBefore/After`
reporting the 587 protected tests, 23 frozen specs, imported art, original database and predecessor index exact.
Distinct managed cases on this source: Core 1,764; Host regression 531 (includes the 6 unit cases and the fixture
test without export); plus fixture exports 5, road/retreat/cargo/legacy authentication 1+1+1+2, durability 2 and
retained authentication 5 (opt-in result tests are counted by their mode).

Native rendered evidence (final): `road-native-20261001T191501Z-3648e/native-road-threat-bja0fon4/` (`keyboard.*.png`
warning, engaged, hit, retreat-inspected, retreating, disengaged, detoured, gearford, sell-review, sold, final; the
controller/pointer sets; `reopen.reopen.png`; `native-manifest.json`). Fixture boundary: managed preparation earns
Wren's reward, buys one Marla ration and walks to road cell (346,318); native begins there, paused.

## Failures retained (no assertion, deadline or rule weakened)

* `build-20261001T182128Z-7f051`: two test compile errors (missing `using`). `unit-…182239Z`, `unit-…182420Z`: my
  test assumptions (level-up HP 78; receipt equality; Active-phase wait). `build-…183551Z`: `string.Create` misuse.
* `build-…182202Z`, `build-…182653Z`: Godot editor `EditorSettings not instantiated … android_sdk_path` with no client
  change; the identical command passed on retry. Environmental/intermittent, not diagnosed further.
* `road-native-20261001T183739Z-77684`: probe required Strike in the same frame the retreat offer became queueable
  ("Preparing this turn's choices…"); the probe now waits for the executable offer (no rule/deadline change).
* `retained-auth-20261001T190738Z-540e0`: a genuine compatibility regression I introduced — an always-present
  `LocalWorldView.RoadThreats` changed the reflection-serialized public view of older worlds. Fixed by the existing
  contract (null + omitted when absent, like `Retreat`) and a new assertion; all reruns above are after the fix.

## Remaining obligations / not claimed

Pursuit, aggro radius, leash/return, approach movement, territorial memory beyond geometry, victory rewards/gil/drops,
respawn, NPC release after incapacity, robbery/rescue, escort wages, traveling merchants, party retreat, multiplayer,
lived-save migration, procedural frontier, new art (the chief reuses the Squire prototype sprite), physical devices/OS
focus and human acceptance; full cargo/combat/P02 exits remain open. Reading-hold initiation is enforced by
`IsUnheldExploration` and the authority but has no session test at the camp (no dialogue partner there).

## Friction

My first tool call used a shell `ls`/`cat` before reading the Bash restriction; every later Bash call was an exact
wrapper command. No permission denials. Usage and time are reported by the adapter, not estimated here.
