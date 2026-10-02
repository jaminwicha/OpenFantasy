# Lead review observations during corrective implementation

This is provisional source review, not a defect claim against the final delivery or a second critic.
The external implementation is live; Runtime remains worker-owned. Recheck these items against the
returned source and tests before making integration edits.

## Care absence wording

The current `EncounterOutcomeText.CareReason(null)` says no helper has agreed to care. However,
`LocalCareView.Project` returns null not only when agreements are absent: it also does so when the
observer is not the last resolution's defeated target, when phase/last-resolution conditions do not
match, or when another patient's care is pending. Therefore absence of this projection alone does
not establish absence of a standing agreement. This matters to incapacitated winners or earlier
casualties in a multi-participant encounter. Prefer an honest absence-of-offer message unless the
public view explicitly establishes agreement absence. Preserve the distinction between agreements,
available treatment and completion policy. No experiment run yet; inspect final tests and actual
world reachability before assigning severity.

The new available-helper detail also says the world completes at its next boundary. In the two-human
case this is conditional on every required reading, and existing pending care must still be honored.
Check final UI copy retains that condition wherever it states the consequence of accepting.

## Pending care is not helper commitment

The provisional unavailable status says the named helper "accepted but cannot treat you now".
`LocalCareView` derives its pending status from the patient's pending acknowledgment plus ability
and helper control. That pending acknowledgment is not itself the helper's actual commitment or
bodily action. Prefer "The requested care is unavailable" / "cannot provide the requested care now"
unless the displayed fact is backed by an actual helper commitment. This preserves the project's
explicit acknowledgment / permission / commitment distinction. Recheck final wording before edits.

## Negative-proof specificity

The provisional complete-root test uses the side-zero second human and host Wren (also a non-participant)
as invalid readers. Inspect final coverage for a positive-side, host-controlled participant as a separate
control-kind contrast; a host who is also outside the fight does not isolate that distinction. The new
two-human care test's rejected completion Preview currently accepts any Exception; retain the direct
not-ready assertion and check the thrown failure's type/reason before treating it as proof of the care
hold. These are test-review follow-ups, not claims that the runtime rule is defective.

## Rendered terminal HUD clipping (observed)

The second native attempt (`qualification/outcome-native-20261002T005845Z-25e50`) completed its victory
and defeat process routes but failed the care route. Lead image inspection of
`native-encounter-outcome-nsxi_ckt/defeat.terminal.png` shows the new outcome card itself inside the play
area, but the lower ordinary HUD extends beyond both viewport edges: its text begins cropped off the
left and action controls extend off the right. The long terminal status/detail likely grows a Control
minimum width; establish the actual layout cause before fixing. `CheckPanel` currently proves the
new card/actor bounds, not that the underlying HUD remains usable. This is a real visual defect to
resolve before rendered acceptance; a route process exit0 is insufficient. Keep the original image.
Check the same state at the supported viewport after correction and add an actual HUD/control-bound
assertion where this change can break it. Prefer concise status with full explanation in the outcome
card/tooltip, and proper wrapping/shrinking layout; do not merely hide a failed assertion.

The first native attempt's layout failure occurred before it saved the offending outcome-card image;
it retained bounds and a prior engaged image only. Consider saving each observed panel image before
its bounds assertion so future failures preserve the state being diagnosed. This is diagnostic quality,
not permission to relax any native deadline or visibility gate.

## Care fixture preparation boundary

The current care fixture now enters the wolf encounter through a real managed command and exports at
the first decision boundary. This is not a precomputed Outcome; native combat can still be real, but
all manifest/header/checker/result claims must distinguish this preparation from native entry. At this
inspection `Export.Boundary` still says native owns the encounter and the top-level comments promise
no encounter before native input. Recheck the final text. Victory/defeat entry evidence does not prove
a player can reproduce this care-specific approach/entry timing. Keep any remaining real-player care
entry/availability issue explicit rather than treating prepared Active as proof that issue was fixed.
Authenticate the actual prepared commands and re-derived declared genesis; do not invent history.

## Care and decline key hints (observed)

