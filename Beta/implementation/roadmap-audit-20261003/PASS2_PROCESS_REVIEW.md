# Pass 2: workflow and documentation-check effectiveness

**Three new corrections are warranted.** Two are reproducible false acceptances in the documentation
checker; one is a stale next action in the new authoritative roadmap. This is not a no-findings pass.
The earlier scope audit does not need to be repeated to fix these issues.

Reviewed project revision: `df60675240d7800dda739861d3b81358c50b6837`. The checker hash was
`2564f0b5b4df2a8663aa690257112b16c3af713e5e7054ba58077cd57a5e77ea`. The roadmap, maintenance
contract and handoff had no working-tree changes when inspected. Review covered their current
contracts, the prior progress report/metrics, audit result, retained six checker cases and relevant
skill hooks. No game build, runtime test, external provider request or source edit was performed.
The only written file is this report. Focused diagnostics used `python3 -B` and in-memory content,
so they created neither fixtures nor Python bytecode in the repository.

## New findings

### P2 — The work record's evidence links are never checked

[check_documentation.py](../../tools/check_documentation.py) lines 62–70 assemble the link-check
file set separately from `--work-record`. `check_record` checks four fields, but the work record is
not added to `files`. Unless the caller also names it in `--files`, its local links are omitted.
The documented default command therefore checks the 91 live pages but not the result whose
evidence it is supposed to assess.

Observed diagnostic: substitute an in-memory work record containing all four real review fields
and the following broken link, invoke `main` with that record and the existing Runtime/docs README
as `--files`, and observe calls to `check_file`:

```markdown
[Required evidence](definitely-absent-proof.json)
```

Result:

```json
{"case":"work_record_broken_evidence_link","exitCode":0,"accepted":true,"workRecordLinksVisited":false}
```

Impact: a result can claim a successful documentation gate while its own required-evidence link
is broken. This falls inside the checker's stated local-file-link scope; semantic review is not a
substitute for checking the omitted file.

**Minimum correction:** add the resolved work record to the de-duplicated link-check file set
unconditionally. Preserve its separate field checks. Add a positive real-evidence case and a
negative missing-evidence case using the ordinary command path, including when `--files` is explicit.
Do not recursively validate every historical artifact or broaden into runtime verification.

### P2 — Hidden/template-only text can still satisfy the mandatory review

`prose`, at lines 13–15, strips only fences starting at column zero whose closing delimiter exactly
matches the opening length. It does not exclude HTML comments. `check_record` consequently sees
review headings and fields that are not rendered as the actual work-record review.

Three independently exercised cases all returned an empty error list:

| Entire work record | Expected | Observed |
| --- | --- | --- |
| HTML comment containing the review heading and four fields | Reject absent rendered review | Accepted |
| Three-space-indented Markdown fence containing the review, with a matching indented closer | Reject template-only review | Accepted |
| Three-backtick opening fence, review body, four-backtick closing fence | Reject template-only review | Accepted |

The indented and longer-closing fences are valid Markdown fences. The previous plain-fence
counterexample does not cover them. A normal prose review remained accepted in the same diagnostic.
These are mechanical structure defects, not a request for the checker to judge whether prose is
substantively correct.

**Minimum correction:** strip comments and recognize the fence forms supported by the documents'
Markdown renderer, including up to three indentation spaces and same-character closers at least
as long as the opener. An unterminated fence must not turn its contents into a review. Retain a
real prose review after a fenced example as a positive contrast. Use a small deterministic scanner
or an already available parser; adding a new large documentation framework is unnecessary.

A related false rejection was reproduced: the broken-link syntax above, shown inside an inline
backtick code span, is parsed as a live link by `check_file` and rejected. The example does not
render a link. Exclude code-span contents from link extraction as part of the same small lexical
correction, with a live missing link alongside it as the negative contrast. This report uses a
fenced example to keep the deliberately absent target out of its own real link set.

### P2 — The authoritative next sequence asks to finish an already completed checkpoint

[CURRENT_ROADMAP.md](../CURRENT_ROADMAP.md) line 45 says delegated paid-work recovery is qualified
and its bounded checkpoint is complete. The compact handoff identifies committed `cbcd7565`.
However, the roadmap's first next-execution step, lines 118–120, still says to close that bounded
qualification; lines 50–52 likewise refer to the next recovery result. This is new stale status
introduced in the current reconciliation, not an old archived narrative.

Impact: the new current-status authority can send the next worker back into an already closed
bounded assignment instead of selecting the integrated journey. It also blurs the distinction
between completed bounded recovery and its explicitly unqualified broader cases.

