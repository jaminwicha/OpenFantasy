# Pass 2 — scope, decision and documentation review

Reviewed 2026-10-03 against committed `df606752`, with the shared checkout still active. This pass owns only this report. No provider, runtime build/test, benchmark, commit or other document/source edit was performed. Three new current-documentation/process findings need disposition; none asks the audit to implement future gameplay.

## New actionable findings

### P2-S1 — Restore the traveling-merchant/escort introduction to the P02 closure table

**Evidence:** [CURRENT_ROADMAP.md:85](../CURRENT_ROADMAP.md:85) routes notice/recruitment/cooperative adventure and buy/haul/danger/sell, while its NPC row routes cognition/offscreen work. Neither routes a merchant traveling with actual goods or retained traveler routes/commitments. Caravans/escorts occur only in the broader P07 ledger row. Meanwhile [AGENT_HANDOFF.md:47](../AGENT_HANDOFF.md:47) calls this table the complete P02 closure authority.

The frozen [IMPLEMENTATION_PLAN.md:138](../../spec-v1_1/IMPLEMENTATION_PLAN.md:138) explicitly requires the two-market cargo/escort circuit, traveling merchants bringing the actual goods, and NPC travelers retaining routes/commitments in P02; it then says P07 expands the economy. RQ-137 and RQ-146 retain P02 introduction and P11 full completion in both coverage and the audit matrix. The original requirements were preserved, but their early witness is missing from the newly authoritative execution checklist. This is a routing omission, not a finding that all merchant/escort gameplay must be finished now.

**Smallest correction:** add the bounded P02 merchant/traveler and actual escort witness to LOOP/NPC, or a small TRADE row with an accountable role. Require real carried goods, retained routes/commitments and an actual escort contribution through the same cargo journey; keep full trade-network/caravan/economy breadth in P07/P11. Do not silently turn this into a requirement for a complete wage-service catalog in P02.

### P2-S2 — Reconcile the next-work sequence with accepted recovery and prerequisite order

**Evidence:** [CURRENT_ROADMAP.md:45](../CURRENT_ROADMAP.md:45) now says the bounded recovery checkpoint is complete. The [handoff](../AGENT_HANDOFF.md) identifies its accepted `cbcd7565` milestone and selects the common development journey next. However [CURRENT_ROADMAP.md:118](../CURRENT_ROADMAP.md:118) still instructs the next worker to “Close the bounded recovery qualification”; its earlier goal paragraph also refers to the “next recovery result.” This is a live instruction left over from the pre-acceptance audit, not a clearly dated historical observation.

The same sequence at lines127–128 says to close the full native/human P02 exit and then reconcile outstanding P00/P01 obligations. The document does preserve the original dependency rules elsewhere, so this is an ambiguous ordering rather than a deliberate gate waiver. The frozen [acceptance graph and explanation](../../spec-v1_1/IMPLEMENTATION_PLAN.md:59) prohibit phase closure before retained prerequisites.

**Smallest correction:** move the completed recovery to a retained prerequisite/status sentence, retain its populated-allowance/listener/latency gaps, and start the active sequence with common-lineage journey selection. Say explicitly that P00/P01 exit obligations must be reconciled before P02 phase acceptance, while useful P02 preparation can continue in parallel. This avoids redundant qualification and makes the existing dependency rule operative at the place workers choose next work.

### P2-S3 — Always validate the work record's own local links

**Evidence:** [check_documentation.py:62](../../tools/check_documentation.py:62) constructs the file/link-check set from defaults or `--files`. At lines68–70 it calls `check_record(work_record)` and separately calls `check_file` only for that set. The work record itself is not automatically included. Consequently, the standard command in Beta instructions can report success while the record's Plan/spec, Reference or Checks fields link to nonexistent files.

A read-only in-memory probe supplied all four nonempty fields with the Checks field linking to the deliberately nonexistent `nonexistent-pass2-evidence-3a72.json`. `check_record` returned `[]`; `check_file` on the same record correctly returned a missing-local-target error. The defect is selection of the checked files, not the link parser. No actual missing artifact was created. This matters because [DOCUMENTATION_MAINTENANCE.md:53](../DOCUMENTATION_MAINTENANCE.md:53) requires real paths/reasons and actual check evidence in this record, and the normal accepted workflow mandates the checker.

**Smallest correction:** include the resolved work record in the deduplicated link-check set for both default and explicit `--files` calls. Retain its structural-field check as well. One positive case and a negative missing-link case with explicit `--files` should protect this orchestration boundary. Preserve the explicit disclaimer that file existence does not prove source accuracy, complete design, executable acceptance or human approval.

## What remains correctly routed

- All181 unique requirement IDs, exact requirement text, stored text-hash fields, introduction phases and full-completion phases still agree between frozen requirements/coverage and the audit matrix. A lightweight read-only comparison passed. The matrix does not invent181 runtime-completion verdicts.
- The original P00–P14 acceptance dependencies and later full-domain exits remain authoritative. The current ledger retains the significant NPC, narrative, geology/ecology, maintenance/transport, history/planes/packages, catalogs/migration, campaign/scale, desktop/mobile and human acceptance obligations. Broad summary rows remain navigation into the full plan; previously noted omitted summary details such as P07 competition are not promoted into new findings in this pass.
- Fixed C#, OpenFantasy runtime naming with preserved Beta paths, typed atomic composition, independent permissions/accepted commitments, desktop-first1–4-player support, required later mobile, accepted concept direction versus native-motion approval, UI and upkeep supplements are reconciled without reducing scope.
- Source-backed runtime claims stay distinct from standalone contracts and future design. The compact handoff keeps original movement/history/raw-gift failures and empty-row versus populated-allowance limits visible. Recovery acceptance is bounded; it does not close full reliability, native device, art, phase or campaign gates.
- Both required documentation lanes are named in the lifecycle hook: plan/spec/phase decisions and timeless encyclopedia/reference. Documentation ownership, source/consumer review, no-impact reasoning, phase/adaptation criticism and pre-acceptance reconciliation remain explicit. Mechanical checks are not described as semantic or human approval.
- The prior empty-field and fenced-template checker fixes are present. Their earlier findings are not counted again. This review does not claim a complete CommonMark parser or exhaustive hostile-input coverage.

## Scope and limitations

Inspected current roadmap, compact handoff, documentation contract, Beta instructions and relevant commit hook; audit RESULT, Fable disposition and final validation summary; exact181 mappings; frozen phase/dependency/P02 exits; relevant current authority/source boundaries and checker call flow. Prior audited frozen phase content was reused rather than reloading the entire research archive. No runtime acceptance claims were independently re-executed. Root owns corrections, current source/artifact reconciliation and the next iterative pass.

## Documentation review

- **Plan/spec:** preserved all181 mappings and full phase scope; identified one new early P02 routing omission and stale next-work/prerequisite wording. No specification edit or future feature implementation performed.
- **Reference:** no runtime or timeless architecture change; reviewed synchronization ownership and actual checker file selection. Only this independent pass record was added.
- **Accuracy/completeness:** exact source/line references and minimal corrections above; all181 mapping fields compared; one in-memory checker counterexample observed. Scope and unexecuted runtime/human gates remain explicit.
- **Checks:** lightweight read-only mapping comparison and in-memory checker-function probe executed. No heavy check/provider/build/test. Final documentation checker execution after lead fixes is integration-lead work and remains unrun by this critic.