`qualification/outcome-native-20261002T011134Z-09cad/native-encounter-outcome-rb909ptc/care.care-outcome.png`
shows both the care button and the separate outcome-acceptance button labelled [E]. The footer says
E chooses care, and the router gives that key to care when care is present. The acceptance button must
not advertise the same shortcut if it only works by pointer/focused confirmation in that state. Check
its tooltip/key hint, focus/controller behavior and the new router guard against the final source.
A distinct accepted-outcome intent must stay distinguished from the legacy release, including in input
eligibility. The failed pointer at (810,484.5) was within the visible acceptance button; do not attribute
that particular failure to out-of-viewport coordinates without evidence.

## Exact native-command correlation remains to prove

The native and outcome-auth runs `011903Z-c8758` / `012114Z-7b96e` pass their implemented assertions.
Source review of `EncounterOutcomePlayProbe` and `NativeEncounterOutcomeResultTests` shows input
keys/pointer coordinates and whole-journal replay, but no captured submitted command IDs/bytes tied
back to the exact offered/selected native actions. The result test counts command types and unique
IDs found in the journal and checks the player's binding; that is not the original packet's requested
comparison of exact captured native commands with their committed receipts. Check final source for
an added correlation before acceptance. If still absent, instrument the existing production submission
boundary (reuse a suitable observation seam if present), record the selected scope/intent and actual
command/receipt identity, and authenticate those exact bytes/IDs against the full save. Include the
outcome reading, combat choices and the post-completion sale; distinguish host-owned entry/completion.
Do not replace this requirement with success flags or counts. The existing passing replay result stays
valid at its stated scope and its artifacts must remain preserved.

The new fixture derivation rejects extra human control and uncalibrated defeat wounds, and reconstructs
the genesis with maintained admission factories. The fixed wounded-chief claim should additionally
assert the declared HP1 (the current Derive accepts the supplied single wound value); the care route
already asserts the prepared player's HP1. Header/manifest wording still needs the prepared-care-entry
correction noted above. These observations are against the live source, not a final worker result.

## Two-human test diagnosis: the wolf does not own the Squire strike

Source-confirmed follow-up to the 3,000-cutoff failure (`outcome-unit-20261002T012210Z-74f80`):
`FightFirstWolf` asks the human-controlled wolf to use the shared test helper `Strike`, which selects
key `strike`. `EncounterFarmAdmission` gives the player only `strike` and the wolf only
`bramble_wolf_bite`; `WorldEncounterActions.EvaluateAbility` refuses abilities outside that combatant's
AbilityIds. Switching controller kind does not grant a human's abilities to the wolf. Replacing each
refused strike with Wait therefore cannot make that wolf win. Use the actual admitted wolf bite (or
select from that actor's real available abilities), preserving its NaturalWeapon requirement and all
costs. Do not grant a new attack, weaken availability, increase the cutoff bound or keep retrying the
wrong ability. Recheck live worker edits: it may independently have corrected this before return.

## Managed care diagnosis resolved

The worker independently corrected the helper to choose from the actual acting combatant's admitted
AbilityIds (`Attack(state, actor, target)`), preserving availability and the 3,000-cutoff bound.
`outcome-unit-20261002T012955Z-3aa41` passes all 27 focused cases. The earlier unavailable/loop failures
remain preserved. This resolves the wolf-strike test-harness finding above; it does not close the other
UI/identity-correlation/coverage findings in this note.

## Wheel omits acceptance beside care

`FarmQuickWheelModel` uses `AcknowledgeCare` instead of `ReleaseEncounter` in its only outcome slot.
In profile35 both offers deliberately coexist, but the wheel therefore exposes only care, with the
other three encounter slots empty. Add the independent outcome acceptance to an unused slot/page
when both offers exist, preserving legacy layouts. Inspect `FarmWheelDisc`'s static ReleaseEncounter
label (currently Continue) so it reflects Accept defeat/Accept outcome correctly. Prove the new wheel
choice reaches the same exact admitted acknowledgment and cannot accidentally request care. The
existing focused-control path is a separate matter; no claim that every controller route is broken
is made without a native/device test. Preserve the keyboard default to care and accurate key hints.
