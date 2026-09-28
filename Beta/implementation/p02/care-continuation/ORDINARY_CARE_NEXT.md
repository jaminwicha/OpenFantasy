# Ordinary restorative item use — implementation-next proposal

Status: **proposed design and implementation sequence; not accepted runtime behavior or completed
qualification.** This document records the next ordinary-care composition, not a change to preserved
specifications or permission to reinterpret existing saves. The integration lead chooses and checkpoints
the implementation scope. Existing aftermath, resource-archive, durable, native and human gates remain.

The owner's [world composition decision](../../WORLD_COMPOSITION_DECISION.md) requires the same material
and resource primitives to support self-use and an ordinary living recipient, while keeping permission,
player confirmation, helper commitment and receipt distinct. Its broader medical, passive-process and
anatomical requirements remain obligations. This proposal supplies that contrasting action case without
turning every heal into an aftermath acknowledgment or introducing a general-purpose effect framework.

## Existing foundation and exact gap

[RestorativeItemPlanner](../../../Runtime/src/OpenFantasy.Core/Care/RestorativeItemPlanner.cs) already
prepares one exact raw or produced dose together with capped HP restoration for the supplier or another
admitted recipient. [RestorativeItemTests](../../../Runtime/tests/OpenFantasy.Hosting.Tests/Production/RestorativeItemTests.cs)
exercise those component candidates. They do not establish world legality, complete publication, time,
permission, ordinary client use or durable ordinary-care recovery.

The current world binding intentionally has narrower semantics:

- [WorldCareActions](../../../Runtime/src/OpenFantasy.Core/Worlds/WorldCareActions.cs) requires an exact
  lethal Outcome, prior agreement, patient acknowledgment and capable reachable neutral helper.
- [WorldCareAdmission](../../../Runtime/src/OpenFantasy.Core/Worlds/WorldCareAdmission.cs) joins every
  consumption to an aftermath treatment and requires treatment count to equal all consumed-item claims.
- [WorldResourceAdmission](../../../Runtime/src/OpenFantasy.Core/Worlds/WorldResourceAdmission.cs)
  reconstructs a treatment through its acknowledgment/agreement and historical zero-HP recipient.
- [WorldStepProcessor](../../../Runtime/src/OpenFantasy.Core/Worlds/WorldStepProcessor.cs) routes the
  dedicated Care boundary to that policy; an active encounter accepts its own decision/resolution flow.

Do not relax these checks for profile25, manufacture a defeat or acknowledgment, or append an unexplained
HP increase. The original lethal attack remains true at its historical boundary. Progression remains
the sole current HP owner; [resource history](../../../Runtime/docs/architecture/world-resource-history.md)
remains reconstructable causal evidence rather than another resource component.

## Proposed model and production API

Admit a fresh explicit **profile26**, with a versioned **CareCodecV2** interpretation. These numbers are
proposed next selectors and must be checked for conflicts before implementation. Preserve profile25,
CareCodecV1 and their exact restore/publication rules. Do not upgrade a lived world on open.

Keep one cohesive care owner rather than a parallel universal interaction authority. Extend its
versioned interpretation with the following typed records; names below are proposed APIs:

| Type | Responsibility and exclusions |
| --- | --- |
| `RestorativeUseDefinition` | References one existing remedy identity/version for its item and restorative amount; adds explicit delivery, compatible-form, recipient-eligibility, reach and time policy. Does not duplicate current HP or possession. |
| `CarePermission` | An exact patient/helper/use-definition scope, revision and active/revoked state. Does not grant control over the helper or ownership of their stock. |
| `RestorativeUse` | Records one completed ordinary use: command, actual initiator/recipient, definition and permission witness, consumption ID, world sequence, resource boundary and declared timing outcome. Contains no aftermath acknowledgment. |
| `UseRestorativeItemIntent` | Captures recipient, use-definition/version, optional permission identity/revision, target presence/resource revisions, supplier storage/production revisions and one exact ingredient selection. |

The existing remedy is the shared item/effect pin. The new use definition binds its ordinary policy;
existing `CareAgreement`, `CareAcknowledgment` and `CareTreatment` retain their specific aftermath
meaning. Do not add optional fields to their V1 bytes and call that compatibility.

