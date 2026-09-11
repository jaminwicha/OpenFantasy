# P02 inventory inspection prerequisite

This increment gives the actual farm inventory its first native inspection screen. It begins a
P02 interface dependency without claiming P01 is complete or changing the phase sequence.
Implementation commit: `3eed739`, 11 September 2026.

Open **Pack** with **I**, controller **View/Select**, or its HUD button. Browse actual owned
tools, material stock and crafted outputs using pointer hover, arrow keys/D-pad, Tab or selecting
an item. Names, quantities and the same details are available without a mouse. Inspection never
equips, consumes, crafts or transfers. There are no inert equipment/use buttons for actions the
current pack does not implement. Native icons remain in the separate artist pipeline; these
text cards establish the truthful inspection and navigation prerequisite only.

The screen groups counts without merging real lots or their permissions. Source and crafted
stock, and protected versus unprotected delivery stock, stay separate even with identical names.
It reads only the current actor's public inventory and admitted tool/recipe information. Unknown
definitions have an unfamiliar-item fallback. Refresh preserves a valid selection and falls back
to an available row if that group disappears; selecting a row never creates a world command.
World/branch/actor changes reset the model's selection scope. This is transient client state,
not saved profile preferences or a production identification system.

The local host exposes its actual scheduler state through a read-only query. The panel displays
**World continuing**, **World waiting**, **World paused** or **World unavailable** with the
projected date/time. Opening inspection sends neutral player movement, not a new pause request.
A running world continues; opening from pause retains that pause. Escape/I/View/B/Close closes
only the pack, and a separate Escape is needed to resume a previously paused world. Focus stays
inside the screen and returns to the previous valid HUD control. Held movement must be released
before field control resumes. The existing host handles OS focus readiness; this does not qualify
multiplayer menu/Wait policy.

## Evidence and review

The [full regression](qualification/final-verification.json) passes **544 Core + 302 Hosting
= 846 tests**, zero skips, all 22 independent fixtures, 68 host inspections and all retained native
journeys. All 679 original runtime inputs stayed unchanged; editor import added three retained
script UIDs, giving 682 final hashes. Builds have zero warnings/errors and the naming scan passes.
The nine native input checks and expanded twelve offer/target/pack checks pass. This qualifies
the local implemented behavior only; the broader gates below remain open.

The [rendered journey](qualification/rendered-inventory.json) contains twelve offer/target/pack
checks and twelve 960×540 captures, including [ration inspection](qualification/native-offers-c81mo5nn/offers.pack-inspection.png)
and [a running world](qualification/native-offers-c81mo5nn/offers.pack-running.png). It uses
actual gathered materials and one conserved ration craft, then exercises Tab focus trapping,
keyboard/D-pad/hover/confirm inspection, field-shortcut refusal, B cancellation, pause retention,
selection/focus return, continuing host time, held-movement release and injected focus recovery.
The five visible item groups represent six real stacks and thirteen owned units; display grouping
does not alter physical custody. The original offer, quantity, craft and dialogue checks remain.

Eight focused facts cover actual gathering/delivery and own-pack scope, empty/changed actor
inventory, unknown definitions, identical-name provenance, protected stock, menu/time ownership,
held-input boundaries and real SQLite-session readiness/reading-hold/shutdown status.

The [review](REVIEW.md) records corrections and an unresolved earlier rendered travel timeout.
That failed run remains retained alongside the later passing run. No timeout, conservation,
input-isolation or exact-outcome assertion was relaxed. Native probe reports now include focus,
pause, inventory, pending-action and final public-view diagnostics.

## Gates and remaining work

This is bounded UI-05/UI-11 work and partial UIV-04/UIV-07 evidence. It is not a completed inventory
system, UIV acceptance case, feasibility gate or phase exit. All 181 baseline requirements remain
retained and all 14 full UI cases remain unrun. The frozen specification and earlier evidence are
unchanged. Full P00/P01/P02, independent critic, human experience, art/audio throughput, full
temporal/co-op, representative workloads, desktop export/device and production gates remain open.
Recent unit/native successes must not be relabeled as evidence for those broader obligations.

Item icons and approved art, equip/use/transfer actions, body/stat comparisons, containers,
weight/volume, sorting/filtering, rich item identification, saved preferences, remapping, large
catalogs, scrolling/localization/scaling breadth, physical devices and novice/expert usability
still require their own implementations and native acceptance. The current panel does not show
other actors' packs or remote stock. The host status query is a local-session diagnostic, not a
network clock or a new authority. Core commands, persistence formats and world rules are unchanged.

Work and review are local to Astra; no additional model assignment or independent-agent review
was invoked. Use the [current handoff](../../AGENT_HANDOFF.md),
[runtime instructions](../../../Runtime/README.md) and [milestone register](../../GIT_MILESTONES.md).
