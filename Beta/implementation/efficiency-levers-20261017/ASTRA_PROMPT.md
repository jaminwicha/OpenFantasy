# Instruction for the integration lead, 17 October 2026

Owner direction, effective now. Read `Beta/AGENTS.md`, which has a new dated paragraph under
"Verification and milestones", and `Beta/implementation/efficiency-levers-20261017/RESULT.md` once.
Nothing here reduces a test, deadline, evidence-retention or documentation obligation. The gates,
the same-change documentation review and the retained-failure rules are unchanged.

## What today's telemetry showed

Your session on this project today made 1,064 API calls at an average of 155k input tokens each,
about 164M input tokens for 742k output tokens. Tool output was only 5.7 MB. Half of all file reads
were repeats: the roadmap 30 times, the handoff 17, the instructions 13, the milestone doctrine 13.
Three of four full native expedition runs failed on preparation, an authored diagonal final segment
and a runner with zero discovered cases, costing about 16 minutes before the passing 672-second run.

## Do this from the next packet onward

1. **Start each packet in a fresh or freshly compacted session** from the compact handoff. Read
   `Beta/AGENTS.md`, `AGENT_HANDOFF.md`, `CURRENT_ROADMAP.md` and `GIT_MILESTONES.md` once per packet.
   Keep what you need from them in the packet's own result or scope file and work from that; do not
   re-read them mid-packet unless they changed.
2. **Batch bounded reads and checks** into one command where the outputs are small. One round trip
   costs the full context; a read of a file you already hold costs the same as new work.
3. **Before any native, storage or journey run longer than the focused-test budget, preflight:**
   validate the authored fixture geometry and routes against current lawful movement; confirm the
   exact test filter discovers at least one case so a zero-case report cannot masquerade as a run;
   confirm the build and binary pins match the intended source. Classify any failure as fixture,
   runner or production before any rerun. No automatic retry. Retain failed runs as now.
4. **Record in each packet result**, when available from the rollout: API calls, average per-call
   input context, and the number of long runs with their fixture/runner/production classification.
5. **Cite measured records for any efficiency claim.** The ecosystem auditor now derives every number
   from its telemetry and proposes no doctrine. Do not act on narrative that its telemetry does not
   support.

Continue the current PLAN-STATE preparation and the P02 closure table exactly as scheduled in the
roadmap. The maintained verification skill's installed copies already carry the matching paragraph.
Report friction with these rules honestly in the packet result rather than silently dropping them.
