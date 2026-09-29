# Ordinary restorative use

## Ownership and policy

[CareSnapshot](../../src/OpenFantasy.Core/Care/CareSnapshot.cs) owns ordinary use alongside
[aftermath care](aftermath-care.md), through its explicit
[OrdinaryCareSnapshot](../../src/OpenFantasy.Core/Care/OrdinaryCareSnapshot.cs). Progression remains
the sole HP/MP owner; counted custody and production remain the material owners. Ordinary use does
not create an aftermath agreement, acknowledgment, pending obligation or second control authority.

A [RestorativeUseDefinition](../../src/OpenFantasy.Core/Care/RestorativeUseDefinition.cs) pins an
existing remedy and exact supported body-definition versions, delivery mode and exploration duration.
The remedy retains the item and capped restoration amount. The ordinary policy does not duplicate HP,
items or anatomy. Positive HP represents participation eligibility in this admitted corporeal scenario;
it does not establish a universal life, consciousness or swallowing model.

Self-use requires the controlled recipient's own actual dose, manipulation and readiness. Assisted
use requires the supplier's control, manipulation and actual stock, plus a living supported recipient
within the remedy's real spatial reach. Receiving assistance does not require the recipient's own
manipulation or zero recovery debt. A party, affinity or side relationship grants no treatment permission.

[CarePermission](../../src/OpenFantasy.Core/Care/CarePermission.cs) pins a patient, helper and exact
assistance definition. The grant is immutable. An authenticated patient can revoke it; the retained
revocation preserves its command, world sequence and ordinal. Historical uses retain the original grant
and revision, and must precede revocation. Self-use has no external permission tuple. Runtime grant
creation and broader medical consent require their own admitted operations.

Each immutable use definition and permission retains its canonical digest lazily on that exact
object. The first successful read uses the canonical encoder; concurrent readers publish one complete
immutable hash. Encoding failures leave nothing retained, so a later read retries. Constructor-owned
form collections prevent caller mutations from changing the encoded facts. These private digests are
not serialized or shared through entity identity, and they confer no admission or treatment authority.

`CarePermission.GrantVersion` covers the original grant; `Version` also covers its revocation state.
Revocation constructs a separate immutable permission with its own lazy digests. Equal grant hashes
therefore do not imply equal complete permissions or an active grant. Permission checks and historical
ordering remain necessary on every admitted use.

## Availability and exact selection

[WorldRestorativeActions](../../src/OpenFantasy.Core/Worlds/WorldRestorativeActions.cs) shares one
assessment between public preparation and authority evaluation against progressively updated owners.
`PrepareAvailability` selects an exact owned dose for the named policy and permission. `Assess` evaluates
an already captured request without refreshing its revisions or substituting stock. The existing
`Prepare` and `Evaluate` entrypoints return that assessment's intent and receipt rejection respectively.

[RestorativeAvailability](../../src/OpenFantasy.Core/Worlds/RestorativeAvailability.cs) carries a typed
reason, the existing `WorldActionRejection`, and an executable intent only when eligible. Its reasons
distinguish context and reading holds, aftermath obligations, recovery debt, full health, incapacitation,
manipulation, supported form, missing or revoked permission, stale selection, reach, available owned
stock, material-history capacity and ordinary-use-history capacity. A reason describes the first failed
check, not an exhaustive diagnosis of every simultaneous blocker. An encounter or pending aftermath
therefore reports its context before ordinary delivery physiology is considered.

These explanations are not new serialized receipt codes. A stale owner, definition or permission
revision retains `RevisionConflict`; a stale lot revision retains its existing `Unavailable` receipt
while its public explanation is `StaleSelection`. Full health, zero HP, an unsupported form, revoked
permission or unavailable dose cannot spend a restorative item. Zero HP follows the separate aftermath
policy.

Preparation is neither authentication nor reservation. The request captures patient presence/resources,
storage, production, definition and permission revisions. Dose selection orders source lots before
transformed lots, then by lot identity, and excludes protected, reserved, unowned or insufficient stock.
It selects one unit with the exact remedy item/version. Publication never silently chooses another dose.

## Complete publication and time

[WorldRestorativeResolver](../../src/OpenFantasy.Core/Worlds/WorldRestorativeResolver.cs) authenticates
the supplier, then composes `RestorativeItemPlanner` with a retained ordinary-use witness and causal
resource batch. [WorldStepProcessor](../../src/OpenFantasy.Core/Worlds/WorldStepProcessor.cs) supplies
the progressively updated care, resource and material candidates in deterministic actor order. Thus an
earlier revocation or dose expenditure is visible to a later command in the same selected step.
Complete geometry/material admission rolls back care and resource history with the other action effects.
Rejected commands retain their terminal receipt and no completed use or material expenditure.

Exploration use and revocation require an `AdvancingStep`, available exploration state, no reading hold
or pending work, and readiness at the beginning of that step. Successful care marks its initiator as
working, excluding that actor's movement sample. The ordinary 1,800 ms exploration interval advances
calendar, residents' experienced time and processes exactly once. It neither resets debt nor grants
eligibility from readiness earned later in the step. A selected advancing step still advances when a request is rejected;
inspection and cancellation that submit no command do not create a step.

