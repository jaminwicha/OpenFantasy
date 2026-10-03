# Current shared agent handoff

Updated: 2026-10-03 UTC. This index selects the next dependency; linked packets retain evidence,
failures and limits. The frozen complete specification remains authoritative.

## Start here

1. [Implementation instructions](../AGENTS.md): shared ownership, stable `OpenFantasy` runtime names,
   modular object-oriented C#, preservation, verification and shared-desktop discipline.
2. [Runtime setup and controls](../Runtime/README.md) and the maintained
   [architecture reference](../Runtime/docs/architecture/README.md). Read affected subjects and update
   their timeless contracts with architectural changes; keep progress and measurements in packets.
3. [Full plan](../spec-v1_1/IMPLEMENTATION_PLAN.md), [requirements](../spec-v1_1/REQUIREMENTS.json),
   [technical design](../spec-v1_1/TECHNICAL_DESIGN.md) and
   [baseline handoff](../spec-v1_1/IMPLEMENTATION_HANDOFF.md). No smaller checkpoint replaces them.
4. [Living UI supplement](PLAYER_INTERFACE_AND_NAVIGATION.md),
   [partial UI coverage](ui-management/COVERAGE.json) and
   [equipment/vehicle upkeep supplement](EQUIPMENT_AND_VEHICLE_UPKEEP.md) for applicable work.

## Current implementation boundary

[Bounded runtime scheduling captures](p02/runtime-scheduling-diagnosis/RESULT.md) add opt-in
test-process CPU/GC/pool counters without production changes. Two original server selections pass76
with the expected Windows-only skip; the isolated original four-player case also passes. No failed
sample reproduces. Captures show substantial process GC pauses in aggregate workloads, but cannot
attribute the prior failure or justify a scheduler rewrite. The predeclared repeats are exhausted;
resume full actor selection and retain the diagnostic for its next relevant qualification. The timing
gate stays open. Before-change ref `codex/checkpoint-before-runtime-scheduling-20261003T030339Z`
preserves `7ab81d44`; marker `codex/milestone-runtime-scheduling-diagnosis-20261003` records this scope.

[Movement delivery diagnosis](p02/movement-delivery-diagnosis/RESULT.md) captures an actually failed
four-player sample: accepted movement is first sampled at151.9644ms, beyond its unchanged150ms lease,
with no selection. The424.742125ms scheduling gap precedes tick serialization; lock acquisition is
0.003542ms. The traced baseline has75 passes, one movement failure and the expected Windows-only skip.
A warning-free build and six new codec measurement/scope/duplicate-name cases pass on verified
.NET10.0.12. Warmed encoding and decoding average3.5ms/5.6ms per real regional view; these averages
do not explain the live delay. Independent review caught and corrected the original .NET8 probe mismatch.
Runtime code is unchanged. CPU, runtime pauses and executor scheduling still need discrimination;
no speculative parser/scheduler rewrite, lease extension or closed latency gate is claimed.
Diagnostic milestone: `codex/milestone-movement-delivery-diagnosis-20261003`; before-change ref
`codex/checkpoint-before-movement-delivery-20261003T024353Z` preserves `b2bc3196`.

[Live host assignment custody](p02/live-control-presence/RESULT.md) binds internal acquisition to a
current, fully admitted transport and captured permission/expiry terms. Exact per-port witnesses,
revocation reconciliation and release on owner return/loss/disposal/startup/stop preserve home identity,
original controller episodes and accepted paid work. Autonomous selectors exclude effective owners.
This remains an internal prerequisite: public local/TLS/Godot input and views still address home actors.

The fresh warning-free build and 154 Hosting checks pass, including eight new decisive cases.
The uninstrumented server selection has 75 passes, one four-player movement timeout and one Windows-only
skip. One instrumented diagnostic passes76; it did not capture the failed request and does not close
that timing gate. [Final evidence](p02/live-control-presence/FINAL_CHECKS.json) retains both outcomes,
fresh full Core1,885 evidence, all752 prior tests/fixtures,23 specs and staging.
The initial inheritance check rejected changed stamped binaries despite identical Core source.
[Independent review](p02/live-control-presence/REVIEW.md) found no concrete runtime defect; an
established third-player custody contrast remains part of full selection qualification.
Bounded milestone: `codex/milestone-live-control-presence-20261003`; before-change ref:
`codex/checkpoint-before-live-control-presence-20261003T021941Z` at `0132b308`.

