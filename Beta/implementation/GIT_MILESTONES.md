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

The next planned checkpoint is the action-preview increment after its implementation,
authority-boundary review and native verification. Add its exact branch and commit only when
that evidence exists.
