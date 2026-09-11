# Calendar-driven outdoor light

11 September 2026. This increment makes the existing authoritative calendar visible in the native
farm. It is a P02 presentation prerequisite toward RQ-094 and RQ-126, not completion of either
requirement or the P01/P02 exits. The frozen specification and all 181 requirements are unchanged.
Implementation and evidence commit: `80bdda0`, preserved as `codex/milestone-daylight`.

## Implemented behavior

The farm's `FarmDaylight` component samples a native editable Gradient using the permitted frame's
milliseconds within its day. It shares the same public projection as the terrain, actors and
clock display. There is no presentation clock, timer, random source, simulation command, save
format change or new core dependency. Historical/reopened instants therefore select the same
light without applying a client catch-up calculation. The actual native reopening check covers
the saved instant; playable historical travel remains unimplemented.

The provisional palette has warm dawn, full daylight, cooling dusk and restrained readable blue
night. Matching midnight endpoints avoid a discontinuity at day rollover. Colors are validated
as finite, opaque tint values. The resource and small scene are editable in Godot, separate from
the simulation and HUD. Actor and terrain colors respond together. HUD/pack CanvasLayers retain
their own brightness; the explicit target marker uses an unshaded material as an interface cue.

The existing cadence remains 1.8 world seconds per 50 ms exploration step: **40 minutes per
nominal full day**. Pauses, reading holds and host readiness keep their existing policy. This is
a calculation from the retained pacing constants, not a timed forty-minute human playtest or a
change to world speed. Existing saves retain their actual dates. Sleep, fatigue, camping and
time skips remain required later implementations.

## Native qualification

`check_native_daylight.py` runs six actual Godot processes against disposable gathering-profile
farm fixtures. Their initial times are explicitly chosen at 05:30, 12:00, 18:30, 23:00 and
23:59:51; the sixth process reopens the last save. These are dated source-farm fixtures, not
normal saves altered to accelerate a demonstration. The host then advances through dusk and
midnight at its ordinary rate. No normal player world is opened by the check.

Assertions compare complete paused public views, actual light colors, world/HUD/pack canvas
separation, palette anchors and midnight continuity. Real Escape input resumes and pauses the
host. Reopening must preserve the full public world, exact tint and database SHA-256. A missing
report, incomplete checks, nonzero exit or any Godot `ERROR:` fails qualification. The check is
included in the integrated verifier. Its rendered option retains ten native 960×540 captures,
including real Q target selection and I pack inspection in the night fixture. The pack must
close without changing the paused world or light.

The [integrated regression](qualification/final-verification.json) passed **544 Core + 311 Hosting
= 855 tests**, zero skips, all 22 independent fixtures, 68 host inspections and every retained
native journey, including the first six-process daylight check. Build/import and the runtime
naming scan passed; all 692 inputs remained unchanged during that run.

The subsequent small night-interface probe extension passed [headless](qualification/headless-final.json)
and [rendered](qualification/rendered-final.json) checks. Only the daylight probe and its runner
changed after the integrated pass; production sources stayed identical. The rebuild changed only
the client assembly among the six recorded binaries. Core, Hosting, Host and both tested assemblies
retained their SHA-256 values, so their suites were not needlessly rerun. The
[verification summary](qualification/verification-summary.json) records this exact evidence boundary
and the final input hashes; the expanded probe is not claimed to have run in the earlier full pass.

The existing [rendered action/target/pack journey](qualification/rendered-offers.json) also passed
all twelve checks and retained twelve captures. It starts at 09:00 and establishes that the daylight
integration preserves the existing daytime journey. The ten daylight captures were visually
reviewed along with its plant/person targeting, pack inspection and dialogue captures. These are
local native checks, not physical-controller, localization, scaling or human experience acceptance.

The rendered review is local to Astra. Terrain, people and the player's foot cue remain visible
at night; HUD text retains brightness. Dawn/dusk are deliberately subtle. The repetitive source
grass, simple sprites and large provisional HUD are still visible limitations, not approved art
or a polished experience. No independent-agent review or human style/device acceptance is claimed.

## Failures and review

The first build used `Color.IsFinite()`, which the pinned Godot C# API does not expose. Explicit
finite-channel checks corrected that compiler error without changing the validation requirement.

The first headless daylight journey passed paused dawn/day checks but failed to advance dusk.
Its report records unchanged 18:30 time with pause and focus both true. The probe had sent the
diagnostic label `hud_pause`, which is not a mapped action. It now injects physical Escape through
the existing native event path and requires actual unpause before awaiting time advancement.
The five-second bound, host/input rules and every world/color assertion remain unchanged. The
failed report and disposable database are retained in `qualification/failures/`.

The earlier intermittent editor-settings error recurred during the initial import, despite exit
zero. The later integrated import passed. No error filter, automatic retry, relaxed exit rule or
toolchain change was added. The pinned engine's Android export poller calls `get_adb_path()` from
its background loop, and that function reads editor settings; this suggests an editor lifecycle
race, but the failing native thread was not captured, so the precise cause remains unconfirmed.
See the [pinned Android exporter source](https://github.com/godotengine/godot/blob/4.6-stable/platform/android/export/export_plugin.cpp#L277).
Keep this recurrence and the older [history](../../p01/recorded-history/README.md) and
[rendered travel](../inventory-inspection/REVIEW.md) failure dispositions available.

The canvas boundary follows the pinned [CanvasModulate contract](https://docs.godotengine.org/en/4.6/classes/class_canvasmodulate.html)
and [unshaded material API](https://docs.godotengine.org/en/4.6/classes/class_canvasitemmaterial.html).
Native frames, rather than those API descriptions alone, establish the local rendered result.

## Remaining scope

Weather/cloud shadows, latitude/season/era sunlight, interior/cave lighting, emitters/occlusion,
visibility/stealth, responsive vegetation, wear, animation, audio and accepted production asset
kits remain required. The palette does not invent those world facts. It is not the final lighting
compositor for multiple environments. Reuse the admitted calendar input when adding those actual
systems; do not infer their state from a cosmetic color or add a second authority.

All 14 full UI cases remain unrun. No phase, feasibility, production, export, performance, co-op,
art or human-experience gate is closed. This increment preserves the owner's concerns about basic
gameplay and poor graphics and provides one reusable presentation connection toward addressing them.
