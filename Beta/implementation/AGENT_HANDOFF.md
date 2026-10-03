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

Effective command integration is under verification in the
[current packet](p02/effective-co-player-control/DESIGN.md). Fresh profile40/schema12, real delegated
movement/combat/retreat/care and material/paid/market consumers are implemented; the clean build and
21 new focused cases pass. Broader affected regressions are running before acceptance/commit. The
[critic](p02/effective-co-player-control/CRITIC_REVIEW.md) found two repaired boundaries: impossible
acquisition chronology and overly broad public planner authorization. No native selection or live
presence proof is claimed. The following foundation remains the last committed milestone.


[Durable co-player consent](p02/co-player-control/RESULT.md) adds fresh profile39/schema11 to the
existing sole world authority: named owner offers, separate exact acceptance, replacement/revocation,
bounded scopes and twelve ordered slots across four home actors. Nonadvancing consent decisions
preserve holds, bodies, supplies, possessions, controller assignments and accepted work. Original
genesis replay, receipts, occupied identities and real archive publication preserve the facet.

Permission alone authorizes no delegated gameplay or private view. The player adapter refuses fresh
consent commands before queueing until a dedicated route exists; authenticated exact prior receipts
remain retrievable while paused. No production world or lived save upgrades automatically.
The [complete dependency design](p02/co-player-control/DESIGN.md) and
[timeless consent contract](../Runtime/docs/architecture/control-permissions.md) distinguish this
storage foundation from the required effective-control consumer.

[Final checks](p02/co-player-control/FINAL_CHECKS.json) record a warning-free build and 2,818 distinct
passing cases, including all 34 new cases and the complete 1,881-case Core run with verified unchanged
Core dependencies. Final Hosting storage488, controller81, network272, shared26, archive14 and server76
pass; Windows ACL remains unrun. The storage repeat passes in 444.86 seconds under the unchanged
600-second limit, without explaining its retained earlier timeout. Independent critique corrected
fabricated recovery provenance and impossible consent chronology. All 735 older tests/fixtures, 23
frozen specs, unrelated predecessor staging and 1,330 architecture links are verified. No new native
or process-kill qualification is claimed. Milestone: `codex/milestone-control-permissions-20261002`.

Before-change ref: `codex/checkpoint-before-co-player-control-20261002T225515Z` at `e741606225e83f68821efe385af35521401df5f4`.
The [handoff WIP checkpoint](p02/co-player-control/HANDOFF_CHECKPOINT.json) preserves successor edits
and partial evidence before consolidation. It is a recoverable backup, not an acceptance claim.
Keep work on `codex/runtime-foundation`; the integration lead owns focused commits and milestone refs.

## Next coherent dependency: effective co-player control

1. Separate active assignment from permission and ownership. Require the original expired disconnect
   episode, safe action boundary, actually connected grantee and explicit acquisition. Keep the true
   submitting principal/session and operated actor; do not manufacture owner deliveries.
2. Add typed authorization and immutable acceptance-time witnesses to delegated commands/movement and
   material/paid history. Revalidate permission for new decisions/open offers; accepted work and closed
   outcomes retain their original authority and complete exactly once after revocation or owner return.
   A submitter's exact old receipt must remain accessible without new action or private-history access.
3. Bind explicit character selection to fresh authorized observations, selection generations, cleared
   queued input/readiness and deliberate Resume. Preserve the grantee's own character's ordinary human
   holds. Stale A→B→A offers, queries and readiness cannot regain authority; transport loss cannot restart
   the original owner's allowance or fabricate presence.
4. Implement the local/TLS/Godot consent and selection journey with actual paid actions, revocation,
   return, archive/reopen and crash evidence. Native reachability is required separately from managed
   preparation. Follow the [detailed acceptance obligations](p02/co-player-control/DESIGN.md).

Combat + traversal + recovery is a provisional simple capability preset, not an owner-approved answer
to the pending preference question. Offers always carry explicit scopes. Inventory, trade, production,
conversation/quests, care disposition and private history remain independently scoped.

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

[The complete previous handoff](AGENT_HANDOFF_20261002_ALLOWANCE_ARCHIVE.md) is preserved byte-for-byte
with its original relative links; [archive identity](p02/co-player-control/HANDOFF_ARCHIVE.json) records
its hash. It links earlier archives, milestone evidence, migration/delegation history and failures.
[Git milestone doctrine and index](GIT_MILESTONES.md) remain authoritative for checkpoint practice.
Archives preserve evidence; this file carries the current assignment without removing any requirement.
