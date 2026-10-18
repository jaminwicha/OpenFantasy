# Current-profile ownership and dependency map

Source baseline:6606db30. PLAN-STATE preparation, not an implemented partition format or migration.
The [packet](p02/partition-state/current-profile-dependencies/RESULT.md) pins reviewed sources.
The root composition is [WorldSnapshot](../Runtime/src/OpenFantasy.Core/Worlds/WorldSnapshot.cs);
profiles40/41 use [the complete codec](../Runtime/src/OpenFantasy.Core/Worlds/WorldSnapshotCodecV35.cs)
through their explicit dispatchers. They are not schema14/profile5 with optional extra fields.

## Coverage and limits

This maps every field of the current complete outer frame and the high-risk nested ownership groups
below. It distinguishes current facts, immutable definitions, derived views and historical witnesses.
It is not a certified byte-by-byte migration of every nested record/union variant. That qualification
still requires exact original-history and current-snapshot examples, noninitial archive/checkpoint
boundaries, capacity cases, and an independent review. The source inventory records inspected
member declarations; it is navigation evidence, not a C# serializer proof or a new CI gate.

## Complete current outer frame

The current reader permits31 fields including the tag. The30 remaining semantic fields are listed in
wire order; profiles40/41 differ in the explicit retreat interpreter, not their outer field count.

| Field(s) | Current encoding owner | Partition disposition / migration obligation |
| --- | --- | --- |
| 1 version | Profile40/41 dispatch | Keep semantic version separate from storage, facet and directory revisions. Preserve original interpreter. |
| 2 inventory | `AuthorityEncodingV1` | Custody storages/lots, controls, policies, admitted actors, source pins/capacities and reservations form an admission cohort. Splitting holders must retain global source/identity/capacity obligations. |
| 3 spatial | `WorldSpatialCodecV1.EncodeComponents` | World/branch/realm, terrain, body definitions and actor presence. Installed colliders are reconstructed from construction; do not persist a second independent structure truth. |
| 4 residents | `WorldSnapshotCodecV1` | Actor/custodian identity, name/controller kind/home/wander bounds, comprehension, utterance and experienced time. Resident exposure is active law even when the actor is offscreen. |
| 5 admission | Original admission hash | Preserve origin and transition ancestry; a storage directory hash is not an admission identity. |
| 6–7 calendar | Signed day and normalized milliseconds | Preserve exact instant and pacing consistency. No wall-clock substitution. |
| 8 revision | Global commit sequence | Bind all owner revisions atomically without replacing local semantic revisions. |
| 9 advancing steps | Exploration ordinal | Keep distinct from process intervals and tactical time. |
| 10 dialogues | Dialogue sessions | Preserve all reading holds and participants, not just the currently viewed speaker. |
| 11 gathering | `GatheringCodecV1` | Node/source depletion and regrowth, tools and actor effort form due obligations. No unloaded-node respawn shortcut. |
| 12 production | V2, or V11 with exact joint anchor | Recipes, batches, transformed lots, consumed claims, material operations and continuation context. Profile5 V1 cannot encode this closure. |
| 13 progression | `ProgressionCodecV1` | Actor profiles/curves/state plus currency ledger. Balances and quest/market commitments cannot become disconnected actor copies. |
| 14 quests | `QuestCodecV1` | Definition pins and actor participations; reward obligations bind reserve accounts and delivered custody. |
| 15 construction | `ConstructionCodecV1` | Definitions/projects, builders/owners, stock/reservations, earned work, required access and installed backing. Derived geometry stays tied to exact material evidence. |
| 16 reservoirs | V2, or V3 with anchor geography | Network/capacity/provenance and geometry have shared material admission. Reservoir data is not independent of source retirement. |
| 17 seals | `ReservoirSealPolicyCodecV1` | Liquid/material/thickness rules are pinned policy, not aesthetic terrain metadata. |
| 18 liquid cells | `LiquidCellStateCodecV2` | Exact reservoir hash, topology, flow profile, boundary, portions/domain and units; reconstruct topology from reservoirs/terrain/seals. |
| 19 excavation | `ExcavationCodecV2` | Sites, source ownership and terrain work/history remain linked to gathering/production and actual terrain. |
| 20 pacing | `WorldPacingCodecV1` | Origin, earned process exposure/progress, encounter timing and origin exposure. Calendar/process boundary is derived consistently. |
| 21 encounter lifecycle | `EncounterLifecycleCodecV1` | Seed/catalog/scenario/phase/counters/readiness debt and current/retained actions. Pending commands and random counters remain real owners. |
| 22 coordinated group | `world.coordinated-market-care.v1` | Care, source baseline, optional joint-anchor binding, consented transfer history, proposals, paid exchange and market book. See nested closure below. |
| 23 resource history | `WorldResourceHistoryCodec` | Actor-resource ledger, evidence and anchor frontier. Its hashes bind historical state, not a freely mutable secondary balance. |
| 24 retreat | V1 for40; V2 for41 | Preserve individual continuation/outcome and profile41 party withdrawal parties/members/resolution. Never decode41 with40's narrower interpretation. |
| 25 outcome | `EncounterOutcomeCodecV1` | Rules pin, finalized count, acknowledgments and last outcome bind the actual terminal encounter/action hash. |
| 26 controllers | `WorldControllerCodecV1` | Actor assignment revision, mode and disconnect identity are separate from permission and effective control. |
| 27 recovery rules | `WorldRecoveryPolicy.RulesPin` | Exact assisted-incapacity interpretation; not an optional empty owner. |
| 28 disposition rules | `WorldCareDispositionAdmission.RulesPin` | Explicit voluntary closure interpretation retained independently of treatment. |
| 29 permissions | `WorldControlPermissionCodecV1` | Grant/offer/acceptance/revocation state with exact parties and scopes. See nested control closure. |
| 30 effective control | `WorldEffectiveControlCodecV1` | Active assignment witnesses/revisions/sequences; never infer effective control merely from an accepted grant. |

