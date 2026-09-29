# Scheduled material proposals — implementation notes

Worker: Claude Fable 5.1 (`claude-fable-5-1`), single run, from base
`d987bbdb6aff9cb1e43f01a5d79eaafd539eea7b`. Astra integrates and reviews.

## Edit-access proof

This file is the first owned write of the run, made before any source analysis. It was created with
the native Write tool at the packet path named in `ASSIGNMENT.md`; no shell redirection was used.

## Working rules taken from the packet

- Owned files only: the six named Hosting simulation files, new `LocalMaterial*.cs`, new
  `ScheduledMaterialProposal*.cs` tests, the three architecture pages, this file and
  `WORKER_RESULT.md`.
- No Core, client/asset, schema, old test/fixture or qualification-script edits. No Git mutation.
- Only the three exact `run_checks.py` shapes (build, focused, regression), run sequentially from the
  repository root, with no source edit while a check runs.
- Architecture snapshots in the private task directory are read-only pre-change context.
- The 19 unrelated staged predecessor files and other workers' edits are preserved untouched.

## Plan (to be refined as analysis proceeds)

1. Read shared instructions, the current handoff, the predecessor integration review, the custody
   integration plan and the affected reference pages.
2. Map the existing session queue/cutoff path and the proposal service before choosing a binding.
3. Bind the four player proposal intents to `LocalWorldSession.SubmitAction`.
4. Add the participant-only proposal projection to `LocalWorldView`.
5. Add the explicit immutable Host NPC gift policy and stable semantic command identities.
6. Enable the ordinary restorative route for the admitted profile.
7. Write `ScheduledMaterialProposal*` tests, then build, focused and regression checks.
8. Update the three architecture pages and write `WORKER_RESULT.md`.

## Decisions and findings

(recorded below as they are made)
