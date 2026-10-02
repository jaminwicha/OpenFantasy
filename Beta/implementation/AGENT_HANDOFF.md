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

The [fresh player roster](p02/player-roster/RESULT.md) is qualified at
`codex/milestone-player-roster-20261002`, based on native-player-session milestone `4a9a2ad2`.
One-to-four production humans have separate controls/facets and finite starter tools; existing NPCs,
resources and solo bytes remain intact. Explicit lifecycle catalog v2 admits the twelve four-human
opportunities within joint byte/count limits. The create-only operator refuses save replacement.
Managed combat requires physical party assembly and each human's own Wait choice. Four actual TLS
clients move, pause, receive fresh observations, reconnect and reopen authenticated history.

[Final checks](p02/player-roster/FINAL_CHECKS.json): build and 2,768 distinct selected cases pass,
including two fresh single-client Godot process cases; Windows ACL is unrun on macOS. Independent
native critique led to joint capacity protection and a real paused-observation barrier. All 653 old
tests/fixtures, 23 frozen specs and exact unrelated predecessor staging remain intact. Architecture
docs are current. The broad exploratory Hosting run timed out; its archived-query failure passed
unchanged in isolation and Networking, but the cause and production latency remain unproven.

Next: four concurrent native combat/reconnect, construction completion with the larger roster and
actual v2 archive rollover/reopen. Recorded disconnect/controller handoff and all full campaign,
platform, production, art and human gates remain open. No provider request occurred. The test-family
selection lesson is recorded here and in ecosystem commit `542c46d`, with three skill copies synced.

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
Native Codex workers and critics also consume Astra capacity; do not report them as Claude offloading.

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

## Player networking work

The [transport checkpoint](p02/player-networking/RESULT.md), commit `6f3a891`, supplies versioned
bounded local/WebSocket envelopes. The [player admission checkpoint](p02/player-admission/RESULT.md)
adds host-issued credentials and exact world/branch/profile/admission/package handshake pins, bound to
actual human player ports. Clients cannot create control by supplying identity claims. Expiry, rotation,
revocation, duplicate joins, bounded pending handshakes and cleanup are covered; new ports remain unready.

[Final checks](p02/player-admission/FINAL_CHECKS.json): solution build, 39 network cases (18 retained
transport + 21 admission) and all 9 prior shared-player cases pass on exact current source/binaries.
Real loopback TCP/WebSocket admission is exercised; HTTP Upgrade/TLS is not. Two independent critic
findings have retained red-before-green tests: transport-close failure must still close the actor port;
a host stopped during backpressured Welcome cannot report successful admission. The affected timeless
architecture references are current; original tests/specs and predecessor staging remain exact.

Qualified scope is preserved at `codex/milestone-player-admission-20261002`. This is a bounded connection
checkpoint, not native multiplayer or a repetition of the earlier whole-game qualification. The
subsequent routing checkpoint below carries the next active dependency; full network delivery remains open. The full design and all earlier open gates remain authoritative.
No provider call was made while the account spending-limit failure holds.

## Gameplay message routing

[Player command routing](p02/player-command-routing/RESULT.md) binds authenticated channels to real
readiness, leased movement and world actions. Pending actions leave the receiver responsive; original
canonical receipts are sent after commit, with separate bounded Busy/preflight refusal. Slow clients
have bounded output/work and cannot hold the host commit path. Actual response loss followed by real
host reopening returns the same receipt without double spending; a loopback WebSocket action is proved.

[Final checks](p02/player-command-routing/FINAL_CHECKS.json): solution build and 69 affected passes,
including all 39 previous network cases, 21 new routing cases and all 9 shared-player cases. Independent
review led to paused-action closure, response-capacity and input-rate tests. The original fixture-cleanup
failure and lead shutdown-ordering correction remain recorded. Current source/binaries, protected tests/
specs, unchanged predecessor staging and architecture links are verified. Qualified scope is preserved
at `codex/milestone-player-command-routing-20261002`; no broader/native/release acceptance is inferred.

