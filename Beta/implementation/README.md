# Implementation progress

The latest [outdoor daylight increment](p02/daylight/README.md) makes the authoritative calendar
visible in the farm through an editable dawn/day/dusk/night palette. HUD and pack brightness remain
independent. The existing nominal 40-minute day, pause rules and saves are preserved.

The [qualification](p02/daylight/qualification/verification-summary.json) retains **855 tests**, zero
skips, the complete integrated native regression, and targeted headless/rendered night-interface
followups. Ten daylight captures and twelve daytime action/pack captures are retained. Only the
probe and its runner changed after full regression; production code and tested assembly hashes
stayed identical. The packet preserves the failed probe-input attempt and recurring, still
unconfirmed editor-settings import error. No acceptance assertion or error policy was relaxed.

This is a P02 presentation prerequisite. The earlier [recorded-history work](p01/recorded-history/README.md)
remains the exact-commit baseline for future temporal systems. All 181 requirements and 14 unrun full
UI cases remain retained; P00/P01/P02 and production exits are open. Weather, seasons, accepted art/audio,
sleep/camp, rich UI, co-op/time, representative performance and desktop exports still need their own
implementation and proofs. The owner's concerns about basic gameplay and poor graphics remain open.

Read the [compact current handoff](AGENT_HANDOFF.md), [living UI supplement](PLAYER_INTERFACE_AND_NAVIGATION.md),
[runtime setup](../Runtime/README.md) and [full plan](../spec-v1_1/IMPLEMENTATION_PLAN.md).
Use proportionate validation during coherent increments while preserving every gate and threshold.
Keep raw evidence in files and consult historical detail only when relevant. Astra remains the default
local lead; a new Fable assignment requires explicit approval. See [milestone discipline](GIT_MILESTONES.md).

## Retained earlier work

- [Native inventory inspection](p02/inventory-inspection/README.md): 846-test checkpoint and twelve rendered states; the farm retains its scoped pack controls.
- [Explicit target selection](p01/target-selection/README.md): 838-test checkpoint.
- [Captured action offers](p01/action-offers/README.md): 832-test checkpoint and independent review dispositions.
- [Input-focus foundation](p01/input-focus/README.md): 825-test checkpoint and measured delegation friction.
- [NPC construction](p01/construction/README.md): source-funded physical construction and preserved Debug/Release evidence.
- [Quest/source integration](p01/quest-construction/CONTENT_CONTRACT.md) and [save review](p01/quest-construction/HOSTING_REVIEW.md).
- [Earlier progress](p00/PROGRESS.md) and [production](p01/production/README.md) preserve preceding implementation evidence.

Earlier qualifications remain scoped to their commits. They are not claims that new code has passed
those older release/export, visual or human checks. The specification snapshots and source archives
remain unchanged.