[Effective co-player command authority](p02/effective-co-player-control/RESULT.md) explicitly admits
fresh profile40/schema12 over the [durable consent foundation](p02/co-player-control/RESULT.md).
Active assignment, scoped current authorization and immutable command witnesses retain the real
submitting principal/session and operated actor without changing permanent inventory bindings.
Revocation/return/loss stop new input; actual accepted combat and retreat retain ordinary paid
resolution. Care acknowledgment remains separate from helper treatment. Raw/produced gifts, paid
exchange and rule-priced markets retain original attribution through actual archive and reopen.
Old offers cannot revive after regrant; returning owners can withdraw with their own terminal identity.
Exact prior submitter receipts remain distinct from new authority and private-history access.

[Independent critique](p02/effective-co-player-control/CRITIC_REVIEW.md) corrected impossible combined
acquisition chronology and public planner authorization widening. Authorized lower-level overloads
are internal to Core composition; standalone public planners remain owner-only. The
[timeless contract](../Runtime/docs/architecture/effective-control.md) and dependent references are current.

The [reservation ownership increment](p02/host-latency-diagnosis/RESULT.md) removes repeated outcome-ID
construction from profiles 16–18 forward excavation cursors, with independent decode ownership and
unchanged complete-world validation. A fixed real continuation workload allocates 10.6508% fewer bytes
with identical canonical roots; this is not a whole-game performance claim. Its retained warning-free build,
Core (1,885), complete storage (489) and uninstrumented server (76, one Windows-only skip) pass.
The storage aggregate completes in 332.081s under the unchanged 600s limit. The 17 focused co-player Hosting regressions also pass; [final checks](p02/host-latency-diagnosis/FINAL_CHECKS.json)
record 2,467 distinct passing cases, source/binary pins and original-test/spec preservation.

Earlier storage timeouts and server movement/startup failures remain in the effective-control packet.
The later passes do not establish their cause or production latency reliability. The reservation change
is outside the server fixture's world profile. No deadline, input lease or acceptance gate was relaxed;
Windows ACL, native control-selection and profile40 process-kill proofs remain open.
The optimization and evidence are preserved by `codex/milestone-excavation-reservation-reuse-20261003`.
Its pre-edit expected-red baseline is `53cd1085`, preserved under
`codex/checkpoint-before-reservation-reuse-20261003T0155Z`; the packet records exact scope and source pins.

Co-player implementation commit: `d76f4681`; [managed checkpoint](p02/effective-co-player-control/MANAGED_CHECKPOINT.json):
`codex/checkpoint-effective-control-managed-20261003T0108Z`. This is a recoverable bounded checkpoint,
not full phase/production acceptance. The bounded milestone
`codex/milestone-effective-control-managed-20261003` preserves its earlier evidence and handoff,
including the failed broader gates. Before-change ref:
`codex/checkpoint-before-effective-co-player-control-20261003T000511Z` at
`fca8961ece87a2a4ec9e0c24682c4a97cdf2b691`. Keep work on `codex/runtime-foundation`.

## Next dependencies

1. Build explicit actor selection on the internal live-presence custody boundary. Separate permanent
   home actor from selected actor; publish authorized observations,
   clear queued input/readiness, and require deliberate Resume against the current selection generation.
   Stale A→B→A observations, queries, inputs and readiness must never regain authority.
2. Keep the grantee's own human character's ordinary decisions/holds. Autonomous owner input cannot
   compete while assigned; accepted obligations finish before safe return. Transport loss cannot
   restart the original allowance or manufacture presence. Permission is not a socket or UI selection.
3. Bind explicit profile39/40 feature/startup-control paths and the local/TLS/Godot consent/selection
   journey with actual paid actions, third/fourth live participants, revocation/return, archive/reopen
   and crash evidence. Native reachability remains separate from managed preparation;
   retain the [complete obligations](p02/co-player-control/DESIGN.md).
4. Keep the retained failed timing gate open. The bounded CPU/pause captures did not reproduce it;
   no more unchanged diagnostic aggregates are justified. Include the opt-in counters at the next
   relevant changed-implementation qualification, then inspect an actual failure before selecting a
   repair. Component averages and later passes do not establish its cause; preserve every bound.

