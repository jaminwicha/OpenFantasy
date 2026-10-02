# Current shared agent handoff

Updated: 2026-10-02 UTC. This is the current-state index; evidence and failure dispositions remain in
linked packets. The preserved specification is authoritative. Read only task-relevant contracts.

## Start here

1. [Implementation instructions](../AGENTS.md): ownership, stable `OpenFantasy` runtime names,
   modular object-oriented C#, preservation, verification and shared-desktop discipline.
2. [Runtime setup and controls](../Runtime/README.md) for builds/runs.
3. [Full plan](../spec-v1_1/IMPLEMENTATION_PLAN.md), [requirements](../spec-v1_1/REQUIREMENTS.json),
   [technical design](../spec-v1_1/TECHNICAL_DESIGN.md) and
   [baseline handoff](../spec-v1_1/IMPLEMENTATION_HANDOFF.md) for the next dependency.
4. [Living UI supplement](PLAYER_INTERFACE_AND_NAVIGATION.md) and
   [partial coverage](ui-management/COVERAGE.json) for interface work. These refine the baseline,
   without reprioritizing phases. Later owner naming instructions override source naming only.

5. [Architecture reference](../Runtime/docs/architecture/README.md): timeless ownership, source
   bindings and domain/design contracts. Read affected subjects, not the entire encyclopedia, for
   an implementation task. The [maintenance hook](../AGENTS.md#architecture-reference-and-maintenance-hook)
   requires same-change updates for architectural changes and a bounded source review at phase handoff.

## Core language assessment

The owner-requested [C#/C++ decision specification](CORE_LANGUAGE_CPP_ASSESSMENT.md) and
[source inventory](CORE_LANGUAGE_CPP_EVIDENCE.json) assess execution, in-process native versus IPC,
performance hypotheses, compatibility, effort, projected source size and conditional qualification.
Owner decision, 2026-09-20: retain C# for the core and application for simplicity. Native alternatives
remain research; no migration, custom native kernel or IPC worker is in the active plan. Preserve core
decoupling and address measured costs in C#. No comparative benchmark or new language gate is accepted;
the complete existing LANG gates remain in force. A language change requires a new owner decision.

## Architecture documentation

The [backfill record](architecture-reference/README.md) reconciles the reference with the retained
runtime. It separates executable domain contracts, actual application bindings and full design
obligations, especially history, Wait, local visibility and macro geography. Progress/evidence remain
here and in packets; frozen specs remain preserved. Documentation coverage does not close game gates.
Commit `2f91983`, preserved on `codex/milestone-architecture-reference`, records the reference and
project hook. Ecosystem commit `4e83086` records the portable hook and verified five-entrypoint sync.
All 123 reference links resolve; the bounded factual review corrections are retained in the packet.

## Current work and routing

Owner steering: use exact `claude-opus-5-5[1m]`, requested Max and finite limits when delegating;
Astra retains integration. The latest direct request reached the account monthly spending limit.
No automatic retry, fallback, account limit change or further provider request is pending.

The [native accepted-care increment](p02/native-accepted-care/RESULT.md) is qualified as
`codex/milestone-native-accepted-care-20261002`. From an unplayed full-health production world,
the native player walks, enters combat, incurs real defeat, acknowledges Wren's care, receives a
separate helper-owned finite-dose treatment, continues the same episode, and later completes/reopens
the saved outcome. Exact production genesis, journal commands/receipts, helper/dose ownership and
recovery are authenticated. No scripted damage, grant, encounter preparation or NPC suppression occurs.

Rendered review also repaired an obsolete treatment-pending HUD message: newly observed restoration
now announces the HP increase once, without changing the sprite cue or replaying old notices on load.
The architecture reference and README describe these presentation and evidence contracts.

[Final checks](p02/native-accepted-care/FINAL_CHECKS.json) record 2,354 distinct managed cases
(1,773 Core, 581 Hosting), covering every prior 2,348 case; fifteen fresh native processes; fresh
care/outcome/road/retreat authentication; retained-journal authentication; and twelve actual process
kills across both durability families. All 609 older tests/fixtures, 23 frozen spec files, protected
artifacts and exact unrelated 19-file predecessor staging remain intact. No gate or deadline changed.
Earlier outcome cases and prepared fixtures are inherited only by explicit dependency comparison.
Failed assembly reproduction led to fresh authority checks, not assumed binary equivalence.

The [prior outcome milestone](p02/encounter-outcome-integration/INTEGRATION_REVIEW.md), preserved at
`codex/milestone-encounter-outcomes-20261002`, separates human reading from world completion in fresh
profile35. Pending care holds completion; treatment resumes the same episode; injuries/resources/history
persist. Earlier independent critique, implementation corrections and all original failures remain there.

Earlier silent care/care-wheel timeouts remain unexplained; later passes do not establish their cause
or production latency/reliability. Background/injected-input evidence is not physical-device, production
art or human acceptance. The solo care journey has no nonempty prior readings; that contrast remains
managed evidence only.

## Delegation evidence and efficiency

[Latest delivery](p02/native-accepted-care/DELIVERY_RESULT.json): verified Opus5.5/1M, requested Max,
verified context, terminal monthly spending limit after 179.564 seconds. Only a design note was delivered;
no runtime code/tests. The $1.7358292 client list-price estimate is not billing or savings. Astra completed
this increment locally. Do not poll/restart terminal provider or verification sessions.

Earlier bounded assignments produced useful code; recent encounter continuation/integration deliveries
still required substantial Astra completion and repair. Net Astra credit savings remain unmeasured;
the latest request did not offload implementation. The [result's economics assessment](p02/native-accepted-care/RESULT.md#delegation-economics)
records the owner's question and the evidence. When access is available, prefer self-contained complete
deliverables and compact results; avoid repeated handoff loops and do not weaken acceptance gates.

[Upstream render-wait sync](p02/native-accepted-care/UPSTREAM_SKILL_SYNC.json) preserves bounded actual
post-draw observation. [Upstream efficiency sync](p02/native-accepted-care/UPSTREAM_EFFICIENCY_SYNC.json)
records source commit 9b8bcc3 and three installed targets: limit inconclusive binary reproduction work
by the cheaper affected verification alternative. No source-equality shortcut permits evidence reuse.

## Shared-player host checkpoint

[Shared player sessions](p02/shared-player-sessions/RESULT.md) is qualified as
`codex/milestone-shared-player-sessions-20261002`: up to four authenticated in-process ports share one
clock, authority and NPC scheduler. Inputs, private views/inquiries, readiness, ordinary fairness,
simultaneous combat, outcome readings, independent close/reconnect and saved recovery are proved.

The [final checks](p02/shared-player-sessions/FINAL_CHECKS.json) cover 2,363 distinct managed cases
(1,773 Core, 590 Hosting), including every prior 2,354 case and nine new shared-player cases; fifteen
fresh native processes; authentication of fresh journals and all six retained families; and twelve
actual process kills across both recovery families. Build, 26 focused, 34 outcome and 569 broad Hosting
cases pass. Broad elapsed time is 580.861 seconds under the unchanged 600-second bound. Core reuse is
supported by exact source and compiled-assembly identity. All 612 protected tests/fixtures, 23 frozen
spec files and exact unrelated predecessor staging remain intact.

The independent critic and unchanged compatibility tests exposed scheduling errors that the lead
corrected: observer starvation, live-port threat selection, care priority and stale receipt boundaries.
The original patient's matching stale offer receives at most one separate committed refusal immediately
after acknowledgment. Its durable revision closes that window across reopening; later requests cannot
starve the helper, and failed preflight cannot defer it. Core remains the owner of action legality.
The [review record](p02/shared-player-sessions/CRITIC_REVIEW.md) preserves all failures and dispositions.
The fresh rendered frame was inspected; prototype art/UI has no new human acceptance.

The [architecture reference](../Runtime/docs/architecture/hosted-player-connections.md) is current.
In-process closure grants no automatic human decision and is not a journaled network disconnect.
Network transport, recorded disconnect handoff and native multiple-client acceptance remain open.
No provider request was made while the account spending-limit failure holds.

## Next dependency and remaining scope

Native accepted treatment and approach/entry are now proved by the bounded journey above. Native
multiple-client readiness, no-survivor resolver reachability, lived profile34 migration and full
phase/production gates remain open. Managed two-human/repeated-episode journeys do not substitute for
native/session proofs. Follow the full plan before selecting the next unit; retain all world-composition,
custody, campaign, frontier, content migration, 1–4-player networking, art/device and human gates.

Before-change refs preserve 698778d and c74fc84; the WIP checkpoint
`codex/checkpoint-native-care-before-notice-20261002T041616Z` preserves 7dff05f before the HUD repair.
The new milestone is the acceptance pointer; the [result](p02/native-accepted-care/RESULT.md) links all
checkpoint, preservation and qualification records. Work remains on `codex/runtime-foundation`.

## Preserved earlier progress and full scope

The [previous handoff](AGENT_HANDOFF_20261002_OUTCOME_ARCHIVE.md) preserves all earlier milestone,
review, migration, delegation and failure records with their original relative links. It is an archive,
not a competing current assignment. Use the full plan and requirements linked above; no campaign, phase,
world-composition, art, device, networking or release requirement is removed by this smaller index.