`WorldRestorativeActions.Evaluate` supplies domain eligibility and a `Prepare` operation captures an
exact available selection. Neither publishes or authenticates an adapter's delivery claim. A cohesive
world resolver authenticates actual actor control, applies those checks to the current candidate,
calls `RestorativeItemPlanner`, and returns all material/resource/care candidates for complete admission.
Use the existing counted custody, production, progression and body/spatial owners; no arbitrary setter,
duplicate inventory, parallel HP field or new process is needed.

## Living recipient, delivery and permission policy

The first supported ordinary policy admits only the explicitly authored living corporeal forms in its
fresh scenario. Positive HP represents their ability to participate under that bounded profile, as in
[WorldActorAvailability](../../../Runtime/src/OpenFantasy.Core/Worlds/WorldActorAvailability.cs).
It is **not** a universal test of life or consciousness. There is currently no separate swallowing or
consciousness state in [BodyFunctions](../../../Runtime/src/OpenFantasy.Core/Actors/BodyFunctions.cs).
Do not invent such an anatomical fact, infer it from manipulation, or attach an unvalidated boolean.

Declare delivery in the pinned action policy instead:

- **Self-use:** the actual controlled actor is the recipient, has positive HP, a supported admitted form,
  usable manipulation and readiness, and owns the selected dose. The policy treats ordinary Potion
  use as an abstract conscious drinking action for that supported profile. It does not simulate a mouth,
  swallowing, airway safety or nutrition. No second acknowledgment or permission record is necessary.
- **Living-recipient assistance:** the actual controlled supplier has positive HP, manipulation and
  readiness; the supported recipient has positive HP and the exact active assistance permission. The
  pinned delivery policy explicitly permits assisted application to that form. It must not imply that
  every corporeal creature, construct or future ghost can receive the remedy. Check actual spatial reach,
  obstruction and target presence. Do not require recipient manipulation merely because the helper uses
  their own hands, and do not require the recipient to have zero action debt merely to receive aid.
- **Zero HP:** refuse the ordinary route. Existing aftermath external administration retains its own
  explicit fantasy adaptation and exact recovery-choice protocol. This neither implements resurrection
  nor decides the later distinction between death, unconsciousness and incapacity.

The predecessor's [UseItem](../../../../src/Sim/World.Systems.cs) names drinking, refuses fallen users
and refuses full-health HP-only use. The retained content pins in
[CareFarmSources](../../../Runtime/src/OpenFantasy.Hosting/Content/CareFarmSources.cs) establish Potion's
40 HP effect and the actual three-berries-to-one-Potion recipe. They do not establish time, anatomy,
external application, range or consent. Pin the new delivery/policy adaptation separately from the
original payload and physical calibration. Never present these authored adaptations as recovered source
facts. Preserve original class/monster statistics and ability pins in acceptance fixtures.

For external aid, use a prior scoped permission owned by the patient. Friendship, side, party membership
and affinity are not permission. A patient-authenticated revoke action advances that permission's revision;
a later or stale use must fail. Historical uses retain the exact prior permission witness, so revocation
cannot erase past authorization. Initial grants may be explicitly authored for the first scenario;
runtime creation/extension of grants needs its own authenticated operation before being offered.

Self-use spends the actor's own stock. External aid spends the helper's stock under the helper's own
principal/session. A patient's request never impersonates that helper. The local NPC policy may select
an eligible aid proposal under the NPC's own admitted control, but the proposal is not a reservation or
patient-issued commitment. The first ordinary use is instantaneous at its accepted boundary and creates
no mandatory pending-care obligation. Do not reuse `CareSnapshot.Pending` or its global aftermath hold.

## Time, order and complete publication

The smallest first binding supports **ordinary exploration self-use and living-ally assistance** through
the existing `AdvancingStep`, not the zero-time aftermath Care kind. Require exploration, actual readiness,
no reading hold, no pending excavation and no unresolved aftermath obligation. Mark successful use as
work so its initiator cannot also move in the same selected step.

The proposed initial cost is that step's existing 1,800 ms exploration interval. All relevant processes,
residents and calendar use the existing [pacing contract](../../../Runtime/docs/architecture/world-pacing.md)
exactly once. Do not add a second calendar charge, reset carried encounter debt or grant an action from
readiness earned later in the same step. A finite authored cost is a policy decision, not a Potion
payload fact or measured balance acceptance.

Full health, stale selection, denial or no applicable effect spends no dose and creates no restoration
record. A selected advancing world step still earns its normal time even if an action is rejected;
the no-effect policy does not rewind autonomous world time. UI preflight can avoid offering a known
full-health use, but authority must recheck it. A denied inspection or cancelled UI selection submits
no command and earns no additional action time.