**Minimum correction:** mark the bounded result as completed in the sequence and make the
integrated-journey specification the next action, preserving populated allowance, reacquisition,
listener/socket-death and reliability obligations with their genuine dependencies. Do not imply
those remaining cases passed or delete them to make the sequence shorter. A source-based document
cross-check suffices; no game regression is warranted for this correction.

## What this pass does not recommend repeating

The compact handoff and explicit P02 table address the first audit's main status-navigation issue.
They are useful corrections. This pass does not reopen already documented future game, scale,
platform, art or human obligations merely to generate another recommendation. The 38-profile
correction resolves my earlier arithmetic error; it is not a new issue. Nor does the current
evidence justify a second status dashboard, a full Markdown implementation, or another broad test
sweep. The 91-page default invocation passes, but it cannot detect the new cases above without
focused negative checks.

The documented hook correctly states that mechanical acceptance is not semantic, anchor, runtime
or human approval. That distinction should remain. The corrections here make its narrow promise
reliable; they do not expand the promise. Review records should retain the exact counterexamples
and corrected outcome, not only an increased count of passing checker cases.

## Compact reusable audit flow and stopping rule

I also inspected the lead's in-progress ecosystem reference
`skills/game-verification-loop/references/campaign-audit.md`, hash
`ea6dcdb219c7148f86941b99a09a635194d9358e29316bc70f89263dce4dae43`. This is an observation of a
working overlay, not acceptance of its eventual commit or installed-copy synchronization. Its
flow already addresses the owner's requested iterative audit. No additional defect in that flow
was established during this pass.

The portable flow can remain compact:

1. At an owner-requested deep audit, significant milestone/phase adaptation, or sustained multi-day
   work with drift signals, pin the objective, accepted revision/overlay and full phase/product scope.
   A four-day-running goal is a reason to inspect lost context, not four days of measured work or
   automatic authorization for a paid review, schedule or full archive reread.
2. Preserve a checkpoint before substantial corrections. Read the current roadmap/handoff and only
   relevant original contracts/evidence. Compare actual player entry, integrated outcomes and the
   complete current-phase exit; inspect process/documentation problems using concrete examples.
3. Keep one finding ledger in the existing audit record: evidence, impact, minimum correction,
   owner, disposition and verification. Distinguish current-scope defects from unchanged future
   product obligations, rejected inferences supported by counterevidence, and genuinely blocked work.
4. Fix accepted current-scope findings between independent passes. Verify changed contracts and
   neighboring consumers with affected checks. Give the next reviewer the accumulated diff, previous
   dispositions and exact new evidence, not merely a checklist stating that all comments were handled.
5. Stop only when an independent pass finds no new actionable issue **and** every accepted
   current-scope finding is fixed and verified. A current defect cannot become a future obligation
   simply to make this predicate true. A rejected finding needs counterevidence. Missing required
   evidence or an unavailable decision means incomplete/blocked audit, not convergence. Unchanged
   full-game obligations may remain at their legitimate phases and are not a reason for infinite
   audit passes. Any correction after the final reviewed boundary receives affected reinspection.

This rule permits a useful bounded audit to finish without claiming a finished game. It also
prevents “no new comments” from laundering old unresolved current-scope defects. The current draft
already says this; retain it rather than adding another overlapping governance layer. After fixes,
a focused independent pass should inspect the combined checker and status delta. Required human,
native or platform proofs remain separate; audit agreement cannot sign them.

## Diagnostic method and limits

The ordinary command executed successfully:

```sh
python3 -B Beta/tools/check_documentation.py --work-record Beta/implementation/roadmap-audit-20261003/RESULT.md
```

Observed output: `accepted: true`, `filesChecked: 91`, no errors, with the stated narrow scope.
The focused harness loaded the checker through `importlib.util.spec_from_file_location`. It called
`check_record` with a minimal in-memory object exposing `is_file()` and `read_text()` for the four
review-body contrasts. For the work-record link case, `unittest.mock.patch` replaced `Path.read_text`
only for the selected real record path and observed `check_file` calls while invoking normal `main`
with explicit arguments. The tested body was:

```text
## Documentation review

- **Plan/spec:** No plan impact; source contract unchanged.
- **Reference:** README is the affected guide.
- **Accuracy/completeness:** Compared the guide and current source.
- **Checks:** Focused mechanical check completed.
```

The three wrappers described above each yielded `accepted: true`; the same body without a wrapper
was a positive control. This establishes structure/link false acceptance; it is not a claim that
the current audit RESULT uses a hidden review or actually contains a broken link. No anchor checker,
whole-reference semantic audit, full Markdown parser conformance test, runtime qualification or
subscription-cost study was performed. The lead owns corrections and final convergence.
