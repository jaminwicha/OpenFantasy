# Commit and milestone doctrine

Owner direction, 2026-09-10: commit implementation progress along the way and create milestone
branches to record major progress. This is shared project discipline for all authorized agents.

- Commit coherent completed increments with the relevant checks and a useful description.
  Do not wait until a large campaign is finished to preserve working progress.
- At a reviewed vertical slice, major integration or other meaningful accepted checkpoint,
  create a `codex/milestone-<descriptive-name>` branch at the exact qualified commit. Record
  the commit, scope, evidence and remaining limits here. This names an existing commit; it
  does not switch the working checkout or include uncommitted work.
- Keep milestone branches as historical markers. Do not advance or force-move an existing
  milestone to a later state; create a new descriptive milestone when progress warrants it.
- Continue daily work on the active development branch. Milestone creation does not require
  pushing, merging or publishing. Those actions follow their own authorization and scope.
- The integration lead owns commits and milestone creation when workers share a checkout.
  Workers return bounded edits/evidence; preserve unrelated staged files and never stage all.
- Passing a bounded checkpoint does not close a phase, production gate, art acceptance or
  human playtest requirement beyond the evidence recorded for that checkpoint.

## Recorded milestones

| Branch | Commit | Qualified scope and limits |
|---|---|---|
| `codex/milestone-farm-input-foundation` | `b1482db` | Connected farm/gather/craft/quest/NPC construction and client input foundation; 825 tests and retained native journeys. [Evidence](p01/input-focus/README.md). Provisional art, general UI, physical device/export qualification and full phase exits remain open. |
| `codex/milestone-action-previews` | `b40f28c` | Captured farm action offers, visible costs and read-only inspection; code `3ae7855`, 832 tests, all retained native journeys and rendered review. [Evidence](p01/action-offers/README.md). Full wheels/management, physical devices, art acceptance and phase exits remain open. |
| `codex/milestone-explicit-targeting` | `747925f` | Explicit Wren/harvestable selection, stable target identity and incompatible-verb blocking; code `c7e29f5`, 838 tests, all retained native journeys and eight rendered states. [Evidence](p01/target-selection/README.md). General pointer targeting/confirmation, full UI, physical devices, art and phase exits remain open. |
| `codex/milestone-inventory-inspection` | `ea56b64` | Native own-pack inspection, host timing display and scoped input; code `3eed739`, 846 tests, all final native journeys and twelve rendered states. [Evidence](p02/inventory-inspection/README.md) retains an earlier travel timeout with unconfirmed cause. Equipment/use/transfer, icons, full UI, OS/physical-device acceptance and phase/feasibility gates remain open. |
| `codex/milestone-recorded-history` | `9e0018d` | Exact recorded-commit reconstruction and operator; code `913a5f2`, corrected stationary quest probe `1d26210`, 855 final tests and combined native evidence. [Evidence](p01/recorded-history/README.md) preserves failed runs, confirmed stale Talk revision and unconfirmed editor-startup error. Arbitrary interior time, prehistory, branch travel, production latency, independent review and full phase gates remain open. |
| `codex/milestone-daylight` | `80bdda0` | Calendar-driven outdoor light and bright target/UI cues; 855 tests, complete integrated regression plus probe-only night-interface followups, ten daylight and twelve daytime UI captures. [Evidence](p02/daylight/README.md) retains the corrected unmapped probe input and recurring, unconfirmed editor-settings error. Weather/seasons, rest/camp, production art/audio, independent/human review and all full gates remain open. |
| `codex/milestone-quick-wheel` | `c8989ae` | Native four-action wheel, pinned inspection/confirmation, controlled background rendering and removal of legacy fixture refocus loops; 861 tests, separate complete native qualification, four affected fixture followups and sixteen rendered frames. [Evidence](p02/shared-desktop-validation/README.md) retains the failed integrated editor startup and precise source/binary boundaries. This is combined qualification, not one uninterrupted verifier pass. Full UI, OS/devices, co-op, art, independent/human reviews and phase gates remain open. |
| `codex/milestone-watershed-persistence` | `bf96ed9` | Explicit genesis-admitted watershed in the composed-world authority; 883 tests, real before/after-commit process kills, exact history/restart, independent outer archive/pin checks and native foundation/farm regression. [Evidence](p01/watershed-admission/README.md). Default farm terrain is unchanged; macro-to-local realization, basin composition, fluid custody, migration, production performance and all full gates remain open. |
| `codex/milestone-architecture-reference` | `2f91983` | Timeless architecture reference, source-binding backfill and upkeep hook. [Review/checks](architecture-reference/README.md) cover 123 reference links, bounded factual critique, frozen-spec/index preservation and five synchronized skill entrypoints (ecosystem `4e83086`). Documentation checkpoint only; no gameplay or phase acceptance is inferred. |
| `codex/milestone-watershed-networks` | `23851fa` | Finite basin composition, canonical archive, exact contributor propagation and shared faces; 904 tests, five independent archives, 23 real host inspections and bounded critic review. [Evidence](p01/watershed-network/README.md) preserves the reproduced scope-alias defect and correction. No live-world/network admission, fluid custody, local realization, performance or full phase acceptance is inferred. |
| `codex/milestone-watershed-network-persistence` | `b2176e5` | Complete finite network retained in explicit fresh profile-seven world; 919 tests, 74 existing inspections, 27 real operator calls, both process-kill boundaries, exact history and headless foundation/farm regression. [Evidence](p01/network-admission/README.md) retains fixture failures and bounded critic coverage limits. Default farm, prior saves/encodings and all byte/recovery gates remain unchanged. No local realization, fluids, played-save migration, performance or full phase acceptance is inferred. |
| `codex/milestone-watershed-terrain-mapping` | `41865ed` | Retained dry macro-to-local mapping with exact integer scale, vertical windows and ancestral addresses; 936 tests, six independent archives, 27 real host inspections and bounded critic review. [Evidence](p01/terrain-mapping/README.md) retains compiler failures and scope limits. Separate candidate/archive only: no live terrain/material/fluid admission, visual change, performance or full phase acceptance. |

