# Documentation synchronization and review

This contract applies to each coherent implementation, design, tooling or acceptance change. It
keeps the intended game, delivery plan and source-based reference aligned without making routine
work reload the research archive. The integration lead owns closure; a delegated author owns the
affected documentation with their assigned code. Documentation is part of acceptance, not a later
cleanup task.

## Distinct authorities

| Document | Owns | Does not own |
| --- | --- | --- |
| Frozen `spec-v1_1/` | Preserved design baseline, 181 requirement identities, full phase and validation contracts | A live implementation-completion claim |
| [Current roadmap](CURRENT_ROADMAP.md) and linked living supplements | Binding later decisions, phase dependencies, integrated scope, remaining exits and exact evidence | Permission to silently delete a baseline requirement or weaken a gate |
| [Shared handoff](AGENT_HANDOFF.md) | Next bounded assignment, ownership, latest accepted boundary and open failures | A second copy of every result or the architecture encyclopedia |
| `Runtime/docs/architecture/` | Present-tense ownership, interfaces, flow, invariants, limits, rationale and source bindings | Dates, phase progress, temporary tasks or unsupported claims that a design is implemented |
| `Runtime/README.md` and `Runtime/docs/` guides | Actual launch paths, controls, toolchain, operator/developer usage | An implicit claim that the default launch exposes every supported fixture |
| Work result/review records | Exact revision/dependencies, checks, defects, decisions and acceptance scope | New game rules hidden in a test packet |

Runtime bindings, standalone domain contracts and future design obligations remain explicitly
distinguished in the reference. A design obligation can be timeless without being implemented.
All archived requirements and subsequent additive decisions retain traceability.

## Hooks in the work lifecycle

1. **Select work:** read the current roadmap and handoff, then the affected specification/supplement
   and reference subject. Identify the user journey and requirement/phase exit served. Check later
   owner steering before treating a frozen decision as current. A contradiction gets an explicit
   disposition; do not quietly choose whichever text is convenient.
2. **Prepare a substantial change:** preserve the required checkpoint. Name affected state owners,
   interfaces, commands, persistence/history, timing, knowledge, generation, content and presentation
   contracts. Include relevant cross-system callers/consumers and launch/run instructions. Assign
   documentation ownership with code ownership; this may be one short sentence in the existing task.
3. **Freeze the coherent change:** update affected reference pages and guides from actual source.
   Reconcile the living plan/decisions if scope, order, prerequisites, acceptance, catalog or user
   behavior changed. Link the baseline requirement and retain an explicit supersession for changed
   decisions. Test-only evidence normally changes status, not architectural contracts. A justified
   no-impact entry is sufficient; cosmetic file churn is not required.
4. **Review before acceptance:** add a `Documentation review` section to the existing result or
   review, containing the four lines below. Run the documented mechanical check on affected live
   pages. Review statements against the actual source and bounded evidence, including dependent
   subjects and contradictory old summaries. Resolve inaccuracies before the milestone; label
   unresolved questions and leave the affected gate open.
5. **Commit/handoff:** include affected docs with their coherent implementation. Update the short
   handoff and the applicable roadmap rows when their status/dependency changes. Record verification
   and limitations once and link them. An unfinished preservation snapshot may retain unresolved
   documentation; it must be labeled WIP and is never an accepted milestone.
6. **Phase handoff or major adaptation:** an independent critic reviews the affected source/reference
   boundaries and the complete phase exit, including requirements outside the recent work queue.
   Reconcile catalog/migration coverage, real default launch, native/human/platform/scale evidence and
   earlier failures. Routine small fixes use local review; do not manufacture another full audit.

Use these four concise fields in the existing work record, with real paths/reasons:

- **Plan/spec:** affected requirement/decision/phase, updates, or reason for no impact.
- **Reference:** changed `docs/`/guide paths and affected dependent subjects, or reason for no impact.
- **Accuracy/completeness:** source/evidence inspected, actual launch/profile boundary, remaining
  omissions/contradictions and reviewer disposition. A link check alone cannot fill this field.
- **Checks:** actual document-check command/result and required independent review, or its explicit
  unrun status. Human usability/art and runtime acceptance remain separate.

## Accuracy and completeness bar

Check both directions: every changed contract has a reference, and every changed reference claim
has an actual owner/source or is labeled a design obligation. Include failure/permission/restart
behavior, clocks and units, complete identities, public/private knowledge and supported limits when
they matter. Inspect cross-cutting effects instead of assuming a local method rename is the whole
change. Examples must match actual commands and supported modes.

A pass count does not close a feature; a feature does not close a phase; a phase does not establish
production platforms or human acceptance. Reused evidence keeps its original dependency/scope
limits. Review the user-facing default separately from opt-in fixtures. Keep finite catalogs and
unresolved workload assumptions visible, rather than concealing them behind percentages.

The mechanical checker catches missing work-record fields, absent files and broken local Markdown
links. It cannot establish semantic truth, design completeness, executable behavior or human
approval. Its result is one input to the source-based review above. No global Git hook or unrelated
project configuration is changed by this contract.

## Efficient upkeep

Use one current roadmap and one compact handoff. Read historical packets only when their contract
or evidence is relevant. Preserve frozen specs, archives and failed evidence. Reuse a maintained
checker rather than creating a new packet-specific documentation verifier. Do not run runtime
regression for prose-only edits. Promote generally useful lessons to the maintained ecosystem skill
and synchronize reviewed installed copies with backups; keep game-specific decisions here.
