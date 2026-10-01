# Combat-retreat correction — result

Status: **not fully qualified.** Twelve of thirteen modes pass on the final source/binaries. **`legacy-auth` failed
twice** on the final build (see Open obligations), and the items listed there remain open. Route:
`claude-opus-5-5[1m]`, requested Max, one request, no agents, no Git, no paid retry. Final source = build
`build-20261001T160548Z-49216`.

| Mode (final build) | Report | Result |
| --- | --- | --- |
| build | `build-20261001T160548Z-49216` | accepted |
| unit | `unit-20261001T160605Z-0e7dd` | 15 passed |
| core | `core-20261001T161511Z-9d1fd` | 1,764 passed |
| regression | `regression-20261001T161116Z-1330c` | 518 passed |
| fixtures | `fixtures-20261001T160724Z-15cee` | 4 exports |
| native / native-auth | `native-20261001T160906Z-3ee6b` (53.4 s) / `native-auth-20261001T161003Z-7b98c` | accepted |
| durability | `durability-20261001T161017Z-bd3cd` | 6 SIGKILLs, 2 managed tests passed |
| retained-auth | `retained-auth-20261001T162220Z-8b54c` | accepted cargo/trading/gift manifests re-verified |
| cargo-native / cargo-auth | `cargo-native-20261001T162411Z-ff1da` (439.4 s) / `cargo-auth-20261001T163134Z-366d7` | accepted |
| legacy-native | `legacy-native-20261001T161819Z-7af6f` | accepted (probes passed) |
| **legacy-auth** | `legacy-auth-20261001T162056Z-fca20` | **failed**: `NativeTradingResultTests.cs:120` |
Decisions: [DESIGN.md](DESIGN.md). Profile 34 law is now `encounter.retreat.v2` (new pin; the original packet's
profile-34 artifacts remain historical pre-correction evidence; older profiles and public bytes unchanged).

## Findings disposition (paths relative to `Beta/Runtime`)

| Finding | Disposition | Where |
| --- | --- | --- |
| P1 complete admission | Fixed: one root admission (`WithState`, as ordinary movement) for every queue prefix, advisor verification (memoised per search) and each sample (publication owners); only that admission is a gameplay refusal | `src/OpenFantasy.Core/Worlds/WorldEncounterRetreats.cs` (`AdmitsPlacement`, `EvaluateRoute`), `WorldEncounterRetreatAdvice.cs`, `WorldEncounterRetreatResolver.cs` |
| P2 contact | Fixed: horizontal body-centre distance > longest range + 256, no cap, sightline/floor ignored; advisor ranking uses the same `ContactExcess` | `WorldEncounterRetreats.cs`, `src/OpenFantasy.Core/Encounters/EncounterRetreatRules.cs` |
| P3 scope | Fixed: exactly two positive sides; the actor is the only combatant of its side, living or downed | `EncounterRetreatRules.IsSupportedScope`, `WorldEncounterRetreats.IsSupportedSide` |
| P4 held actor | Fixed: `Retreating` blocker for Wait/abilities, `PlayerDecisionQueued` while held, HUD "Retreating · step x of y", one notice per new terminal record | `src/OpenFantasy.Hosting/Simulation/{LocalEncounterActionView,LocalWorldView}.cs`, `client/scripts/{FarmEncounterStatus,FarmGame}.cs`, `client/scripts/Retreat/RetreatOffers.cs` (`RetreatOutcomeNotice`) |
| P5 privacy | Fixed: progress/record only for the observer; others appear as anonymous `OtherWithdrawing`; `AcceptedCount` no longer projected | `src/OpenFantasy.Hosting/Simulation/Retreat/LocalRetreatView.cs` |
| P6 cost wording | Fixed: offer shows steps, tick-quantised walk time and exit recovery; detail shows per-step and total readiness; "No safe ground" only for ground blockers | `LocalRetreatView.cs` (`WalkingUnits`, `ExitRecovery`), `RetreatOffers.cs` |
| P7 public bytes | Fixed: `Retreat` omitted when null; `retained-auth` re-verifies the accepted cargo/trading/gift manifests on byte copies | `LocalWorldView.cs` |
| P8 docs/catch | Fixed: resolver no longer catches around record/disengage; docs updated (timing/actions exceptions, advisor, in-flight wording) | resolver; `docs/architecture/{encounter-retreat,encounter-timing,encounter-actions}.md` |
| Stale first wheel capture | Fixed: wheel opens on offers of the view read at opening; regression `RetreatWheelCaptureTests`; native first opening at the ready boundary is inspected once, never closed and retried | `client/scripts/FarmGame.cs` (`ChangeWheel`), `FarmClientSession.CaptureWheelView` |

## Required proofs and evidence (`qualification/`)