[LocalWorldSession](../../src/OpenFantasy.Hosting/Simulation/LocalWorldSession.cs) accepts an explicit
controlled-player ordinary request only at its admitted, unheld exploration boundary. The session's
readiness gate and ordinary selected cutoff still apply. A reading hold or encounter boundary refuses
selection before an incompatible batch can be built. Exact completed receipts remain reusable while
paused; receipt lookup cannot repeat the treatment. Public offers and helper decisions enter through
the separate projections and controllers described below.

Active-combat item use requires its own due-actor choice/resolution binding under Wait; it cannot use an
ordinary advancing command or a zero-time Care batch to bypass encounter timing.

## Public controls and patient-owned permission

[LocalRestorativeView](../../src/OpenFantasy.Hosting/Simulation/LocalRestorativeView.cs), exposed as
`LocalWorldView.Restorative`, projects the profile26/27 player's known self-use policies and permissions
owned by that patient. Earlier profiles have no ordinary-care projection. Known blocked self-use
choices remain inspectable with their typed reason and no executable intent. Each use offer identifies
the item, pinned restoration amount, capped HP gain at the observed health and exploration duration.
The item label derives from its admitted physical item key, with a generic fallback; this is not a
separate authoritative item-name catalog.

Permission offers expose the helper's visible identity/name, active state and exact patient revocation
selection. Withdrawing permission requires an available living, ready patient in unheld exploration.
It requires neither missing HP nor medicine, supplier stock, proximity or manipulation. A revoked
permission stays inspectable without another executable revocation. Withdrawal is permanent for that
grant and preserves prior care; it does not cancel the separate aftermath agreement.

The projection contains no helper-controlled use intent, helper inventory, source lineage, control
credentials or global state hash. Captured self-use and revocation intents carry the necessary
concurrency tokens; they do not grant authority or expose private histories as browsing data.

[RestorativeActionOffers](../../client/scripts/RestorativeActionOffers.cs) maps these permitted views to
shared semantic actions and explains blocked choices. An ambiguous self-use policy is not silently
resolved to the first item. Permission selection follows an explicitly selected helper, or the sole
known permission when unambiguous. The [quick wheel](../../client/scripts/FarmQuickWheelModel.cs)
keeps the existing four cardinal action slots and places ordinary care on a separate four-slot page.
Inspection and page changes issue no command. Refreshing an open choice cannot silently change its
captured intent or actor revision: changed choices require a new selection. Input devices use the same
semantic offer and client submission boundary.

## Helper-owned ordinary aid

[LocalRestorativeController](../../src/OpenFantasy.Hosting/Simulation/LocalRestorativeController.cs)
advises ordinary assistance under `local.full-dose-assistance.v1`. Its conservative local preference
requires positive patient HP and missing HP at least as large as the remedy's complete restoration
amount. This full-dose threshold belongs to the helper policy, not Core treatment legality; an explicit
legal self-use or assisted request can restore a smaller capped amount.

Selection requires a Host resident with an actual current control binding, helper readiness, the exact
active patient permission and a successful shared restorative preflight. The helper commits its own
captured request. Patient acknowledgment is neither required for ordinary aid nor a substitute for
helper commitment. No party relationship, UI selection or projection grants control of the helper.

The local session considers aid at ready exploration cutoffs alongside ordinary host actions. Reading
holds, encounter/aftermath boundaries and consequence resolution exclude this route. Actors already
selected for an action, including a patient using medicine or withdrawing permission, are excluded from
helper selection. Permissions are ordered by helper, patient and permission identity, and selection
reserves both participants against another aid proposal in that batch. The complete world still
authenticates, evaluates and publishes the command atomically.

A helper selection identity binds world/branch, participants, definition/version, permission grant and
revision, patient resource revision, and exact dose kind/lot/revision/quantity. It excludes clocks,
movement and unrelated storage/production revisions, although the actual request captures and
revalidates the relevant observational revisions. An occupied command or derived consumption/use
identity suppresses that preferred proposal; the controller does not allocate fresh identities or try
another patient as a hidden retry. A meaningful new resource or dose selection can yield a different
proposal.

The controller consumes finite stock through ordinary authority. It does not create a dose, craft from
ingredients, replenish supplies, teleport, waive reach or restore the helper's readiness. The session's
existing preview fallback can omit conflicting NPC proposals without rejecting a valid player request.
The session's pause and focus readiness gates apply to helper selection as well as player commands;
an inspection wheel by itself does not pause a running world.

## Evidence, identity and persistence

[RestorativeUse](../../src/OpenFantasy.Core/Care/RestorativeUse.cs) retains the actual participants,
policy/permission pins, consumption, command position, resource boundary and timing interpretation.
[WorldRestorativeAdmission](../../src/OpenFantasy.Core/Worlds/WorldRestorativeAdmission.cs) joins these
records to exact consumed claims. Every consumed claim belongs to exactly one aftermath treatment or
ordinary use. HP-before and HP-after describe historical resources, not a substitute current-health owner.

