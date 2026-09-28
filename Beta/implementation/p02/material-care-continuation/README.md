# Material and care continuation

This packet implements the coordinated continuation proposal in recoverable increments. The
[reviewed proposal](../restorative-recovery/MATERIAL_CARE_CONTINUATION.md) retains the full scope and
mandatory qualification. A standalone material capture does not complete that protocol.

## Exact material basis

`ProductionContinuationBasis.Capture` derives immutable live lots, exact remaining produced-output
budgets, direct raw-retirement coverage and nonrecursive producer metadata from supplied inventory
and complete production owners. Definition pins, custody, protections, reservations and absolute
revisions survive. The encode-only observation is bounded and deterministic. It cannot restore
authority, prove source issuance or free any existing history capacity.

Only new Core types and tests are introduced; existing planners, codecs, profiles, storage and native
client behavior are unchanged. The [timeless reference](../../../Runtime/docs/architecture/material-continuation-basis.md)
and its consumption/archive/index links are maintained in this same change. The existing project and
GameDev ecosystem documentation maintenance hook already requires that discipline; no competing hook
or specification is introduced.

### Why this representation

- Surviving lots retain actual selection restrictions. Per-output interval unions preserve provenance
  without treating an original recipe's already-spent outputs as available again.
- Raw retirements merge only exact source/item/custodian adjacency. Transformed ancestry is never
  expanded into another raw retirement. Historical holes and custody boundaries are not discarded.
- Producer witnesses identify the immediate producers of surviving outputs without holding ancestors
  or complete owners. A batch hash alone cannot satisfy construction's full ingredient proof.
- The 4,096 live-lot bound does not bound historical retirement fragmentation. Capture has a separate
  16,640-range envelope and 8 MiB encoding envelope. Future continued coverage must address its own
  cumulative fragmentation explicitly; this change makes no unlimited lifetime or scale claim.

### Qualification

The [final verification](qualification/verification.json) records a clean solution build and **235
passing affected Core cases**, including all **11 new basis cases**, with zero failures/skips.
The affected selection covers production, consumption, custody and terrain-material composition;
it is not a full Core/Hosting regression. The first focused 11-case run and earlier 235-case run
also pass. A final exact issuance-rejection assertion motivated the second build/affected run;
both initial reports remain retained. No acceptance threshold was changed.

New fixtures use actual crafting, consumption and gathering for claimed journeys. The 65-batch
craft/recraft chain retains two live producers without growing the observation's encoded length.
The 4,096-lot diagnostic fits the declared byte envelope. Deliberately constructed diagnostic
partitions and counterfeit issuance proposals are labeled separately and establish no earned
gameplay history. Source/configuration/fixture pins and final execution pins remain unchanged across
the final run. Architecture relative file links resolve; anchor validation is outside that check.

The [independent review](REVIEW.md) identified a class-equality assertion and missing internal-hole
coverage; both were corrected before execution. No blocking source finding remains. No build or
executed-test failure occurred in this increment. Its checks do not prove authenticated archive
restoration, continuing world behavior, production performance or removal of retained-history caps.

Native rendering and process-kill probes belong to the eventual changed world/Host binding; running
them against an unused capture type would not prove that integration. All full acceptance gates and
the proposal's >256-operation/two-cut/long-ancestry proofs remain mandatory and open.

### Reproduction

From the repository root, use the pinned local .NET 10.0.401 executable to build
`Beta/Runtime/OpenFantasy.sln --no-restore -m:1 -p:UseSharedCompilation=false`. Then run
`dotnet test Beta/Runtime/tests/OpenFantasy.Core.Tests/OpenFantasy.Core.Tests.csproj --no-build --no-restore`
with filter `FullyQualifiedName~Production|FullyQualifiedName~Consumption|FullyQualifiedName~Custody|FullyQualifiedName~TerrainMaterial`.
The narrower filter is `FullyQualifiedName~ProductionContinuationBasisTests`. Quote filters in the
shell and use a fresh results directory to retain these reports. The test runner requires local
socket permission; no Godot window, existing save, network service or external agent is needed.

### Preservation and ownership

Base: `427743907159ad75ed6a46623e823cc800bdba8a` on `codex/runtime-foundation`.
Pre-change checkpoint: `codex/checkpoint-before-material-frontier-20260928T035421Z`.
[The inventory](qualification/preservation-before.json) pins all 23 frozen specifications, 62 prior
milestone refs and the unrelated 19-file predecessor staging. No predecessor work belongs to this
increment. The integration lead owns commits and preserved milestone refs. Verified component:
`codex/milestone-material-continuation-basis`. This milestone does not close the coordinated protocol.

Two native workers have disjoint ownership of new Core types and new test files; a separate native
critic reviews the coherent result. These are not Claude runs. Prior external-route friction remains
reported in the shared handoff; no automatic paid retry or credit-saving claim is made.

## Remaining complete protocol

1. Introduce explicitly versioned production/care continuation owners with exact imported-right
   accounting, cumulative counters and old V1/V2/profile25/26 compatibility. Derive any accepted
   imported basis from a verified complete boundary, never arbitrary supplied observation bytes.
2. Integrate crafting, consumption, selection, permanent identity lookup and direct retirement with
   exact archived output budgets. Preserve reserved/installed output and full construction witnesses.
3. Bind excavation/terrain and all nested runoff/capacity/reservoir/transport owners and codecs to
   the same closure. Preserve actual issuance, pending obligations and source-custodian exclusions.
4. Close completed care causes, consumed material and resource effects together. Specify preemptive
   headroom scheduling, material-only cuts and complete rollback without stranded acknowledgments,
   hidden retries, extra game time or changed captured-command meanings.
5. Add atomic Host storage/retrieval/replay with actual archive bytes and permanent receipts/identities.
   Prove tamper refusal and actual external process interruption at publication boundaries.
6. Demonstrate >256 actual doses and surviving crafted ancestry across two cuts, bounded resident
   state, all dependent owners, native post-cut use and separate-process reopen. Full journal lifetime,
   production scale/platform/multiplayer and human acceptance remain separate open obligations.