- **Durability** (`durability-20261001T161017Z-bd3cd`, 6 kills, all `exitCode=-9`): `RetreatDurabilityFixtureTests`
  replays an ordinary session journal through a fresh authority and exports closed saves before the queued retreat, a
  standing mid-route sample and the disengaging sample; `tools/check_retreat_durability.py` runs the existing
  `--probe-world-step-crash` seam before/after each, recovers with `--inspect-recorded-world`, retries uncommitted steps,
  refuses duplicates; `RetreatDurabilityResultTests` proves every recovered/retried save row-equal to the unbroken result
  (`WorldBackupComparison`: journal, receipts, identities, checkpoints, both archive families), prefixes equal to the
  unbroken session journal, full verified recovery and the original receipt on exact retry.
- **Checkpoint** (`RetreatCheckpointTests`): ordinary idling and Wait boundaries place the authority's own 6,000-step
  checkpoint inside the committed retreat (continuation mid-route in the checkpoint root); retained journal/receipt/
  identity/checkpoint rows before it are unchanged; checkpoint bytes equal the recorded root; full reopen reproduces the
  live root (debt, feet, body, HP, lots, resource history, retreat record); exact retry returns the original receipt.
- **Native** (`native-20261001T160906Z-3ee6b`, `native-auth-20261001T161003Z-7b98c`): keyboard, controller, pointer each
  open the wheel once at the first ready boundary with a current executable Retreat, cancel (nothing sent), commit,
  walk 16 presented samples, see the "broke away" notice; keyboard clears the wolf target, walks to Marla and buys one
  ration through the stall panel (2 rations from her shelf, exact quoted total); separate paused reopen matches feet,
  record, balance and rations. `NativeRetreatResultTests` replays every commit after the fixture prefix through the
  processor (each stored root reproduced), proves no player command between entry and the retreat, each sample one
  32-subunit step at the player's own boundary with ticks between, and the trade/lineage.
- **Contrasts** (`RetreatContrastTests`, marked counterfactual roots): lost locomotion at queue and mid-route, control
  loss mid-route (stale controller on the committed command), final-sample `ContactRemains`, held actor refused for
  Wait/Strike/second retreat, cancelled choice identity retained at disengagement, participants' ids/sides/feet/HP kept,
  undecided player holds the world while the NPC walks its real retreat (anonymous view), conflicting identity reuse
  refused while exact retry returns the receipt. Pure scope rule (ally, downed ally, third side): `EncounterRetreatContractTests`.
- **Codec** (`RetreatRootCodecTests`): V34 round trips, V33↔V34 refusal both ways, malformed retreat field, feet off
  the walked path and a mismatched queued choice surfacing as `FormatException`.

## Open obligations (not claimed)

- **Blocker — fresh native trading timing.** `legacy-auth` failed on the final build twice
  (`legacy-auth-20261001T160253Z-842b1` on build `154450Z`: 2722≠2723; `legacy-auth-20261001T162056Z-fca20`: 2716≠2717).
  The protected check expects the showing to be issued in an advancing batch (`before.AdvancingSteps + 1 + window`);
  in the fresh runs it lands one step earlier, in a non-advancing batch. The same authenticator passes on the accepted
  pre-retreat trading journal (`retained-auth`), and the earlier packet's legacy-auth passed. The most likely cause is
  my wheel-opening reorder in `client/scripts/FarmGame.cs` (`ChangeWheel` now reads/presents the current view before
  opening, and presents twice), which the protected trading probe also uses. Not diagnosed or fixed; Astra should
  treat the wheel change as unaccepted until this is resolved (a scoped fix is to keep the old order outside
  encounters, or find the hold this ordering introduces), then rerun the matrix.
- P1 trigger proof: no test constructs a placement that `PrepareMove` admits but the complete root refuses (dry
  envelope) — queue `Unavailable`, advisor skip and resolver `Unadmitted` for that case remain unproved by a test.
- An ally, downed ally or third side cannot be installed in an admitted profile-34 root (episode combatants must equal
  the authority-derived scenario), so `AllyRemains` mid-route and those queue refusals are proved only by the pure rule.
- A "decision still queued after the first sample" root is unrepresentable (timing refuses it); tested only as absent.
- Checkpoint only, not a coordinated material-care archive cut, lands inside the retreat.
- `Runtime/README.md` and the architecture index were not revisited in this request; only `encounter-retreat.md`,
  `encounter-timing.md` and `encounter-actions.md` were updated.
- Not claimed: party/joint retreat, pursuit, road Bandit Chief, multiplayer, lived-save migration, physical devices,
  OS focus, art or human acceptance, full P02.

## Failures retained

Unit runs before fixes (WithSide/scenario coupling, timing revisions, DB lock); one Core run (my 17→18 arithmetic);
native runs failed on the wolf target hiding market offers, an over-strict timing equality, a two-step frame hitch,
off-screen close button from longer text (texts shortened), and once (`native-20261001T160755Z-b9eca`) the wolf queued
its choice while the pointer's commit wheel was open so the capture correctly read as changed (rerun passed);
`legacy-auth-20261001T160253Z-842b1` failed one protected timing comparison (showing expiry vs. step, 2722≠2723);
three transient Godot `EditorSettings` import errors on build.
