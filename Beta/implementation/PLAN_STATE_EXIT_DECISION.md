# PLAN-STATE exit decision

**Status:** lead decision made under the owner's 2026-10-20 delegation, which asks the lead to make the most
logical decision, have another agent review it and record it. This draft is the reviewed input; its final status
and the review disposition are recorded below once the independent review returns. It does not close P01, P02,
P03, any LANG gate, platform, scale or human acceptance.

Inputs: the [conditional recommendation](PLAN_STATE_DECISION.md), [exit reconciliation](PLAN_STATE_EXIT_REVIEW.md),
[membership contract](PLAN_STATE_PROCESS_MEMBERSHIP.md), [current-profile rollout](PLAN_STATE_CURRENT_PROFILE_ROLLOUT.md),
[ownership contract](../Runtime/docs/architecture/partition-ownership.md) and the
[matched owner-cost comparison](p02/partition-state/owner-cost-comparison/RESULT.md).

## Decisions

1. **Ownership model.** Typed stable owners validated against complete operation admission cohorts is the
   canonical ownership model for every persistent fact, as the ownership contract states. Owner keys are world,
   branch, typed role and stable subject identity, independent of place. One authoritative fact, one publisher:
   the existing SQLite authority and atomic commit.
2. **Saved representation through P02.** The complete canonical root under the existing authority remains the
   default saved representation for current and new interpretations. The typed-owner directory and the
   terrain-separated locality image remain internal, qualified candidates; neither is promoted to default.
   Basis: in the matched three-candidate comparison the complete and coarse paths are cheaper than typed-owner
   storage at the current scale, and supported P02 worlds stay inside the 16-region and 64-actor local bounds
   with complete roots far below the 16 MiB cap. Partial residency is a scale need the current game does not have.
3. **Promotion triggers.** Rollout steps B–E of the current-profile rollout proposal become mandatory work, and
   precede any expansion that depends on them, when a measured trigger is met for a supported workload: complete
   publication or admission exceeds its share of the 50 ms scheduler window at p95; the canonical root exceeds
   half of its 16 MiB cap; or a supported world needs residents or regions beyond the local spatial bounds. They
   are measured triggers, not calendar dates, and P03's LANG workloads must report against them.
4. **NPC cognition persistence.** Permanent NPC-01 cognition is a typed actor-owned facet: acquired evidence with
   source and visibility, beliefs with supporting evidence, intentions and accepted commitments, and memory of
   outcomes, each with provenance and chronology. Its owner key is world, branch, role `cognition` and actor
   identity. It has its own versioned canonical codec and is admitted only by a new explicit world
   interpretation after profile 41, through a fresh admission. Profiles up to 41 keep their exact meaning;
   current saves are never migrated implicitly, and any later copied transition follows the 40→41 pattern. A
   later storage-only conversion can move the facet into a typed owner image without a semantic change.
5. **Scheduling for NPC-01.** Reconsideration is deterministic and event-triggered at committed boundaries:
   committed acquisitions, own-resource changes, a commitment becoming feasible or invalid, and a finished action,
   with a declared fallback cadence in the world's own timing domain. Every resident of a supported P02 world stays
   resident, so offscreen continuation is the same detailed execution, not an approximation. Oldest due
   reconsideration age is reported in that timing domain. Any approximation needs its own later qualification.
6. **Finite bounds without depth caps.** Per-actor cognition storage carries explicit finite development bounds,
   like existing archives. Exceeding one refuses the change rather than silently truncating. Retention never
   drops an unresolved commitment, a promise, private provenance or the difference between an attempt and its
   committed result. These bounds are workload limits to revisit under LANG, not limits on character depth.

## Obligation disposition

| Original PLAN-STATE obligation | Disposition |
|---|---|
| Compare ownership alternatives | Done: three matched candidates with identical receipts and worlds; decision 2 records the outcome |
| Stable identities across spatial crossing and real custody transfer | Done within the legacy episode; decision 1 makes the keys canonical |
| Funded offscreen work with absent detail, return and intervention | Done within its scope; general residency deferred to decision 3's triggers |
| Atomic coupled publication and original history | Done for named profiles; unchanged single-writer rule in decision 1 |
| Real host selection and earned timing | Done within scope; decision 5 sets NPC-01's cadence and reporting |
| Actual process discovery | Done within scope; membership and absence remain required before partial loading |
| Bytes, allocations, load, commit, replay, oldest due age | Warm measurements done; cold, scale and multi-domain age move to decision 3's measured triggers and LANG |
| Legal composition, historical versions and preserved-source rollout | Designed; decision 4 adds cognition only through a new interpretation, never by altering old ones |
| Independent architectural decision review | This review |

## Risks accepted

- Deferring partial residency could make a late storage conversion larger. Mitigation: decision 1's keys and
  decision 4's separate codec make the cognition facet convertible without semantic change, and decision 3 makes
  conversion mandatory before the workload exceeds measured limits.
- Complete-root publication grows with every new facet. Mitigation: the triggers in decision 3 are reported by
  P03 workloads; no facet may raise the root cap to pass a test.
- A new interpretation per gameplay facet lengthens the historical interpreter list. This is the existing,
  reviewed versioning mechanism; the facet manifest evaluation in PLAN-STATE remains available if the list
  becomes unmanageable.

## Not decided here

Partial loading, due-age fairness across domains, cold, platform and scale qualification, current-profile storage
conversion and migration, typed-owner promotion, and every P02 closure row remain open with their gates.

## Independent review

Pending. The review assignment, dispatch record and disposition will be linked here.

## Documentation review

- **Plan/spec:** records the PLAN-STATE exit decision the roadmap requires before permanent NPC persistence. No
  requirement, phase exit or gate is removed; scale and migration obligations keep their gates under decision 3.
- **Reference:** pending the review disposition; the ownership contract's extension boundary for persistent
  character depth will cite these decisions once accepted.
- **Accuracy/completeness:** decisions cite the matched measurements, the 16 MiB cap in `WorldSnapshotCodecV1`,
  the local spatial bounds in `LocalSpatialSnapshot` and profiles 40 and 41 in their admission sources.
- **Checks:** `python3 Beta/tools/check_documentation.py --work-record Beta/implementation/PLAN_STATE_EXIT_DECISION.md --files`
  with affected pages, after the review disposition.