## Nested ownership that cannot be reduced to actor roots

| Group and current members | Canonical responsibility | Coupled admission / required migration contrast |
| --- | --- | --- |
| Inventory actors, controls, policies, sources/capacities, reservations; custody revision, storages and counted lots | Stable actor/control/custodian and source/reservation owners | Source intervals and reserved claims must stay globally exclusive across holders. Move actual carried lots between holders, not merely a directory address. Preserve semantic storage/lot revisions. |
| Production recipes, batches, live transformed lots, consumed items, material operations | Definitions/provenance immutable; live holdings owned by custodians; consumption/operations historical | The same retired source/output cannot become spendable twice after migration. Preserve installed outputs, gathering issuance, excavation/transport/terrain evidence and combined storage capacity. |
| Production continuation basis, ordinal, base revision/counts, supported feature flags, construction recipe/batch witnesses, gathering baseline, storages and authority-admission hash | Immutable continuation witness tied to exact archive boundary | Historical storages/batches are witnesses, not duplicate current mutable owners. Archive retention cannot be replaced by a summary hash with no retrievable proof. |
| Care remedies, agreements, acknowledgments, treatments, dispositions and ordinary care | Definitions/consent, requested/accepted meaning, finite treatment and voluntary closure are distinct | Treatment records bind consumption, timing, patient-before revision, HP result, helper speed/delay. Disposition is not healing; a pending acknowledgment is not an applied treatment. |
| Ordinary care definitions, permissions, uses/revision and continuation | Permission and finite resource use under care law | Preserve normal and incapacitated assistance contrasts, patient/controller rights and real consumed resources. |
| Care continuation closing hash/revision, archive totals, frontier, pending acknowledgment, patient witnesses and closed-defeat witnesses | Immutable historical proof plus carried pending state | A cut through pending care must preserve exactly what is still open and what already closed; no re-awarding treatment or resurrecting a cancelled request. |
| Coordinated anchor reference/ordinal/boundary sequence/hash, admission, production/care/progression, source baseline, proposal/paid/market contexts and internal quest/lifecycle/geography dependencies | One coordinated archival commitment | Do not split care/production/resource/geography anchors into independently advancing roots. Decode binding requires the actual matched context; the stored binding alone is insufficient. |
| Source issuance allocations, node/source cursors and extracted-source identities | Finite original source admission | A regenerated node cannot mint already-issued intervals. Keep the source baseline and later issuance evidence connected across archive cuts. |
| Resource history actor ledger, evidence, admission and archive/coordinated anchor | Resource-conservation history | Must agree with actual progression/custody/production/care and terminal history; neither current HP nor current balance reconstructs past spending. |
| Proposal open records and outcomes | Exact grant/delivery/witness, creation/expiry and terminal acceptance/termination | Preserve pending terms and remaining expiry across archive/migration; do not silently recapture current terms or clear open offers. |
| Paid exchange open quotes, outcomes and issuer revisions | Exact quote/acceptance/withdrawal/supersession with monetary/material settlement | Quote fill atomically changes both holdings, both accounts, provenance/receipts and related sibling quote state. A directory split cannot split settlement. |
| Market bindings, quote records, observations and lot disclosures | Merchant stock/account/policy; observer-specific knowledge; seller disclosure | Market prices/knowledge are not one global public value. Preserve observer/seller identities, cause action, observed/disclosed sequence/step and expiry. Avoid exposing unseen prices through loaded metadata. |
| Pacing encounter actors/decisions, due actors and required decisions | Shared encounter timing; due collections are derived | Near-Wait legality uses the whole required decision set, not the focused player. Keep tactical units separate from exploration/process time. |
| Lifecycle actions: scope, seed, revision, abilities, combatants, pending commands, random counter and last resolution | Current encounter action authority | Pending casts/attacks consume exact identities/resources/readiness. Retained actions/outcomes remain historical witnesses, not another live combat. |
| Retreat active command/intent, origin, walked samples/cost and last result | One individual retreat law | Interruption/cancel must preserve already-paid costs, route and pending command identity. |
| Profile41 withdrawal revision, parties, last resolution; party scope/side/revision/required actors/members; member command/route/origin/speed/samples; ended reasons/cost/sequence and cancelled decisions | Shared party intent with actual per-member progress | Two independent sides, missing member, cancelled member, blocked step and mid-route save must retain distinct consequences. Do not flatten parties into individual retreat records. |
| Controller assignment actor/revision/mode/disconnect; permission slot owner/grantee/revision/changed/offered/accepted sequences and terms | Connection/availability and explicitly consented capabilities | Offers, acceptance and controller lifecycle must not grant one another implicitly. Preserve original principal/session pairs and revoke/replace semantics. |
| Effective slot actor/revision/sequence/witness; witness owner/grantee, grant/acceptance commands, grant revision, capabilities/terms hash, assignment identity/revision/acquisition, disconnect and owner-control revision | Actual acquired authority | Validate every witness against permission, assignment and historical control, including stale acquisition/revocation. Reconstructing an actor does not itself authorize commands. |

