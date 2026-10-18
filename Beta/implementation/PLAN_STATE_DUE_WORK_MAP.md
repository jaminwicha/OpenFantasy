# PLAN-STATE due-work discovery and cutoff map

Source boundary: `9e567ab777b221f2d4898858064a172458feefa0`. This is a lead preparation review,
not independent architectural acceptance or a new scheduler implementation.
[Pinned sources and review](p02/partition-state/due-work-discovery/RESULT.md) support the
[timeless contract](../Runtime/docs/architecture/due-work-and-scheduling.md).

## Findings and disposition

| Finding | Consequence for the next implementation |
| --- | --- |
| The ordinary host may select several NPC work commands; the original stored interval accepted one | The stored-batch extension now preserves a captured actor/project set and one cutoff. Do not simulate fairness by giving each actor a separate advancing step; richer profiles still require their complete closures. |
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

The [ordinary multiple-NPC reference](p02/partition-state/multiple-npc-cutoff/RESULT.md) now qualifies
two actual host builders in the existing coordinated profile27: real deliveries/routes, shared
cutoffs, holds/exclusions/refusals and complete SQLite original-history/fault recovery. Nine new
cases and thirteen affected existing cases pass. This is the ordinary reference only: it retains
complete world state, selects construction policy directly rather than exercising a ready-player
session, and does not test an unrelated third resident or newly due depleted node. No partial-residency
preparation budget, persisted discovery frontier or multi-domain fairness is established. The existing
legacy26 versus coordinated27 aggregate-bound contrast also passes unchanged; never weaken that bound
to make a multi-actor profile-five extension pass. The frozen current40/41 migration obligations remain.

The [stored-batch preparation](p02/partition-state/stored-batch-preparation/RESULT.md) adds exact-head,
owner-count-bounded read slices over the union of captured work dependencies, followed by one canonical
batch and one passive boundary. Its explicit profile-five two-builder admission reuses real deliveries
and routes. The sole SQLite writer publishes all receipts and changed owners atomically. A project is
decoded from its own canonical payload, preserving its revision separately from the catalog total.
Final installation and crossing the legacy aggregate bound return the whole batch to ordinary admission.
This is an extension of the existing typed-owner prototype, not another full-world wrapper or a new format.

Live session yielding is now qualified within the explicit legacy binding below; durable preparation
continuation remains a separate unimplemented contract.
[Yielded authority publication](p02/partition-state/yielded-publication/RESULT.md) now exposes bounded
read calls that release the authority gate, with exact originating-authority/interval/head checks,
cancellation, one complete publication and recovery fencing. The synchronous wrapper drains the same
path under its outer lock. The opt-in live consumer below retains a handle between ticks; its selection,
preview and final projection still need complete-world reads. Owner-count slicing does not bound
final rule-evaluation CPU time, persist domain membership, or establish starvation limits. The
[combined due-boundary proof](p02/partition-state/combined-due-boundary/RESULT.md) now supplies the
missing combined contrast: two actual funded builders, a newly admitted unrelated observer, all four
original residents retained (five total), and a node depleted through real harvesting becoming due
at its unchanged regrowth delay. Sliced preparation matches ordinary execution, refuses a missing
bystander actor and publishes all work/regrowth/exposure together with retained original SQLite history
and before/after fault recovery. This is scripted authoritative preparation, not a live ready-player
session or a general obligation discovery algorithm. General obligation
discovery, multi-actor fairness, persistence of timing membership, current40/41 nested migration,
cold/platform/scale measurements and independent PLAN-STATE review remain open. Retain the complete
P02 closure table and scheduled NPC-01/story/ecology/art/audio work after the decision/review exit.
The previously qualified noninitial profile-five checkpoint remains valid within its own evidence;
this review neither reruns it nor broadens its scope. No queue limit becomes a gameplay cap.


## Live session integration boundary

The [live construction packet](p02/partition-state/live-construction-slices/RESULT.md) binds the authority
handle to the existing serialized LocalWorldSession loop through an explicit internal positive
owner-count option for profile-five/schema14. Default creation and current40/41 remain unchanged.
Only movement-free NPC construction with no awaiting player action qualifies. Ordinary admission
preview selects the complete batch first; preparation retains it across ticks and charges all work,
regrowth and every resident's exposure once. Newly queued actions wait for the next cutoff; movement
leases are not extended. Each read slice preserves the previous immutable view.

Pause/stop between slices cancels unpublished selection without earning time. Resume selects fresh.
Readiness sampled before an in-flight final opportunity does not promise immediate rollback. A real
intervening head change discards stale selection, refreshes the view and ends that tick; no automatic
replay is attempted. Same-source semantic errors and storage failures propagate. Committed receipts
and full-world projection use the shared ordinary observer. New tests cover original-genesis recovery,
queued receipt identity, coherent regrowth/work/exposure, ordinary movement, cancellation and intervention.

Complete selection/capture, possible input reads, final evaluation/fallback and projection remain
synchronous/full-cohort work. This is not selective live residency, a final CPU bound or a throughput
claim. Durable cursors, general obligation discovery/membership, oldest-due age/fairness, current40/41
migration, scale/platform measurements and independent review remain open. Preserve the complete
P02 table and scheduled next gameplay work; another wrapper or repeated proof cannot replace these exits.


## General process discovery follow-on

The [process discovery contract](PLAN_STATE_PROCESS_DISCOVERY.md) source-checks three constraints for
the next executable unit: effective post-action owners, distinct timing domains, and lawful blocked
outcomes versus missing evaluation. Its next contrast uses actual regrowth and reserved transport,
including authentic admission limits and historical interpreters. It does not implement an index or
close any general scheduling, migration or review gate. Avoid another construction-only wrapper.


The [typed domain binding](p02/partition-state/domain-process-discovery/RESULT.md) now implements the
regrowth/transport queries used by their real executors, with exact source affinity, canonical discovery,
stock eligibility and retained blocked identities. The living process contract distinguishes qualified
component/current-profile limits and remaining joined-history/index/fairness evidence. No phase gate closes.
