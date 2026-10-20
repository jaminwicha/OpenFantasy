# Measured efficiency levers, 17 October 2026

**Status:** adopted as doctrine by owner direction on 17 October 2026. Three rules enter
[AGENTS.md](../../AGENTS.md) and the maintained verification skill. **No test, deadline, evidence-retention
or documentation obligation is reduced.** The October 3 [efficiency disposition](../EFFICIENCY_AUDIT_DISPOSITION.md)
and its rejections of universal caps, profile freezes and bulk evidence migration stand.
**Scope:** one day of actual lead-session telemetry for this project, the day's commits and native runs, and
the automated ecosystem review. Numbers are pinned in [measurements.json](measurements.json).
The shareable instruction for the current lead is [ASTRA_PROMPT.md](ASTRA_PROMPT.md).

## What was measured

| Lead session on this project, UTC day | Value |
|---|---:|
| API calls | 1,064 |
| Average input context per call | 155,317 tokens |
| Maximum input context per call | 244,421 tokens |
| Input tokens, 98% cached | 164,042,380 |
| Output tokens, of which reasoning | 741,637 / 308,419 |
| Tool output returned into context | 5.7 MB |
| Distinct files read | 439 |
| Files read more than once | 182 |
| Repeat reads | 462 |
| Reads of the roadmap, handoff, instructions, milestone doctrine | 30, 17, 13, 13 |

| Day's output | Value |
|---|---:|
| Commits | 26 |
| Production, test, documentation lines | 4,209 / 6,484 / 4,662 |
| Evidence lines, logs and JSON | 126,306 |
| Full native expedition runs, failed | 4, 3 |
| Failed run durations | 122, 239 and 593 seconds |
| Passing run duration | 672 seconds |

Input context exceeds output by about 221 to 1. That is roughly 10,700 input tokens per hand-authored
line against 48 output tokens per line. Tool output is not the driver; it was 5.7 MB for the day. The
driver is the number of round trips multiplied by the context each carries on a session resumed since
8 September, whose lifetime input is 2.03 billion tokens. About half of all file reads were repeats, and
the four plan and instruction files alone were read 73 times.

The three failed native runs died on an authored diagonal final route segment that lawful movement
rejects and on a runner that reported a fatal pipeline exception with zero test cases. Both are
preparation failures, not production defects, and each cost two to ten minutes before the failure
surfaced. The passing run was unaffected and its evidence stands.

The automated ecosystem review's telemetry was correct: no repository in the workspace recorded a
commit in the seven days before its 04:18 run. Its narrative was hardcoded prose in the generator with
stale project figures that the telemetry does not contain. The generator is corrected so every stated
number derives from the telemetry file; it proposes no rules.

## Rules adopted

1. **Session and context.** Start each work packet from the compact handoff in a fresh or freshly
   compacted session. Read the instructions, handoff, roadmap and milestone doctrine once per packet;
   keep working notes in the packet record rather than re-reading. Batch bounded reads and checks into
   one command where their outputs are small. Record the packet's API call count and average per-call
   context in its result when the host exposes them.
2. **Cheap preflight before long runs.** Before any native, storage or journey run longer than the
   focused-test budget: validate authored fixture geometry and routes against the current
   lawful-movement rules; confirm the exact filter discovers at least one case so a zero-case report is
   impossible; confirm the build and binary pins match the intended source. Classify every failure as
   fixture, runner or production before any rerun. Never retry automatically. Retain failed runs exactly
   as now. This adds a check; it removes none.
3. **Telemetry-derived review.** Efficiency claims cite a measured record. An automated review whose
   narrative contradicts its own telemetry is not evidence. The ecosystem auditor now derives every
   number from its telemetry and proposes no doctrine; candidate rules go through the affected project's
   own disposition.

What is deliberately not changed: the documentation doctrine and its same-change review, the native
probe deadlines and assertions, evidence retention, the full acceptance gates, the no-orchestration
steering, and the rejected proposals from October 3.

## Where it changed

- [AGENTS.md](../../AGENTS.md): dated owner steering paragraph under "Verification and milestones".
- [Efficiency disposition](../EFFICIENCY_AUDIT_DISPOSITION.md): dated addendum.
- [Handoff](../AGENT_HANDOFF.md) and [roadmap](../CURRENT_ROADMAP.md): one pointer each.
- Maintained verification skill in the sibling ecosystem checkout: a "Session and long-run cost"
  paragraph, synchronized to the three installed copies through the maintained plan/apply/verify sync
  with a dated manifest and validation record.
- Ecosystem `tools/project_efficiency_auditor.py`: report narrative derived from telemetry only.

## Documentation review

- **Plan/spec:** no requirement, phase exit, gate or catalog target changes. The roadmap's working
  discipline row and the handoff's start-here item link this record. The October 3 disposition gains a
  dated addendum; its decisions are unchanged.
- **Reference:** no impact. These are process rules in `implementation/` and the shared instructions;
  no ownership, interface, data flow, canonical bytes, timing, persistence, generation, input or
  presentation contract changes, so no `Runtime/docs/architecture/` page is affected.
- **Accuracy/completeness:** the lead parsed the actual lead-session rollout for entries timestamped
  on the UTC day, counted commits and line classes from git, read the native journey evidence and
  failure files, inspected the auditor's generator source and verified its zero-commit window against
  five repositories. Omissions retained explicitly: no subscription or dollar cost, no claimed
  percentage saving, no measurement of other leads' sessions, and the UTC day boundary excludes work
  after 19:00 local. The telemetry file and the day's commits are the only sources for the counts.
- **Checks:** `python3 Beta/tools/check_documentation.py --work-record Beta/implementation/efficiency-levers-20261017/RESULT.md --files Beta/AGENTS.md Beta/implementation/AGENT_HANDOFF.md Beta/implementation/CURRENT_ROADMAP.md Beta/implementation/EFFICIENCY_AUDIT_DISPOSITION.md Beta/implementation/efficiency-levers-20261017/RESULT.md`
  from the repository root, `git diff --check` on the authored pages, and the ecosystem sync tool's
  `verify` on the skill manifest. Results are recorded in [measurements.json](measurements.json).
  Document-only change; no runtime suite, benchmark or Godot launch is claimed. Independent review is
  not claimed.
