# P01 captured action offers and previews

Implementation commit: `3ae7855`. This is a bounded P01 checkpoint.

This increment connects the existing farm actions to immutable client offers. Talk, gift/task,
gather, craft and conversation close each carry their original actor/world/branch/session,
expected revisions, target and exact intent. HUD buttons and hotkeys execute the last-presented
offer. A changed plant, inventory or quest status cannot silently select different material,
a nearer plant or a different quest operation. The existing host remains the authority.

Known targets, costs and blockers can be inspected through hover and actual Control focus
without submitting anything. Costs stay visible on action buttons. Unavailable known actions
remain inspectable; unsupported gathering/crafting domains stay hidden. Quest requirements
and rewards appear only in the addressed public projection. Source gift lots remain distinct
from transformed material. A client instance owns its offers even across reopening the same
saved control identity; retrying the same offer returns its same submission task and receipt.

[Review](REVIEW.md) records the independent findings and local corrections. The native probe
uses a disposable real world and checks blocked inspection, one oak and two berry harvests,
exact ration inputs/output/waste, Talk and dialogue-first Escape. It also checks visible action
control bounds. Older gathering/production/quest probes now wait for the displayed offer,
retaining their strict one-input/one-effect and restart assertions. The initial timing failure
is preserved in [qualification](qualification/initial-gathering-failure.json); its save contained
zero command outcomes, so it was a refused preview, not a lost or duplicated harvest. A
[second retained failure](qualification/movement-gathering-failure.json) was an explicit host
revision conflict: movement advanced actor revision 74 to 75. Scripted single-action journeys
now wait for released movement to settle and the displayed revision to match before pressing.
This does not change gameplay revisions or automatically retry player actions.

## Verification

The [final integrated report](qualification/final-verification.json) passes **544 Core + 288
Hosting = 832 tests**, zero skips, 22 independent fixtures, 68 host inspections and every retained
native journey. All 669 runtime inputs remained unchanged, with no new generated files, and
the naming scan passed. Nine native input checks and six new offer checks passed. The seven
focused offer facts account for the test increase from the prior 825-test checkpoint.
The separate [rendered action report](qualification/rendered-offers.json) retains five checked
960×540 states: blocked craft, oak, berries, ready craft and dialogue. The first rendered pass
revealed clipped button text; two-line cost labels, the compact movement hint and footer/dialogue
spacing corrected it. [Rendered quest recovery](qualification/rendered-quests.json) separately
checks the source-preserving upgrade, actual acceptance/delivery and repeated/reopened outcome.

To reproduce, use the pinned toolchain from the [runtime README](../../../Runtime/README.md)
and run `tools/verify.py`, `tools/check_native_offers.py --rendered` and
`tools/check_native_quests.py --rendered` with explicit tool and output paths. These tools use
fresh disposable saves. Retained reports name temporary databases for provenance; those databases
are not included as player content. Screenshot files retain their original hashes.

## Scope retained

This is partial P01 work for UI-01/UI-03 and the inspection prerequisite to UI-15. It does not
complete any full UIV case or P00/P01/P02 exit. The 181 baseline requirements remain unchanged.
The farm is still an authored two-resident prototype. General target selection, curated wheels,
action library, rich inventory/maps/calendar/management screens, remapping, full focus/navigation
arbitration, physical controllers, localization/scaling and novice/expert acceptance remain in
the existing phase plan. No icons, new art/audio or human style acceptance are claimed here.

A stale world can still reject an otherwise available-looking action; it explains the rejection
and needs a new player choice. There is no automatic retarget, inventory substitution or retry.
Future target/confirmation flows must preserve that boundary while improving feedback and
validation across the full device and multiplayer matrix.

## Orchestration and checkpoint discipline

[Delegation evidence](delegation.json) records the completed worker run, denied tool attempts,
lead corrections and unverified credit savings. It finished before the owner's latest override.
Astra now works directly by default. Each optional Fable assignment requires explicit approval
first, after a concrete task, expected benefit and usage limit are ready. Earlier authorization
or “continue” does not carry forward. If approved, use exactly `fable[1m]` and the local context
sync hook; do not add relay agents or automatic paid retries. The source and installed shared,
Codex and Claude skills plus both global entrypoints carry this preference.

Follow [Git milestone doctrine](../../GIT_MILESTONES.md) for the qualified commit and preserved
branch. Keep the predecessor's staged work, prior GDD editions and source archives intact.
