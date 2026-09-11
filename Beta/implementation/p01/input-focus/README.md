# P01 client input-focus increment

Implemented 2026-09-10 from the [retained review packet](../../ui-management/DELEGATION_REVIEW.md).
This is an introduction of UI-11's client input seam. Full UIV-04/UIV-05 acceptance, P03
foundation qualification, P13 device coverage and the wider phase exits remain open.

## Current behavior

One engine-free `FarmInputRouter` owns client context and semantic requests. The Godot
adapter translates native events and polled movement; HUD buttons use the same permission
path. The existing in-process host still validates actions, owns readiness and dialogue
reading holds, advances time, and persists the world. No Core, save format, assembly or
runtime-process boundary changed.

Input priority is unavailable, unfocused, locally paused, opening, addressed dialogue, then
ordinary play. This is the current farm policy, not a general-purpose stack of every future
screen. New wheels, targeting, text entry and management screens must extend the same focus
ownership with their actual contracts and native tests.

| Control | Ordinary play | Dialogue | Locally paused |
|---|---|---|---|
| Escape / controller Start | Pause | Close dialogue | Resume |
| Controller B | Craft when available | Close dialogue | Resume |
| E / controller A | Talk | Close dialogue | Consumed |
| G / controller X | Existing contextual gift/task route | Existing gift/task route | Consumed |
| Explicit HUD Pause button | Pause | Pause while preserving conversation | Resume |

Held movement cannot cross a blocking layer until the affected controls are released.
Opposing held keys are tracked separately from their zero resultant vector. Boundary
sampling handles a layer opening and closing between physics frames; `Input.GetVector`
retains the circular stick deadzone. Consumed key repeat/releases do not become new world
actions. Focus return reconciles missing releases, and unfocused input cannot toggle pause.

## Verification and review

The pure policy tests cover layer ownership, cancellation, repeated/duplicate presses,
release tracking, pending/unavailable states, opposed directions, boundary capture and focus
reconciliation. The native probe uses a separate headless Godot process and a fresh disposable
SQLite world. It injects actual keyboard, controller-button and stick-axis events into the
adapter and observes authoritative movement, dialogue, inventory and calendar state.

The native checks cover opposing held keys across pause; fresh movement after release;
repeat suppression; controller pause cancellation; brief focus loss/return; circular-deadzone
drift and diagonal movement; dialogue movement/time hold; and Escape/B cancellation without
movement or action leakage. Focus signals invoke the real handlers. Injected events do not
establish physical controller, real OS focus switching, rendered usability or human acceptance.

Existing quest/construction recovery probes intentionally preserve an open dialogue. They
now activate the real HUD Pause button and record `hud_pause`; validators still require the
exact intended journey and public state. Product cancellation follows the current contract
instead of an older test's Escape assumption. This is a supported UI signal, not a new host
or test-only gameplay authority.

The lead's integration review corrected the returned Escape binding and same-frame movement
leak, preserved analog behavior, and added pre-GUI release bookkeeping and focus reconciliation.
The first failing native run is retained as development evidence. A subsequent focus-probe
failure was a test delivery-order error: buffered keys must be delivered before simulated
focus return. The corrected probe still allows no intervening physics frame.

Code commit: `58b1f28`. The [full verifier](qualification/final-verification.json) passed
**544 Core + 281 Hosting = 825 tests**, zero skips, **22 independent fixtures**, **68 host
inspections**, native authority/lifecycle recovery and all farm/gathering/production/quest/
construction journeys. The [nine native input observations](qualification/native-input.json)
passed. All 659 original runtime inputs were unchanged during verification; the editor added
two recorded script UID files. The naming scan passed. The [initial movement failure](qualification/initial-input-failure.json)
is explicitly pre-correction evidence. The 19 staged predecessor files and 23 frozen
specification files were preserved. This run used the pinned local macOS toolchain and Debug
build; earlier Release results remain historical, and desktop packaging is not established.

## Delegation and reusable knowledge

The owner-selected `fable[1m]` returned the router, adapter changes and initial focused tests;
the lead owned native qualification, review and corrections. [Measured usage and disposition](delegation.json)
records the returned model and about 672 seconds / $6.06 API-equivalent estimate. This is not
subscription billing. The runner reported denied convenience command forms despite useful
code and verified context; the lead completed integration without restarting the paid task.
Net credit savings are unverified, and the local correction effort was material.

The maintained ecosystem received the native `Edit` permission fix for file creation
(`3eea034`, 30 offline adapter tests), plus input-boundary and economical partial-handoff
lessons (`2f1a794`). Both active skill manifests verify: 137 portable mappings plus 67 alignment
mappings. Five installed files were synchronized with backups for these latest lessons. The
skill frontmatter remained unchanged; the optional bundled skill validator could not run
because its Python environments lack PyYAML. Manifest hashes and changed text were verified.

## Remaining design scope

The current farm remains a limited authored loop with provisional art. This change does not
implement action wheels, a large action catalog, icon/tooltip presentation, full management
screens, rebinding, controller disconnect/device parity or the complete multiplayer menu-time
policy. Those remain in the [living UI supplement](../../PLAYER_INTERFACE_AND_NAVIGATION.md)
and unchanged phase sequence. All 181 baseline requirements remain retained. The owner's
concerns about basic gameplay and poor graphics remain open; native technical checks do not
approve art or establish enjoyment.
