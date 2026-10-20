# PLAN-STATE exit review: disposition

The independent reviewer returned **accept with required changes** for
[the PLAN-STATE exit decision](../PLAN_STATE_EXIT_DECISION.md) at revision 684dc873. The
[returned review](REVIEW.md) and [dispatch record](DISPATCH.json) are committed; the raw session stays local.

The lead accepted every required change and both suggestions. Each figure and reference the review relied on was
re-read from its source before use. The changes are to wording and scope; the review asked for no runtime work
before acceptance. Two findings also changed the NPC-01 Core contracts written in parallel, as the last column says.

No second review was dispatched. The verdict needs no re-decision, every required change is applied as written,
and the owner's guidance keeps review proportional. The next independent critic belongs at NPC-01's first world
binding, where findings R4–R8 become executable.

## Findings

| Finding | Disposition | Where |
|---|---|---|
| R1 High: total residency redefines offscreen | Accepted. Total residency is scoped to NPC-01's first episode, its evidence is labelled player-absent detailed continuation, and non-resident reconciliation stays a P01/P02 gate before P02 closure, first exercised by the traveling merchants | Decision 6; obligation rows for funded offscreen work |
| R2 High: triggers not measurable, lagging, incomplete | Accepted. Named host, build and workloads; numeric thresholds; reopen, journal and cognition-bytes triggers; each mapped to its workstream; design-time projections; steps B–D certain for the P01 exit; LANG reporting starts now | Decision 3 |
| R3 High: decision 2 misstates the comparison | Accepted. The partial-commit figures are stated as measured, and the real basis replaces the wrong one; a current-profile baseline is required before NPC-01 acceptance | Decision 2 |
| R4 High: ownership and dependency coverage | Accepted in full: references to accepted commitments and existing knowledge, motives as authored content, deferral records and pending reconsideration, every person-actor, the producing action's cohort, per-owner framing | Decision 4. Core: pending wake added; the codec already frames one record per actor |
| R5 High: recorded decisions versus laws | Accepted. Evidence and belief derivation are laws; decisions enter only through recorded commands with the policy version; replay never runs the planner | Decision 5. Core: decisions and memories carry the policy version; a test replays recorded decisions without the planner |
| R6 Medium-high: wake triggers | Accepted: lawful wakes only, persisted wake with cause, advancing-step domain, recorded suppression, autonomous-proposal service with the fallback and open starvation bound, no empty commits | Decision 6. Core: wake with cause and suppression, earliest-wins, fallback cadence |
| R7 Medium-high: bounds | Accepted. Overflow stops the authority and never refuses an action; the full retention rule; the lossless path for step 3; composition fit stated | Decision 7. Core: the capacity exception documents authority stop, not refusal |
| R8 Medium: copied 41→42 transition | Accepted as the transition's required rules; the transition itself is not implemented | Decision 8 |
| R9 Medium: versioning | Accepted: strict successor, storage dispatch for codec-only revisions, a freeze point, default creation and the qualification origin | Decision 8 |
| S1 Low: nested owner map for ≤41 | Accepted | Decision 1 |
| S2 Low: affected pages and open items | Accepted | Documentation review; Not decided here |

## Obligations the review judged misdisposed

| Item | Disposition |
|---|---|
| Stable identities row dropped unproved realms and frontier populations | Restored |
| Atomic publication row dropped checkpoint, migration and partial authenticated replay | Restored as open |
| Host selection row omitted fairness, starvation and bounded final projection | Restored as open |
| Measurement row deleted cold load by moving it to triggers | Corrected. What was measured is a first authority open in a fresh test process. OS-cache-cold load, retained and peak residency, boundary stalls and oldest due age stay PLAN-STATE residuals, gated before step D and P02 closure |
| Missing rows: global journal, facet manifest, branch and history reads | Added |
| Funded offscreen work row made general residency trigger-contingent | Corrected with R1 |

## Review quality and cost

The review was specific and checkable: every finding named a line and a source, and every figure checked out. One
assumption was flagged by the reviewer as unverified: one journal commit per nominal step. The local session
commits an advancing step on every tick outside a reading hold, so the assumption is plausible; decision 3
requires the baseline to measure it. The reviewer estimated about $3.50; the provider reported $3.98 against the
$20 limit, in 888 seconds against 2,400.

## Documentation review

- **Plan/spec:** dispositions the PLAN-STATE independent review; no requirement, phase exit or gate changes.
- **Reference:** decision 4–8 wording is cited from the ownership contract's extension boundary.
- **Accuracy/completeness:** every finding and misdisposed obligation in the returned review has a row.
- **Checks:** `python3 Beta/tools/check_documentation.py --work-record Beta/implementation/plan-state-exit-review/DISPOSITION.md --files`
  with the decision and affected pages.