Within a multi-action step, plan against the progressively updated candidates: a preceding dose use,
permission change, reward or inventory action must be visible to the following command. Retain actual
command ordinals. Complete candidate admission must roll back ordinary-use records and resource history
alongside inventory, production, progression and newly created identities if later material/geometry
checks reject that action. Reuse the existing reservoir, liquid, terrain retirement and capacity joins.

**Active-combat item use is a following required binding, not implicitly covered by exploration use.**
It must enter the existing due-actor choice/resolution machinery, wait for required human decisions,
retain the exact selected command and revalidate target, permission and dose at resolution. A queued
receipt means selected, not healed. Resolution publishes consumption, restoration and declared readiness
debt together without granting exposure; tactical ticks alone advance that clock. Specify the cost of
a selected action invalidated before resolution and prove it. Do not silently choose another dose,
overwrite another actor's choice, bypass Wait with a Care batch or give a second receipt for the same
selected command. Pending selection is not an inventory reservation unless a real reservation contract
is introduced and qualified.

## Resource history, codecs, archives and durable admission

Add a named `WorldRestorativeUseResourceEvidence` bound to the exact completed use and selected request.
Reconstruct its actual capped HP change from the historical actor state and admitted definition, with
unchanged MP, currency, capacities and unrelated actors. Historical permission/control/geometry truth
still requires actual journal replay; a self-consistent effect record cannot authenticate itself.

Replace the profile26-only consumption join with complete, disjoint ownership: every consumed claim
belongs to exactly one aftermath treatment or ordinary use, and every such operation owns its exact
claim. Leave profile25's existing equality and aftermath checks intact. No consumption becomes free
food, magic or another unnamed effect merely because the material planner accepts it.

Update these concrete boundaries together:

- [WorldResourceContinuation](../../../Runtime/src/OpenFantasy.Core/Worlds/WorldResourceContinuation.cs):
  reserve capacity for all possible ordinary restoration batches/effects and other resource writers in
  the complete selected batch before a cut. Never seal halfway through a command or transaction.
- [WorldResourceArchiveAnchor](../../../Runtime/src/OpenFantasy.Core/Worlds/WorldResourceArchiveAnchor.cs):
  retain ordinary-use and permission continuity needed by successor admission, preserving original
  aftermath, reward, RNG and closing-progression witnesses without retaining ancestor root chains.
- `WorldResourceAdmission`: account for every ordinary use exactly once, replay historical permission
  versions, preserve original attacks and exact funded currency rewards, and reject forged/missing uses.
- Canonical world/action/step/care/resource-evidence codecs: use explicit new interpretations and bounded
  frames. Existing resource inline V1 and archive-backed V2 are already assigned; do not overload V2 as
  a new inline format. Keep profile25 encoders/decoders and old source/save fixtures byte-exact.
- Complete identity indexing and genesis: reserve use/permission/definition identities, retain permanent
  command uniqueness across cuts, and exclude earned ordinary use, consumed stock and resource evidence
  from new-world admission. A sealed or component-valid root is not trusted fresh genesis.

Schema7 may remain the storage container if the increment uses existing commit kinds and tables. This
requires an explicit reviewed profile26 binding in `SqliteWorldAuthority` and `WorldJournalRecovery`;
their current exact profile25/schema7 checks cannot simply be removed. Schema/index definitions and
old schema/profile combinations remain enforced. If a genuinely new table or commit kind is necessary,
introduce a new schema rather than rewriting schema7. No automatic save migration is proposed.

Retain [resource archive publication](../../../Runtime/docs/architecture/resource-archive-continuation.md):
actual retrieved bytes, before-hash linkage, publication at boundary+1, commit/receipt/identity/head
atomicity, missing/extra archive rejection, permanent identity lookup and bounded journal accounting.
The new evidence must work through real recovery, archive retrieval and recorded-state inspection.
Resource continuation does not solve separate care/material retention limits or the journal's development
envelope. Do not present 256 consumptions as a character lifetime limit or claim unlimited durable play.

## Staged implementation and acceptance

