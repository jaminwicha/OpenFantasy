# Coordinated native continuation: result

Status: **partial — the native fixture is blocked by an actual world rule.** The selection route,
client startup route, runner journey and managed authentication are implemented and build cleanly.
The specified fixture cannot be prepared: the world refuses the helper-controlled transfer. No fixture
was exported, so nothing here is rendered, native, independent, full-phase or production evidence.

Base `4009ee4`, checkout `codex/runtime-foundation`. No Git writes, no subdelegation, no Godot runs.

## Blocker (requirement 3)

The player's permission withdrawal is accepted first, as required. The following helper-controlled
`GiveItemIntent` (helper → player, one raw Potion, quantity 1) is then refused: `NotAuthorized`.
Recorded by `worker-tests-02`:

- source (helper storage) policy: owner = helper, `DepositRule = AnyControlledActor`
- destination (player storage) policy: owner = player, `DepositRule = OwnerOnly`

`CountedAuthorityState.AllowsTransfer` requires the destination to admit the giver. The existing native
fixture demonstrates only the opposite direction (player → helper). All 600 Potions of this ledger were
admitted to the helper, so the player holds none. I did not edit policy, stock or Core, and did not
substitute another route. Preparation previews the transfer and stops before it enters any ledger.

Decision needed from Astra, any of which is outside this assignment's stated rules:

1. Accept player-earned medicine instead: actual gathering of three berries and an accepted craft
   (as `RestorativeNativeFixtureExportTests.CraftPlayerDose`). The dose is then `Transformed`, not a
   transferred raw unit; metadata and `Authenticate` expectations for ingredient kind/source change.
2. Re-earn a long ledger from a fresh genesis whose player storage admits deposits or holds a Potion.
3. Add an accepted transfer mechanic (Core/policy change).

## Owned files

Changed:
- `Beta/Runtime/src/OpenFantasy.Hosting/Worlds/SqliteWorldAuthority.cs` — public
  `OpenExisting(string, ReadOnlySpan<byte> expectedGenesis)`.
- `Beta/Runtime/client/scripts/FarmClientSession.cs` — `OpenCoordinatedFixtureAsync`; companion selection
  by the player's care permission (this world has two quest givers, which the quest rule cannot resolve).
- `Beta/Runtime/client/scripts/FarmGame.cs` — `CoordinatedFixtureGenesisPath`.
- `Beta/Runtime/client/scripts/Bootstrap.cs` — `--coordinated-restorative-probe` (`keyboard`, `reopen`).
- `Beta/Runtime/tools/check_native_restorative.py` — `coordinated-keyboard` journey.

Created:
- `Beta/Runtime/tests/OpenFantasy.Hosting.Tests/Care/CoordinatedNativeSelectionTests.cs`
- `Beta/Runtime/tests/OpenFantasy.Hosting.Tests/Care/CoordinatedNativeFixtureTests.cs`
- `Beta/Runtime/tests/OpenFantasy.Hosting.Tests/Care/CoordinatedNativeResultTests.cs`
- `Beta/Runtime/tests/OpenFantasy.Hosting.Tests/Care/CoordinatedNativeJourney.cs`
- `Beta/implementation/p02/coordinated-native/RESULT.md`

Unchanged: `Beta/Runtime/client/scripts/RestorativePlayProbe.cs`. Its controls, checks, 30 s opening,
150 s total and the runner's 180 s process bound are untouched.

## Decisions

- The overload validates the expected root with the existing internal `WorldGenesisAdmission`, requires
  its exact canonical encoding, refuses an absent file, recovers once, then compares both the recovered
  genesis hash and the stored genesis bytes; the database is disposed on any failure. It is
  profile-neutral. The existing `OpenExisting(string)` is unchanged.
- Profile 27, commit/step zero, existing save and scope/admission are required by the client route.
- `default all` keeps its five routes. The coordinated journey additionally pins `genesis.ofc`, four
  startup sources, `SqliteWorldAuthority.cs` and Godot/dotnet/project execution inputs.
- Continued frames are never decoded standalone: authentication compares encodings of states read
  from the reopened world. Only the fresh genesis is decoded.
- Opt-in cases follow the existing convention and return without asserting when their variable is
  absent. Such a pass is not evidence.

## Runs

| Label | Result |
| --- | --- |
| `worker-build-01` | **failed**: 6 compile errors, all in new test files |
| `worker-build-02` | passed, 0 warnings, 0 errors |
| `worker-tests-01` | **failed**: 8 passed, 3 failed, 0 skipped of 11 |
| `worker-build-03` | passed, 0 warnings, 0 errors (adds the refusal diagnostic) |
| `worker-tests-02` | **failed**: 8 passed, 3 failed, 0 skipped of 11 (28 s) |

The 8 passes are 7 selection cases on small roots (expected genesis opens its save; missing save;
two wrong-genesis variants; lived root; wrong profile; unchanged profile-26 opening) and 1
environment-absent result case that asserted nothing. The 3 failures are all fixture cases, from the
single refusal above. All reports and logs are retained in `qualification/`.

## Gaps

- No `before.sqlite3`, `genesis.ofc`, `before.ofc`, metadata, receipts or expected result were exported.
- `CoordinatedNativeJourney.Authenticate`, the managed rehearsal and the corrupted-prior-archive refusal
  compile but have **never executed**.
- The client route is verified on small profile-27 roots only. It has not opened the long ledger, so its
  public projection/offers there and the 30 s opening bound are unmeasured.
- Bootstrap/FarmGame changes are compile-verified only. The Python journey was not executed.
- The post-run source-hash assertion did not execute. The source directory is only copied and hashed.
- The new public overload is an interface addition for the persistence reference (Astra owns docs).

## Friction

- One Bash call was denied and not retried; the same files were read with Read/Grep:
  `cd /Users/alucinor/Workspace/Projects/OpenFantasy/Beta/Runtime && rg -n "public static .*Decode|internal static .*Decode|public static .*Encode" src/OpenFantasy.Core/Worlds/WorldSnapshotCodec*.cs | head -30 && rg -n "Decode\(" tests/OpenFantasy.Hosting.Tests/Care/CoordinatedWorldContinuationTests.cs tests/OpenFantasy.Hosting.Tests/Care/CoordinatedLiveWriterTests.cs tests/OpenFantasy.Hosting.Tests/Care/CoordinatedIntegrationRegressionTests.cs | head -30 && cat tests/OpenFantasy.Hosting.Tests/Care/OrdinaryCareWorldFixture.cs && cat src/OpenFantasy.Hosting/Simulation/LocalRestorativeController.cs`
- An earlier read-only `cd … &&` call moved the shell into `Beta/Runtime`. I returned with
  `cd /Users/alucinor/Workspace/Projects/OpenFantasy` before the runner commands, which were used exactly.
- The research archive was not read. Only the first 723 lines of the handoff were read.

## Reproduction

From the repository root, incrementing the suffix:

```
python3 Beta/implementation/p02/coordinated-native/qualification/run_worker_checks.py build worker-build-04
python3 Beta/implementation/p02/coordinated-native/qualification/run_worker_checks.py test worker-tests-03
```
