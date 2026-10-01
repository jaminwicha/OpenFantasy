# Independent combat-retreat review

Owner explicitly requests Opus5.5 delegation. Use exact claude-opus-5-5[1m], requested Max,
one request, 1,800 seconds / $18 client estimate maximum. No model fallback, retry, subagents,
Git mutation, shell commands or code changes. You are an independent critic, not the implementation worker.
You are not alone in this checkout. All code/doc/evidence inputs remain read-only during review.
Only write REVIEW.md in your private task directory. First create that real deliverable to confirm access.

Review the coherent profile34 retreat delivery against the original ASSIGNMENT.md, actual source,
architecture and tests; Astra will integrate findings. Return actionable prioritized findings with
exact file/line references, triggering examples and minimal remedies or missing proofs. Distinguish
confirmed source defects from untested risks. Do not repeat generic design advice or require an ECS rewrite.
The 289KB delivery.patch includes new untracked source; current working tree equals that snapshot.
Use Read/Glob/Grep to follow relevant source dependencies; no need to read the whole research archive.

Read these entrypoints:
- AGENTS.md, Beta/AGENTS.md and current Beta/implementation/AGENT_HANDOFF.md.
- Beta/implementation/p02/combat-retreat/{ASSIGNMENT,DESIGN,RESULT}.md.
- Runtime docs/architecture/encounter-retreat.md, encounter-timing.md, encounter-actions.md,
  world-composition.md and relevant code references.
- Private inputs/delivery.patch and source-map.json; parent-AGENTS.md.
- Relevant new tests under Core.Tests/{Encounters,Worlds}/Retreat and Hosting.Tests/{Simulation,Client}/Retreat.
Use paths relative to Beta/Runtime for the Runtime references above.

All eleven final modes have passing reports exactly matching current sources/assemblies, and 571 old
files/23 frozen specs/original save/assets/predecessor staging are preserved. That is not acceptance.
The implementation worker explicitly omitted:
1. Actual process interruption at retreat publication (only graceful resume exists).
2. Real checkpoint/archive interaction across retreat (code path but no qualification).
3. Native trade after retreat (only travel; managed trade is separate).
4. Missing refusal tests for lost locomotion, AllyRemains, changed final ContactRemains, controller loss mid-route.
5. First-open wheel can capture previous frame's unavailable Retreat; probe closes/reopens to work around it.
Call these required omissions out compactly, but spend most review on additional correctness/design holes.
Do not claim any of them waived or passed. No new test execution granted; source review only.

Concentrate on:
- Full Wait, exact due-boundary sample costs, repeated/blocked samples, surviving recovery debt and pacing exposure/carry.
- Ground/body/current controller/occupancy/fluid/support/world admission at selection AND each continuation;
  all exit outcomes/cancelled queued actions with actor, command and episode identities retained.
- Sole ally/two positive sides/bystander boundaries, player/NPC same admission, final contact reevaluation,
  no actor loss, disguised teleport, free escape, HP reset or hidden automatic attacks.
- Canonical owner and codec bounds, malformed/version-mismatched roots/actions/steps, deterministic retry,
  old profile meanings/bytes and history, archive/journal cuts and replay. Historical proofs not replaced by fresh worlds.
- Native advisory captured-state staleness, actual event-driven controls, first-frame choice, cancel/confirm,
  truthful route timing/cost and unavailability, no authority access from render callbacks.
- Potential regressions: new LocalWorldView.Retreat null serialization on old public views; LastProgress/record
  of any actor shown to current participant; cost text sums delays but remaining exit debt is one sample.
  These are leads to evaluate, not pre-decided findings.
- Architecture docs describe implemented semantics accurately; broader party/pursuit/MP remain full-plan obligations.

REVIEW.md: compact verdict; severity-ranked defects; required missing proofs; accurate strengths/boundaries;
any consequential model consistency opportunities backed by actual paths. No cosmetic nitpicking as blockers.
Return concrete findings even if budget expires. Do not repair code or automatically request another model.
