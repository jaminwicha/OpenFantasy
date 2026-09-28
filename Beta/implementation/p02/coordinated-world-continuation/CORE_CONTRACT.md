# Coordinated world continuation: Core contract

Owner: Core worker (`fable[1m]`, Max). **Verification status: the source below exists in the tree but
has never been compiled or executed.** Both permitted shell commands were denied to this worker (see
`CORE_RESULT.md`). Every signature is what the source file declares, not a compiler-confirmed API.
World 27, Production V9 and resource-history V3 therefore remain proposed.

## Design as written

One fresh explicit complete-world interpretation rooted in actual profile 26. The existing world
journal stays the only action log. No second Production operation counter and no
`MaterialOperationHistory` exist for full-world transfers, gathering or work.

| Concern | Decision in source |
| --- | --- |
| Opt-in | Only at a fresh revision-0 profile-26 genesis. Lived worlds are refused. |
| Owner per fact | Raw lots: `CountedAuthorityState`. Transformed lots, crafts, claims: `ProductionSnapshot`. Care records: `CareSnapshot`. Effects: `WorldResourceHistory`. `WorldCoordinatedContinuation` holds only the source baseline and the latest anchor. |
| Production revision | Craft and consumption only. Custody and storage revisions are lower bounds in Production; their exact causes belong to Host replay. |
| Closing identities | `ClosingWorldSequence` (world revision) and `LatestCareSequence` (care frontier) are distinct anchor fields. |
| Causal baseline | Every cut starts a new resource segment bound to the joint anchor (resource-history V3 frame). |
| Cut scope | Every cut is joint. Production is recaptured; care is closed when it progressed, else keeps its context; resources are re-based even when empty. |
| Headroom | Whole next batch plus one further operation (two resource effects) per window. |
| Ancestry | The anchor keeps no closing root and no parent anchor. |

## Public Core API (as declared in source)

```csharp
public static class WorldCoordinatedAdmission
{ public static WorldSnapshot CreateFresh(WorldSnapshot ordinaryCareGenesis); }

public sealed class WorldCoordinatedContinuation
{ WorldSourceIssuanceBaseline Sources; WorldMaterialCareArchiveAnchor? Anchor; }

public sealed class WorldSourceIssuanceBaseline
{ Allocations; Cursors; ExtractedSources; ContentHash; byte[] Encode(); static Decode(ReadOnlySpan<byte>); }

public sealed record WorldMaterialCareArchiveReference(worldId, branchId, ordinal, boundarySequence, boundaryHash);

public sealed class WorldMaterialCareArchiveAnchor
{ Reference; AdmissionVersion; ClosingWorldSequence; LatestCareSequence; Production; Care; Progression;
  SourceBaselineHash; ContentHash; }

public sealed class WorldMaterialCareArchiveSegment
{ Reference; Anchor; WorldSnapshot Continued; static Seal(WorldSnapshot boundary); byte[] Encode();
  static Decode(WorldMaterialCareArchiveReference expected, ReadOnlySpan<byte> bytes,
      WorldMaterialCareArchiveAnchor? previousAnchor = null); }
```

Additions to existing types: `WorldSnapshot.Coordination` and a trailing optional constructor
parameter; `WorldStepDecision.SealedMaterialCareSegment`; `WorldResourceHistory.CoordinatedAnchor`;
`WorldSnapshotCodec.DecodeCoordinated(bytes, anchor?)`; `WorldResourceHistoryCodec.DecodeCoordinated`;
`WorldSnapshotCodecV27`; `WorldStepCodecV27`; `ProductionCodecV9`;
`ProductionContinuationContext.SupportsCompleteWorld`;
`RestorativeItemPlanner.Plan(..., Func<EntityId,bool>? isIdentityOccupied = null)`.

Host integration points: call `WorldStepProcessor.Decide` with the permanent identity lookup, archive
`decision.SealedMaterialCareSegment.Encode()` under its `Reference` atomically with the batch, and on
recovery walk segments with `Decode(reference, bytes, previousAnchor)`, keeping only the latest anchor.

## Restrictions kept in source

`ProductionCodecV3.Encode`, the public `Continue*` cuts and the public `GeographyProductionDependencies`
constructors refuse the coordinated context. `WorldSnapshotCodec.Decode` refuses a cut profile-27 world
and refuses a resource-only anchor for profile 27. Profiles 1–26 keep their encoders and entrypoints.
Material-evidence observation stays bound to profile 26.