| Stage | Cohesive work and required evidence | What it does not close |
| --- | --- | --- |
| 1. Domain and fresh content | Versioned ordinary definitions, exact permission/revocation, use records, intent and pure eligibility; source-backed raw self-use and crafted living-ally composition. Pin supported forms and declared delivery; retain original stats. | World publication, durable or native behavior. |
| 2. Complete exploration world | Fresh profile26/CareV2 admission, advancing-step resolver, current-candidate ordering, identity/resource/material/terrain joins, explicit no-effect behavior. Prove both action families through actual accepted world commands. | Active-combat use or storage recovery. |
| 3. Durable and continued execution | New profile/schema binding, exact codecs/replay, complete-batch resource reservation, actual archive retrieval, atomic publication, duplicate receipts and real external process kills. | Native controls, human/device or production-scale acceptance. |
| 4. Session and client | Authenticated player and real Host-NPC routes; exact public offers, inventory/self and selected-recipient actions, permission status and cancellation/revocation. Share semantic actions across icon, key, wheel, pointer and controller paths. | Rendered behavior until exercised; multiplayer consent or full medicine breadth. |
| 5. Native exploration journeys | Real raw self-use and actually crafted living-ally aid after earned damage, observable restrained use/reaction, exact paused reopen, blocked/stale inputs and focus/cancel behavior. Preserve reports, source/binary hashes and failures. | Physical-device/OS focus, human usability/art acceptance or complete P02. |
| 6. Combat composition | Pending ordinary-use choice and resolution under Wait, due-actor cost and target/permission/stock revalidation, real renewed attack, interruption/reopen and native controls. | Later rest, poison, persistent conditions, death or broad medical policies. |

Do not stop at component or headless completion. Native probes use the shared desktop's isolated
background-render policy with bounded waits and no focus theft. Real OS/device behavior and readable,
enjoyable use still require their distinct acceptance. Current provisional visuals do not qualify
production art or animation. Update timeless architecture references alongside each implemented
contract; keep proposed work and qualification history in this packet.

The minimum adversarial matrix spans the stages rather than being deferred to UI work:

- Raw self-use and real crafted living-ally aid; near-full capping, exact one-dose lineage, unchanged
  MP/currency/unrelated actors, and later crafting preserving all claims.
- Actual patient versus supplier controls; unavailable/revoked permission, stale permission revision,
  unsupported delivery/form, missing manipulation, zero HP and real range/obstruction failures.
- Full health, stale presence/progression/storage/production/lot, competing users of one dose, historical
  identity collision, overflow/capacity failure, atomic rejection and exact duplicate receipt.
- Actual damage → ordinary use → later funded reward/refill → further attack, plus ordinary use beside
  existing aftermath care. No historical HP rewriting, omitted currency, duplicated helper action or RNG
  reset. Preserve original combat stats and obtain damage by legal play rather than forged loaded roots.
- Complete-batch archive cut immediately before a use and before multiple potential resource writers;
  actual retrieved boundary, restart, corruption rejection and external SIGKILL before/after publication.
- Exact profile24/profile25 golden bytes and replay, original aftermath pending/completed reopens,
  raw/crafted care and existing archive crash regressions appropriate to changed shared contracts.
- Native exact selection, cancellation, stale refusal, inspected versus committed state, separate actual
  helper control, no information leakage, paused reopen and restrained presentation of actual outcomes.

## Risks, unresolved breadth and phase placement

The principal design risk is accidentally making the aftermath exception the universal healing model.
Another is labeling the bounded positive-HP participation policy a full life/consciousness system.
The explicit delivery/form policy prevents unsupported medical claims while leaving later conditions,
anatomy and death owners free to narrow capabilities. It must not become a hard-coded species-name test.

Permissions add historical state: revocation cannot erase evidence or substitute current permission for
past authorization. Multiple commands in one step and combat selection/resolution expose stale input
and one-dose races. Readiness, exposure and receipt meanings must remain distinct. A public projection
must show relevant recipient/action consequences without revealing private helper lots, credentials or
provenance; automatic NPC willingness must never be inferred solely from an available inventory.

Add no passive recovery in this increment. Poison, regeneration, camp treatment, delayed activities,
resurrection and anatomical repair need their own operation, exposure, interruption and retention laws.
The first ordinary route has no free rest, no instant full refill and no universal consent framework.

This is a **P02** dependency for the actual item/cooperative-action playable loop, with active-combat
use required before claiming its relevant encounter interaction complete. The
[full implementation plan](../../../spec-v1_1/IMPLEMENTATION_PLAN.md) still requires P03 multi-client
Wait, permission/reconnect and rest scheduling; P04 environmental medicine/camp care; P05 broader
capability and restorative operations. Full P01/P02 exits and all later production gates remain open.