[WorldRestorativeUseResourceEvidence](../../src/OpenFantasy.Core/Worlds/WorldResourceEvidence.cs)
retains the exact selected request. Resource admission reconstructs the capped effect from historical
progression, checks dose/permission/request pins and accounts for each completed use exactly once.
Journal replay supplies historical control, geometry and capability truth; self-consistent labels alone
do not authenticate treatment. Archive continuation preserves grants, revocations and completed uses,
rejects retroactive pre-cut insertions and reserves all potential resource writers before sealing.

World profile26, action V14 and Care V2 explicitly encode ordinary use. Profile25 retains its V1 care
bytes and aftermath-only interpretation. Resource evidence uses a named ordinary-use tag within the
existing inline structural history/archive-wrapper formats; profile25 rejects this cause. Schema7
admits the explicit profiles without changing its tables or upgrading lived saves. Complete recovery
replays the actual commands and checks receipts, identity history, archive rows and head bytes.

### Publication, interruption and acknowledgment

An ordinary use publishes its item retirement, care witness, resource effect, elapsed interval and
terminal receipt in one complete world commit. Revocation publishes the patient-owned permission
change and its receipt under the same rule, without a dose or HP effect. A resource cut selected before
that batch joins the same transaction: the closing root is the exact pre-batch world, and the new use
belongs to the successor resource segment. The cut itself grants no time, item or restoration.

[SqliteWorldAuthority.Commit](../../src/OpenFantasy.Hosting/Worlds/SqliteWorldAuthority.cs) writes all
rows before committing SQLite and publishes its in-memory state only afterward. These are distinct
boundaries:

| Interruption boundary | Required recovered state |
| --- | --- |
| Before SQLite commit | The complete prior root, without the attempted outcome, identities or archive publication |
| After SQLite commit but before acknowledgment | The complete successor root and durable outcome, including any co-published archive |
| Exact authorized receipt lookup after recovery | The same outcome with no second dose, restoration, permission change or elapsed interval |

A caller cannot infer which boundary won merely from a missing response. Recovery verifies the
accepted journal and associated rows before admitting more work. Replaying an already accepted batch
as a fresh commit is distinct from looking up its exact command receipt; the old batch cannot advance
the new root. The [persistence contract](persistence-and-history.md) owns these recovery and retry rules.

[RestorativeJourneyAdmission](../../src/OpenFantasy.Hosting/Content/RestorativeJourneyAdmission.cs)
composes a fresh source-backed scenario with the original Potion effect, recipe, combat stats and
quest rewards. It partitions the same two raw source doses between player and Wren, retaining six
berries in Wren's custody. This explicit fresh distribution changes neither deposit policy nor the
existing care scenario. The player's corporeal definition/version binds the delivery policy; stock,
range, prior permission and 1,800 ms use duration are authored adaptations rather than original item facts.

## Retained capacity and contract boundaries

The material owner retains at most 256 consumption claims, shared by aftermath treatment and ordinary
use. The ordinary-care owner separately retains at most 256 completed uses. Shared preflight checks
both bounds after delivery and dose eligibility, returning `MaterialHistoryCapacity` or
`OrdinaryHistoryCapacity` with no executable intent. The authority rechecks the same limits against
the progressively accumulated owners in its selected transaction. The material planner independently
refuses a full consumed-claim collection. Rejection preserves the dose, health and completed care history;
submitting a rejected advancing command still has the normal time semantics described above.

These are retained representation bounds, not a character's lifetime allowance. A
[resource-history segment cut](world-resource-history.md) rotates causal resource batches while
preserving the material claims and ordinary use records. It frees neither of these owners. Material
capacity is checked first when both are full; complete-world accounting makes ordinary uses a subset
of total consumed claims. The distinct diagnostic categories do not imply that a valid world can retain
more ordinary uses than consumed claims.

The standalone [care continuation frontier](care-continuation.md) preserves cumulative use counts,
grants, revocations and care chronology while bounding active records. It does not rotate consumed
material claims or change the profile26 limits above. Profiles25/26 refuse that continued owner.
The explicit [coordinated profile27](coordinated-world-continuation.md) closes material, care and
resource owners together before the whole next batch and its future actionable headroom exceed
those windows. It preserves exact permission and prior-use facts through an authenticated joint
archive; it does not silently upgrade a lived ordinary-care world or grant unbounded Host storage.

Lifetime material/care continuation requires an explicit admitted protocol that preserves provenance,
permissions, identity occupation and completed effects. Resource segment rotation alone supplies no
such protocol. Capacity explanations identify the implementation boundary rather than reporting
missing medicine or denied permission, and cannot bypass it. Ongoing activities, passive recovery,
conditions, injury repair and broader medical delivery have distinct owners and delivery contracts;
ordinary HP restoration establishes none of those effects. Architectural contracts do not substitute
for rendered, device, human, art or production acceptance.
