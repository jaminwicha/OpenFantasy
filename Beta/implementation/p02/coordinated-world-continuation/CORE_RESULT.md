# Core result: coordinated world continuation

## Status: partial, unbuilt and untested

Source was written but **never compiled and never run**. No test result exists. Treat the tree as
possibly not compiling until the targeted build is run. Nothing here closes a gate.

## Blocker: permitted shell commands denied

Tool `Bash`, both attempts denied with "Permission to use Bash has been denied because Claude Code is
running in don't ask mode":

1. The permitted build with `2>&1 | tail -n 40` appended (my deviation from the permitted form).
2. The exact permitted command, no pipe or redirect:
   `/Users/alucinor/.local/share/openfantasy/toolchains/dotnet-10.0.401/dotnet build /Users/alucinor/Workspace/Projects/OpenFantasy/Beta/Runtime/tests/OpenFantasy.Hosting.Tests/OpenFantasy.Hosting.Tests.csproj --no-restore -m:1 -p:UseSharedCompilation=false --verbosity quiet`

I stopped there. No other command, tool or workaround was tried. `dotnet test` was never attempted.
File reads and the owned writes/edits were permitted.

## Tests actually run

None. Builds: 0. Test cases executed: 0. Original failures: only the two denials above.
`CoordinatedWorldContinuationTests.cs` (5 cases) is unexecuted; if it blocks compilation, exclude it.

## Written behavior (unverified)

- Fresh profile-27 opt-in from a revision-0 profile-26 genesis; genesis source baseline captured.
- Exact source partition on every profile-27 admission: archived + active direct raw retirement +
  live raw lots must be disjoint and equal genesis allocations + gathering cursor advance + completed
  extraction. Capacity is only a bound.
- Joint cut at unchanged revision/time: Production recaptured with original construction batches,
  reservoir capacity and liquid cells rebound, care closed or retained, new resource baseline.
- Trigger reserves the whole batch plus one future operation in each window.
- Codecs: world V27, Production V9, resource-history V3 frame, step V27.

## Changed paths

New, `Beta/Runtime/src/OpenFantasy.Core/`: `Production/ProductionCodecV9.cs`;
`Worlds/WorldStepCodecV27.cs`, `WorldMaterialCareArchiveReference.cs`, `WorldMaterialCareArchiveAnchor.cs`,
`WorldMaterialCareArchiveSegment.cs`, `WorldMaterialCareContinuation.cs`, `WorldCoordinatedContinuation.cs`,
`WorldCoordinatedAdmission.cs`, `WorldSourceIssuanceBaseline.cs`, `WorldSnapshotCodecV27.cs`.

Edited, same root: `Production/ProductionCodecV3.cs`, `ProductionSnapshot.cs`,
`Production/Continuation/ProductionContinuationContext.cs`, `ProductionContinuationValidation.cs`;
`Care/RestorativeItemPlanner.cs`; `Construction/ConstructionPlanner.cs`, `ConstructionSnapshot.cs`;
`Terrain/ExcavationPlanner.cs`, `ExcavationSnapshot.cs`; `Geography/GeographyProductionCodec.cs`,
`GeographyProductionDependencies.cs`; `Worlds/WorldSnapshot.cs`, `WorldSnapshotCodec.cs`, `WorldStepCodec.cs`,
`WorldStepBatch.cs`, `WorldStepFrameCodec.cs`, `WorldStepDecision.cs`, `WorldStepProcessor.cs`,
`WorldResourceContinuation.cs`, `WorldResourceHistory.cs`, `WorldResourceHistoryCodec.cs`,
`WorldResourceAdmission.cs`, `WorldCareAdmission.cs`, `WorldRestorativeAdmission.cs`,
`WorldRestorativeResolver.cs`, `WorldRestorativeActions.cs`, `WorldCareProcessor.cs`.

New test: `Beta/Runtime/tests/OpenFantasy.Hosting.Tests/Care/CoordinatedWorldContinuationTests.cs`.
Packet: `CORE_CONTRACT.md`, `CORE_RESULT.md`. No existing test, Hosting, Godot, project, doc or Git change.

## Known risks to check first

- Existing behavior touched: `ProductionCodecV3` was split into shared frame helpers;
  `ProductionContinuationContext` constructor checks were rewritten; legacy care callers now pass an
  identity lookup only when Production is continued. Retained bytes and tests must be rerun.
- Geography V3 frames nest Production V9 for an owner cut by the coordinated world; only the
  dependency constructor is gated, not `LiquidReservoirCodecV3.Encode`.
- `WorldResourceHistoryCodecV1.Encode` was not read or guarded against dropping a coordinated anchor.
- Assumed, unverified: inventory admission hash is stable across ordinary actions; gathering issues
  contiguously from its cursor; `WorldFrameReader` accepts the baseline's field count.
- The cut root passes no previous identity index.

## Remaining work

Everything in the acceptance matrix: build, all tests, >256 real uses and recraft ancestry across two
cuts, pending care and grants/revocations, partial wet mining and construction, quest funding and
backing, multi-actor batches with later refusal, source-coverage/alias/stale-donor adversarial cases,
and legacy byte reproduction. Profile-27 material evidence is not bound. Core behavior, authenticated
Host retrieval (schema 8), process-kill recovery, native UI and human acceptance are all unproven.

Budget: about $16 of the $20 estimate used; no retry, no nested delegation, no network.
