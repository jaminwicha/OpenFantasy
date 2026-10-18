# PLAN-STATE due-work discovery and cutoff map

Source boundary: `9e567ab777b221f2d4898858064a172458feefa0`. This is a lead preparation review,
not independent architectural acceptance or a new scheduler implementation.
[Pinned sources and review](p02/partition-state/due-work-discovery/RESULT.md) support the
[timeless contract](../Runtime/docs/architecture/due-work-and-scheduling.md).

## Findings and disposition

| Finding | Consequence for the next implementation |
| --- | --- |
| The ordinary host may select several NPC work commands; the stored interval accepts one | Preserve the whole selected cutoff. Do not simulate fairness by giving each actor a separate advancing step. |
| Action selection order and canonical actor execution order differ | Preserve participation exclusions, exact selected identities and canonical order, including conflicts revealed by earlier actions. |
| Passive effects are not NPC proposals | Mandatory gathering/exposure/expiry/physical processes must complete even when a legal advancing batch has a refused action or no NPC work. |
| A preview dispatch exception can remove all NPC proposals from the fallback batch | There is no demonstrated NPC starvation bound. Diagnose actual deferral reasons; do not treat every exclusion as missed mandatory work or claim starvation has been reproduced. |
| The calendar queue has no production world/session consumer | Reuse its valid contracts only where units and phase semantics fit. Another queue wrapper would not prove offscreen execution or durable discovery. |
| Queue transition phases precede commands; existing ordinary expiries follow actions | A blind queue binding changes behavior. Keep the existing interpreter's order; a different law needs explicit versioning. |
| Profile-five stored work reads all resident exposure owners | No general timing membership, reduced exposure write set, multiple-domain fairness or profile40/41 migration is proved. |

## Complete ordinary cutoff inventory

This inventory follows the ordinary `WorldStepProcessor` path, not every interpreter in the game.
The [current-profile map](PLAN_STATE_CURRENT_PROFILE_MAP.md) remains the complete outer/nested-owner
coverage inventory. Encounters, care, pending physical consequences, controller events and archives
keep their separate admission and recovery contracts.

| Stage | Source and required dependencies | Budget boundary |
| --- | --- | --- |
| Select proposals and player request | Current controls, readiness, availability, participation, actual commitments, exact resources and geometry | Preparation only; no promise that every feasible desire executes |
| Execute canonical selected actions | Whole selected batch and prior actions' effects, unique identities, ordinary authority and full coupled admission | Never persist a fraction of one action or silently discard a selected action to meet a budget |
| Exploration time and expiries | Exact advancing count/calendar/pacing; coordination proposals and paid quotes | Advance and expire once in existing order, after selected actions |
| Reservoir transport | Selected calendar, hold state, occupied identities, actual transport/reservoir/inventory owners | Include conserved transfers and created identities atomically |
| Gathering and resident exposure | Applicable process interval or advancing count; actual gathering and every affected resident | No unloaded-owner omission; held interactions earn no interval |
| Detailed liquid flow | Applicable calendar/process boundary, actual cells and held state | Bounded-plan failure aborts the selected step; no partial time/flow |
| Movement | Post-action/post-boundary world, current authority/availability and actual occupancy; workers/holds suppress movement | Preserve canonical actor decisions and complete candidate admission |

## Next bounded executable witness

First qualify discovery and preparation against an ordinary cutoff with at least two actual eligible
host builders or another explicitly justified contrasting autonomous owner. Admit legal quest funding,
tools, reserved lots and nonconflicting reachable worksites through the existing contracts. Preserve
the current actor/access ceilings. Compare the exact selected command identities, receipts, candidate
bytes/owner directory, resource deltas and due effects with ordinary execution at every boundary.
One successful builder cannot substitute for this contrast.

1. Establish ordinary selection with both actors eligible at one cutoff, then preserve that same
   opportunity through partition preparation. Include an unrelated resident with exposure and an
   actually depleted gathering node that becomes due; it must not depend on camera residency.
2. Bound preparation so the candidate spans more than one preparation slice. Before completion,
   published sequence, clocks, owners and receipts stay unchanged. After completion, one ordinary
   logical boundary publishes once. Do not add artificial advancing commits to drain preparation.
3. Cover an actual hold and its release, ordinary action refusal, exclusion by real participation,
   and a dependency-changing intervention. Invalidate/reprepare before publication; no stale work,
   hidden stock grant or retrospective catch-up. Diagnose fallback behavior separately from passive debt.
4. Retain deterministic discovery across owner enumeration and load order. If real work is deferred,
   retain its causal identity/continuation and measure oldest due age in its actual domain. Distinguish
   eligible-but-unselected proposals from mandatory backlog; report the latter as unavailable when no
   authoritative domain/obligation mapping exists, not as an assumed zero.
5. Reopen the same saved lineage with original command/receipt history and exact work/exposure.
   Exercise before/after publication faults. Any durable preparation state needs its own actual
   restart proof; in-memory resumption must not be presented as durable continuation.

Cheap preflight validates funded geometry, occupancy and all authored routes under lawful movement,
exact nonzero test discovery and intended source/binary pins. Keep each managed run's existing
180-second process-group bound, classify failures before a deliberate rerun, retain them and do not
retry automatically. Match the maintained runners; no new testing framework is needed.

## Remaining exit boundaries

This is a preparation map, not executable qualification of the witness above. General obligation
discovery, multi-actor fairness, persistence of timing membership, current40/41 nested migration,
cold/platform/scale measurements and independent PLAN-STATE review remain open. Retain the complete
P02 closure table and scheduled NPC-01/story/ecology/art/audio work after the decision/review exit.
The previously qualified noninitial profile-five checkpoint remains valid within its own evidence;
this review neither reruns it nor broadens its scope. No queue limit becomes a gameplay cap.