Combat + traversal + recovery remains a provisional simple preset, not an owner-approved preference.
Offers carry explicit scopes. Inventory, trade, production, conversation/quests, care disposition and
private history remain independently scoped; recording private-history scope implements no reader.
No production world or lived save upgrades automatically.

## Relevant inherited behavior and evidence

- [Allowance recovery](p02/allowance-recovery/RESULT.md): schema10/profiles36–38 resume only durably
  measured elapsed time on a new monotonic segment. Offline and unpublished crash time are uncharged;
  repeated crashes can extend grace. Copy activation preserves the schema-nine source. Evidence includes
  933 distinct selected passes, eight actual Host kills and one rendered TLS handback. This does not
  promise exact crash-tail reconstruction or general profile migration.
- [Control return](p02/control-return/RESULT.md): observation-bound deliberate Resume, genuine managed
  hostile damage/loss/paid retreat and native accepted-retreat continuation/handback. Its rendered
  preparation does not prove native hostile approach, initial loss or allowance expiry.
- [Care durability](p02/care-disposition-durability/RESULT.md): 128 actual dispositions, finite recovery,
  coordinated history cuts, transaction faults, ten Host kills and original/archived native decisions.
  Maximum disposition-only/composite capacity is still open; resource pressure triggers this cut first.
- [Four native participants](p02/native-player-roster/RESULT.md): actual Godot/TLS assembly, required-human
  holds, same-actor reconnect, paid Wait combat/outcomes and exact reopen. This earlier profile35 journey
  does not establish later effective co-player takeover or all multiplayer production gates.
- [Archived quote replay](p02/archived-quote-replay/RESULT.md) and
  [terrain projection reuse](p02/multiplayer-service-latency/RESULT.md): measured allocation reductions
  preserve authentication and contracts. Earlier timing failures remain evidence; production history
  latency is not qualified by smaller allocation counts or later isolated passes.

## Owner direction and routing

Keep the portable core and application in C#. The [C++ assessment](CORE_LANGUAGE_CPP_ASSESSMENT.md)
remains research; there is no active native-kernel or IPC migration. Apply the
[world composition contract](../Runtime/docs/architecture/world-composition.md) with one canonical
owner per fact, explicit coupled transactions and meaningful domain types.

The [upkeep supplement](EQUIPMENT_AND_VEHICLE_UPKEEP.md) explicitly covers clothing mending,
item/armor/weapon repair, vehicle/part wear and maintenance, and direct attacks on vehicles and mounts.
Targets distinguish rider/mount and crew/vessel/components. Vehicles use material damage/repair;
mounts use anatomy/injury/care. Resolve occupants, cargo, wrecks and rider consequences without loss or
duplication. P04–P07 integration, P11 content reconciliation and P12–P14 qualification retain their
existing priority; P05 targeting precedes P06 transport/mount integration.

Approved external routes remain exact `fable[1m]` or verified `claude-opus-5-5[1m]`, one-million context,
requested Max, bounded assignments and synchronized skill/docs/steering. The latest request reached
the monthly spending limit. No provider retry, fallback, limit change or further request is pending.
Native workers and critics consume Astra capacity; claim no external-credit savings without evidence.

## Open gates and preserved history

Effective co-player control, general party retreat/pursuit/rescue, production admission, broader lived
save migration, composite/disposition capacity and history latency remain open. Retain the entire
campaign: original content migration, seeded frontier/history/time travel, world composition, ecology,
culture, narrative, progression, all gameplay domains and their art/audio/UI integration. Full
1–4-player, Windows/macOS/Linux, later mobile, physical-device, human, art and production gates remain
required. Windows invitation ACL execution is unrun on this Mac. Passing managed tests cannot close
rendered, physical-device or human acceptance gates.

[The older complete handoff](AGENT_HANDOFF_20261002_ALLOWANCE_ARCHIVE.md) is preserved byte-for-byte
with its original relative links; [archive identity](p02/co-player-control/HANDOFF_ARCHIVE.json) records
its hash. It links earlier archives, milestone evidence, migration/delegation history and failures.
[Git milestone doctrine and index](GIT_MILESTONES.md) remain authoritative for checkpoint practice.
Archives preserve evidence; this file carries the current assignment without removing any requirement.

[The immediately preceding consent handoff](AGENT_HANDOFF_20261003_PERMISSION_ARCHIVE.md) remains
byte-for-byte preserved with [its identity](p02/effective-co-player-control/HANDOFF_ARCHIVE.json).
