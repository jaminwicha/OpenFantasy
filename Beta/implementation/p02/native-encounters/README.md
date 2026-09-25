# Native encounter checkpoint

Pure-C# profile24/schema6 encounters now connect to the actual Godot client. The explicit
`python3 tools/play.py --encounters` route opens a separate retained world. Select a wolf, inspect an
entry, choose Strike or Wait through contextual controls, resolve the source-backed battle, and return
to exploration through the same durable authority. Normal farm saves and launch selection are preserved.

The [contract](CONTRACT.md) defines scope and required evidence; the
[architecture reference](../../../Runtime/docs/architecture/native-encounters.md) explains ownership.
The [review](REVIEW.md) retains source findings, native failures, diagnosis and visual corrections.

## Behavior

- Host projections expose the player's own grants, captured timing, actual costs and authoritative
  availability. Client offers preserve their scope/intent, and a stale wheel choice needs a new
  deliberate selection. Ordinary actions cannot bypass encounter phase, incapacity or retained recovery.
- Q/right shoulder selects a target. E/A engages, queues Strike or continues from outcome according
  to context. G/X queues Wait in combat. V/right-stick click opens the icon wheel; I/View inspects the
  pack. Neither inspection nor cancelling a draft spends a choice. Explicit pause/focus policy remains.
- Required decisions hold world time, while the session remains ready to process a confirmed choice.
  Applied attack receipts mean queued. Visible HP loss drives a small hit reaction and damage number.
  In-place transitions do not teleport residents, and wounded/incapacitated actors retain their condition.
- The original wolf sheet is copied byte-exactly, with provenance. The selected wolf receives a name
  cue; ordinary excavation summaries appear in their relevant context. These assets remain provisional.

## Retained native journey

The disposable journey enters the east-wolf scenario, verifies required-choice holds and input blocking,
inspects/cancels Wait through focus changes, inspects the pack, confirms Strike through controller wheel
input, then pauses and closes. A second actual process recovers the exact mid-duel public state, continues
through keyboard choices, reaches wolf HP0/player HP2 using unchanged source stats, and releases using a
pointer click on Continue. A third process recovers the exact released public state and unchanged save.

The wrapper also reconstructs the exact pre-entry/confrontation/staging/active commits through four
actual Host calls and compares the complete identity-keyed actor positions and calendar. A live view
sampled earlier cannot substitute for that exact boundary. Rendered fixtures use a disposable startup
configuration, preserve shared project bytes, and record unfocusable state plus actual focus observations.

The successful integration report is
[native-rendered-startup.json](qualification/native-rendered-startup.json). Its eight frames received
independent review, which identified the subsequently corrected footer and wording issues. The final
presentation follow-up is recorded with the final qualification below; earlier failures remain intact.

## Final qualification

- [Final rendered journey](qualification/native-rendered-final.json): all three Godot processes pass,
  with exact entry boundaries, mid-duel recovery, actual source outcome, pointer release and released
  recovery. Native focus remains false throughout; the shared project configuration remains unchanged.
  Eight final frames are retained under `qualification/native-encounter-scbztlwa/`. The independent
  follow-up closes the footer clearance and paused/outcome wording findings; this is functional UI
  review, not final art acceptance.
- [Managed summary](qualification/test-summary.json): 645 Hosting/client cases pass, zero failures or
  skips, including the 14 new cases also run as a focused check. These counts are not additive. The
  solution and final Godot-only follow-up builds have zero warnings/errors. Core source is unchanged;
  this increment does not claim a new full Core regression.
- [Original farm control](qualification/native-farm-control.json) and
  [original wheel control](qualification/native-wheel-control.json) pass headlessly with their existing
  assertions. These controls qualify injected input and persistence, not physical devices or OS focus.

Disposable render launch configurations remain in evidence. Their temporary resource symlinks were
removed after the processes exited; the wrapper recreates them on another run. No resource targets or
shared client settings were deleted or changed by this cleanup.

## Remaining obligations

The source profile currently grants one player attack. General ability browsing/targeting remains open.
Confirmed native Wait and defeated-player behavior were still pending at this checkpoint; the later
[choice qualification](../encounter-choice-validation/README.md) supplies those actual native journeys.
That follow-up proves incapacity and retention, not an implemented defeat continuation.
Flee/rescue, healing/death/rewards, richer animation/audio, independent co-op, wet/structural dependency
closure, scale, physical input, accessibility, approved art, desktop exports and all full campaign/phase
gates remain open. An incapacitated initiating player cannot currently release; the UI says so.

The ecosystem focus/input lesson is preserved by `ffd9bf7`, with three installed skill entrypoints
hash-verified and rollback copies retained. No external model was invoked for this increment.
