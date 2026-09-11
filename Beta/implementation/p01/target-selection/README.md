# P01 explicit field target selection

Implementation commit: `c7e29f5`. Qualified locally on 10 September 2026 (the reports use
11 September UTC). This builds on the preserved [captured offers](../action-offers/README.md).

Press **Q / right shoulder / Next** to cycle Wren and the farm's publicly observed harvestables.
Press **R / left shoulder / Clear** to return to automatic contextual offers. The HUD names the
selection and a field marker follows its public position. These controls do not spend resources
or submit world commands. They obey the existing pause, conversation, focus, pending-action and
held-input boundaries.

The selected entity stays selected when another plant becomes nearer. Depleted plants remain
selected with a recovery blocker; absent plants retain their known name and identity with no
position or executable offer. Regrowth restores that same target. Talk/Give cannot redirect a
selected plant to Wren, and Gather cannot redirect a selected person to a plant. Explicit Clear
restores contextual selection. A captured action still owns its original exact intent even if
the player subsequently chooses another target.

Selection, marker and offers refresh together from one public snapshot. Changing the world,
branch or controlled actor clears client selection; a new client lifetime starts automatic.
Selection is transient presentation state, not a saved preference. The deterministic Core,
in-process authority, command and save formats are unchanged.

## Executed qualification

The [full verification](qualification/final-verification.json) passes **544 Core + 294 Hosting
= 838 tests**, zero skips, all 22 independent fixtures, 68 host inspections and all retained
native journeys, including quest/construction and save recovery. All **672 original runtime
inputs stayed unchanged**; editor import added two script UIDs, both included in the code commit
and the report's 674 final hashes. Builds have zero warnings/errors and the naming scan passes.
There are nine native input checks and eight expanded native offer/target checks.

Six new focused facts cover stable duplicate-name identities, read-only cycling, immutable
captured targets, movement, depletion/disappearance/regrowth, incompatible verbs, control-scope
reset and input ownership. They use actual world processing and public projections; choosing
targets is checked against unchanged encoded authority. The [local review](REVIEW.md) records
the scope, fixture correction and rendered capture correction.

The [rendered report](qualification/rendered-targets.json) retains eight 960×540 states with
their original hashes: [person](qualification/native-offers-asw3b4tn/offers.person-target.png),
[plant](qualification/native-offers-asw3b4tn/offers.plant-target.png),
[locked out-of-range target](qualification/native-offers-asw3b4tn/offers.locked-target.png),
blocked craft, oak/berry offers, ready craft and dialogue. The native journey injects keyboard
and shoulder events, invokes the actual Next control and checks target identity, marker position,
blocked input, real harvests, exact crafting quantities and conversation cancellation. The report
also hashes its disposable database for provenance; that database is not retained as game content.

Reproduce with the [pinned runtime setup](../../../Runtime/README.md): run `tools/verify.py`
with explicit tool paths and `--forbidden-token Beta`, and run `tools/check_native_offers.py`
with `--rendered`. The latter now requires eight checks and eight rendered PNGs. Screenshots were
captured before that harness-only minimum/scope update; the full final verifier runs the updated
harness against the same client behavior. All runs use disposable saves and separate processes.

## Remaining scope

This is partial UI-01/UI-03/UI-11 evidence, not completion of UIV-01 or a phase. The public view is
the bounded farm's admitted projection; this does not establish distance-based sight, occlusion
or global observer filtering. Target cycling currently covers Wren and harvestables only.
Arbitrary pointer selection, damaged objects, rich target confirmation, wheels/action library,
icons/management screens, saved UI preferences, remapping, physical controllers, other screen
sizes/locales and novice/expert acceptance remain in the plan. The marker can be outside the
viewport or behind fixed HUD space; the target label remains visible. Full-screen field framing
and off-screen target guidance need the later navigation/viewport review.

Provisional art is unchanged. All 181 baseline requirements remain retained; all 14 full UI
acceptance cases and full P00/P01/P02 exits remain open. Work was completed directly in Astra without another model
assignment. Follow [milestone discipline](../../GIT_MILESTONES.md); preserve prior evidence and
the predecessor's unrelated staged work.