The inventory-inspection branch records bounded code `3eed739` and retained evidence at
`ea56b64981f9f5409248dc9bb728af08439db386`. The development checkout continues on
`codex/runtime-foundation`; the earlier milestones were preserved and none was checked out.
No push or merge was performed.

The recorded-history branch preserves code and evidence at `9e0018d`. Qualification combines
successful integration checks with targeted native followups and final tests on the rebuilt
binaries; it is not a single uninterrupted full-verifier pass. The development checkout and all
earlier milestone branches remain in place. No push, merge or phase closure accompanies this marker.

The daylight branch preserves source and evidence at `80bdda07398a8657b1b8c7034c83242ef8dc641b`.
The final native probe extension was qualified separately after the integrated pass, with identical
production sources and tested non-client binaries. The development checkout remains unchanged;
earlier milestone branches and the unrelated staged predecessor work were preserved.

The quick-wheel branch preserves code and evidence at `c8989aed1178bfd13f68bcf38b64be82f540f2b6`.
The earlier wheel packet remains its original partial checkpoint; the shared-desktop packet
records subsequent completion. No checkout switch, force update, push or merge accompanied the
marker. The recurring editor-startup error remains unresolved and retained as a failure.

The watershed-persistence branch preserves code and qualification at `bf96ed9baa076f80f6b5266383612fa5a1f466ce`.
It records one explicit new-world macro admission, not a completed frontier or geological simulation.
The development checkout and previous milestone branches remain unchanged. No push or merge occurred.

The watershed-network-persistence branch preserves code, architecture updates and qualification at
`b2176e5ddb29bbbf355035b217d523941a4d4cc9`. It records durable ownership of finite prepared networks, not a completed frontier
or geological simulation. The development checkout, earlier milestones, frozen specifications and
unrelated staged predecessor work remain intact. No checkout switch, force update, push or merge occurred.

The watershed-terrain-mapping branch preserves code, reference updates and bounded qualification at
`41865edfa17a1d8476946261007abbe0ec53f334`. The development checkout and all earlier markers remain in
place. No checkout switch, force update, push or merge accompanied this marker; unrelated staged
predecessor changes and frozen specifications remain intact.
