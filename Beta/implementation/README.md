# Implementation progress

The [current agent handoff](AGENT_HANDOFF.md) owns current progress, active assignments, evidence and
open exits. Read it together with [implementation instructions](../AGENTS.md) before choosing work.
This index routes readers to that record; historical packet counts apply only to their own revisions.

Use the [runtime setup and controls](../Runtime/README.md),
[architecture reference](../Runtime/docs/architecture/README.md),
[living UI supplement](PLAYER_INTERFACE_AND_NAVIGATION.md),
[equipment and vehicle upkeep roadmap](EQUIPMENT_AND_VEHICLE_UPKEEP.md) and
[full production plan](../spec-v1_1/IMPLEMENTATION_PLAN.md) for their respective contracts.
Frozen designs and predecessor content remain preserved. Scope extends through production; a passing
increment does not close a phase, art/UX acceptance, platform coverage or a production feasibility gate.

The shared implementation instructions contain the project-specific delegation authorization and
limits. Astra owns integration and Git. Approved external work uses the maintained context/skill
synchronization hook, explicit ownership, finite limits and reviewed evidence. Keep verification
proportionate without weakening gates; follow [commit and milestone discipline](GIT_MILESTONES.md).

## Retained earlier work

- [Outdoor daylight](p02/daylight/README.md): retained calendar-driven presentation and its scoped qualification.

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