Next: participant-safe regional observations/recovery; separate authenticated trade inquiries and
quote/proposal inspection; HTTP/TLS listener; actual Godot clients and solo envelope parity; native
1–4-client validation and recorded disconnect/reconnect/controller handoff. The full campaign and all
unclosed gates remain in force. No provider request was made while its spend-limit failure holds.

## Player observations

[Player observations](p02/player-observations/RESULT.md) is qualified as
`codex/milestone-player-observations-20261002`: complete typed regional views and captured action terms,
bounded visible-revision deltas/full recovery, atomic replica publication, explicit subscription and
bounded output work. Actual host actions/receipts and regional views cross local and real loopback
WebSocket transports without exposing private world commit counters. Default diagnostic JSON is intact.

[Final checks](p02/player-observations/FINAL_CHECKS.json): solution build and 237 distinct affected cases
pass, including 87 new observation cases, all 60 prior networking and all nine shared-player cases.
The independent critic reproduced an admitted 4,096-lot overflow; lossless encoding now delivers that
complete view over both transports. Wire cap remains 900 KiB; expanded JSON has a new explicit 16 MiB
budget and bounded serialization/decompression. Maximum composite-world coverage and production
memory/latency remain open. [Review](p02/player-observations/CRITIC_REVIEW.md) retains the red regression,
focused follow-up and precise cadence contract. All original failures, 645 protected files, 23 specs,
93 checked architecture links and exact predecessor staging are recorded in the packet.

Pre-change checkpoint `codex/checkpoint-before-player-observations-20261002T082210Z` preserves
`cc073fd`; WIP checkpoints `codex/checkpoint-observations-before-packing-20261002T084343Z` (`bf00a142`)
and `codex/checkpoint-observations-before-capacity-20261002T085257Z` (`e3e74af2`) preserve successor
edits/evidence before format adaptations. The source/binary-pinned final results claim this bounded
boundary only; no new native/full-game/platform/human qualification or provider request is inferred.
Next: authenticated trade inquiry/inspection replies, HTTP/TLS, actual Godot clients and solo envelope
parity, native 1–4-player proof, and journaled disconnect/reconnect/controller handoff.

## Authenticated query checkpoint

[Player queries](p02/player-queries/RESULT.md) expose shopper inquiry submission/cancellation/status
and participant-only paid-quote/material-proposal history through authenticated local/socket channels.
Requests carry no controller claims; typed results bind scope, kind and subject. One independent
query slot, bounded output and joined teardown preserve controls while actual history work is pending.
Merchant offers and separate durable acceptance remain unchanged. Real archived participant/outsider
cases and real authority-lock backpressure/closure are proved. Historical replay still serializes
with the host; no production latency/scale claim is made.

[Final checks](p02/player-queries/FINAL_CHECKS.json): current-source build and 228 distinct affected
cases pass (188 networking, 31 retained trade/history, 9 shared), including all prior 147 networking
cases and 41 new cases. Independent native critique found no actionable defects; initial test-fixture
failures remain retained. All 651 protected test/spec files and the starting predecessor staging are
unchanged. Timeless architecture references are updated. Checkpoint before edits:
`codex/checkpoint-before-player-queries-20261002T092308Z`; verified milestone:
`codex/milestone-player-queries-20261002`. No provider request occurred after its terminal limit.
Next: HTTP/TLS/provisioning, actual Godot network clients and solo envelope parity, native 1–4-player
proof, and journaled disconnect/reconnect/controller handoff. Full design and unclosed gates remain.

## HTTP/TLS player hosting checkpoint

[HTTP/TLS hosting](p02/player-http-host/RESULT.md) is qualified on macOS as
`codex/milestone-player-http-host-20261002`: reusable net8 Server adapter, existing net10 dedicated
composition, exact saved cargo admission, real package fingerprint and protected create-only player
invitations. Actual TLS requests own their complete admitted session; joined shutdown preserves the
world owner. Application-owned signals avoid competing ASP.NET process handlers. The operator guide
and timeless architecture references are current; Core/Hosting/Godot remain independent of ASP.NET.

