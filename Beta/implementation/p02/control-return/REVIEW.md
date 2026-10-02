# Control-return review and retained failures

The prior goal turn is verified progress: `68fa9fc5` qualified care durability and its actual HUD repair.
The clean successor was preserved before this unit by the checkpoint in `PRESERVATION.json`.
Unrelated predecessor staging remains outside the unit.

## Reproduced defect

The original actual host/protocol test queued paid work, disconnected, rejoined while that work held
control, sent early true, resolved the safe boundary and deliberately sent true again. The last request
was suppressed by the client's cached true even though the host had discarded the first. The final
Ready assertion failed against unchanged runtime. `RED_REPRODUCTION.json` pins that original source
and failed report. The repaired test keeps the actual journey and uses the new explicit Resume API;
merely replaying an early request is not the selected fix.

## Independent critique

A bounded read-only native critic identified the needed distinction between canonical Human assignment,
transient authority availability and explicit player readiness. The critic also called out the view
cache/publication ordering: a port transition must publish even when view bytes do not change. The
server now captures port status beside the view and forces a complete observation before its marker.
The first restored proof stays fixed while other players advance; raw true never regains a bypass on
a returning port. A later review found no actionable production defect in the coherent changes.

The Hosting worker's focused API review found a presentation race: the native pass read its view first
and could read a newer restored marker later. The lead changed the pass to capture control before view,
carry that value through presentation and consume the displayed value for explicit Resume. The critic
reviewed the correction. Input held during pending control is blocked until release; focus return is
not consent. This is source review, not physical-device or human UX acceptance.

The initial focused tests did not advance another actor after capturing the marker. Both protocol flows
now earn an actual later tactical cutoff under opponent readiness and apply the resulting newer view
before resuming with the original marker. Wrong port, actor, stream, revision and generation are also
refused. This closes that narrow proof gap without requiring latest-view readiness or changing any world law.

## Execution findings

The first combined build retained two new-test compilation mistakes: a missing Host namespace import
in the native coordinator and a nullable annotation in the hostile test. They were corrected without
changing assertions. The first seven-case protocol run passed the reproduced-behavior repair and all
wire assertions, then the raw test failed during explicit cancellation cleanup. Its cleanup now catches
OperationCanceledException only for its own cancellation of a previously running task; an already
completed failure still propagates. The original build and test failures remain in `qualification/`.

The final source review covers actual hostile preparation and continuation: one unrelated observer is
admitted human in fresh genesis, the first wolf remains hostile and deals real damage, original stats
and finite stocks are unchanged, and all movement, choices, timers and controller events are earned.
Independent original-genesis replay authenticates the exported native pending route; the native adapter
receives participant views and controls, never a mutable world or direct writer.

No external provider request or model substitution occurred after the recorded spending limit.
Disjoint native workers and critique consume Astra capacity; no external-credit savings is claimed.

## Native inspection and editor import

All three control-return captures were inspected: pending work has a disabled Awaiting control button,
the restored view retains the local pause and explains deliberate Resume, and the resumed view retains
the complete visible character and controls. The four-player roster captures also preserve the hold,
later resolution and completion. Crowded world-space labels remain a polish issue; these observations
are not art or human-usability acceptance.

Two unchanged headless import invocations failed the strict log check with the engine diagnostic
`EditorSettings not instantiated yet` for `export/android/android_sdk_path`. Both failures remain
retained. Godot's [upstream fix](https://github.com/godotengine/godot/pull/116515) describes a matching
editor-settings initialization race in Android device polling, and the pinned 4.6 source calls that
setting from its poller without the later guard. This supports an engine-ordering explanation; no
project resource failure was printed. It does not justify discarding either failed check.

The wrapper now gives the editor 120 iterations after `--import`, retaining resource-import completion,
the same 180-second limit, zero exit status and refusal of every `ERROR:` line. The original wrapper is
preserved in `originals/native.py`. This adjusts initialization time within the existing deadline;
it neither suppresses the diagnostic nor changes the Godot executable, project, runtime or native
assertions. A clean result qualifies only that actual invocation, not a general cure for engine races.