## Derived and alternate-profile roots

`WorldId`/`BranchId` derive from inventory; `EncounterActions` comes from lifecycle in40/41;
`ProcessIntervals` derives from pacing; `ProfileVersion` selects the admitted semantic combination.
`IdentityIndex` and reading-hold convenience views are derived admission/query structures.
`WatershedNetwork` is owned by reservoir capacity in these profiles. Reservoir projections, liquid
quantities/domain/topology and installed spatial colliders must be reconstructed from their owning
facts or bound exact witnesses, not accepted as independently editable duplicates.

`Watershed`, standalone `Transport`, `ConstructionPhysical`, structural/settlement/consequence/coupled
policies, pending excavation and descent interpretation also exist on `WorldSnapshot` for other
compositions. They are not optional independent40/41 fields: the constructor rejects incompatible
combinations, and the complete codec does not serialize them as extra roots. Preserve earlier profile
interpreters and their explicit transitions; future unification must map these owners separately
rather than accidentally stripping them when importing a different original profile.

## Concrete selective construction cohort

The actual [work kernel](../Runtime/src/OpenFantasy.Core/Construction/ConstructionWorkKernel.cs) reads
more than the builder/project pair. Required checks are source/world/control, project ownership and
custodian policy, no reading hold, exact actor/project/tool/storage revisions, manipulation/ground
movement/HP, quest completion, feet/reach, tool definition/quantity/protection/reservation, reservation-
wide stock completeness, and every required-access actor's current position. Placement is checked even
before the final step. Completion additionally needs global spawned identity exclusion, combined
capacity, all finite production inputs/outputs and installed backing.

An advancing selected step also updates calendar/ordinal, gathering and every applicable resident's
exposure. Current schema14 actor images embed resident exposure; all affected actor images change.
Therefore a two-payload loader is incorrect, and skipping those actors is not a permissible optimization.
An immutable history witness may prove unchanged global facts only within a serialized admitted interval;
any intervening action that changes a dependency must invalidate/rebuild it before another step.

The next implementation should extract the *existing* common work qualification from the completion
material mutation, with explicit source-bound stock/access/identity dependencies. Then bind a selected
stored-owner cohort to that qualification and the complete due-process closure under the sole writer.
Prove that unrelated custodian detail and immutable closed provenance can be absent, while actual due
actor/gathering updates remain present. Do not claim actor-detail savings until the representation can
support them without changing exposure law. Final completion loads its larger material/geometry cohort
and performs full original admission; a missing/stale dependency fences before publishing anything.

Required executable contrasts: same saved funded/carry/gift/work/intervention/completion history;
missing/foreign/changed dependency, extra reservation stock in another holder, stale required-access
actor, control replacement, due gathering boundary, absent actor exposure, and before/after publication
faults. Compare original interpreter receipts/full canonical results and retained original history.
Measure actual owner reads, retained detail, full directory/closure bytes and archive work. A smaller
list of requested hashes or source-level interface alone is not selective residency evidence.

## Exit status

This resolves the ambiguity about what the first selective cohort must include and identifies concrete
current-profile migration risks. It does not close the nested-format migration gate, approve a new
format, relax original complete admission or waive review/fairness/cold/platform/scale/checkpoint work.
The full P02 closure table and P00–P14/181-requirement scope remain authoritative and unchanged.