[Final checks](p02/player-http-host/FINAL_CHECKS.json): build and387 distinct passes (server33,
network188, content156 plus saved-journey audit1, shared9), with exact current source/binaries. The
original broad content invocation stays incomplete; its one missing fixture case passed separately
against pinned original exports. All633 earlier tests/fixtures,23 specs and exact predecessor staging
remain unchanged. Windows inherited-permission regression is explicitly unrun on this Mac.
Independent critique found/followed up response-timeout isolation and Windows credential protection;
all failures and fixes remain recorded. No external provider request occurred.

Pre-change ref preserves6a0d36f; review-fix WIP ref preserves7b3a105 with partial verification. The
accepted milestone preserves this bounded scope only. Actual Godot clients/solo envelope parity,
native1–4-player validation, recorded disconnect/reconnect/controller handoff, Godot listening-host
export/runtime qualification, Windows/Linux and all full phase/production/human gates remain open.

## Player client checkpoint

[Player client](p02/player-client/RESULT.md), preserved as
`codex/milestone-player-client-20261002`, owns validated startup/first view, default-trust TLS,
bounded ordered input, pending-action deduplication, exact reply correlation, recovery and joined shutdown.
Build and244 distinct affected cases pass (server56 including23 new; network188), retaining every prior
case. Windows DACL execution remains explicitly unrun on Mac. [Final checks](p02/player-client/FINAL_CHECKS.json)
pin source/binaries and644 old tests/fixtures,23 specs and exact predecessor staging. One native critic
found no concrete defect; both coverage recommendations are proved. Initial compile failures remain.
No provider call occurred. Native agents consume Astra capacity, not Claude credits.

This is protocol ownership, not Godot network play. [Design](p02/player-client/DESIGN.md) records the
next prerequisites: authoritative run status, content-bound presentation bootstrap and bounded Waiting
inquiry refresh. Preserve captured offers and exact retry identity; no private-state or regenerated-genesis
shortcut. Native1–4-player proof, solo envelope parity, recorded disconnect and all wider gates remain.
Pre-change checkpoint preservesf2ec8674; work remains on `codex/runtime-foundation`.

## Next dependency and remaining scope

The [native participant binding](p02/native-player-session/RESULT.md) is preserved as
`codex/milestone-native-player-session-20261002`. The ordinary Godot game joins the actual TLS host,
receives admitted presentation/status and submits its unchanged immutable offers. Local and remote
session owners are distinct; client closure preserves the host. Startup now reads initial window focus.
The remote adapter refreshes inquiries with a bounded foreground priority, and pause labels state that
other players may continue. Architecture and launch instructions are current.

[Final checks](p02/native-player-session/FINAL_CHECKS.json) record 446 distinct passes, all prior 56 server,
188 networking and 167 client cases, and five fresh Godot processes. Two prove real network walk/talk/
close and saved receipts; the rendered route also proves unfocused startup. Three freshly prove the
unchanged trade-boundary refusal/recovery gates. Original stale-offer/timeouts, missing-manifest runs
and obsolete retained boundary projection-hash failure remain preserved. All 649 older test/fixture
files, 23 specs and predecessor staging are unchanged. Windows ACL remains unrun on Mac. One native
critic's focus finding and inquiry coverage recommendation are addressed. No external provider call.
The pre-change checkpoint preserves ed0e590b; working branch remains `codex/runtime-foundation`.

Next: concurrent native 1–4-player interaction and waiting/combat semantics, solo envelope parity,
recorded disconnect/reconnect/controller handoff and adverse transport qualification. Single native
connection and managed shared-player evidence do not substitute for those gates. Status and view
remain independent publications; there is no atomic pause barrier or implicit action retry.

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
