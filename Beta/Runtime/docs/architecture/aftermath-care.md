# Aftermath care

## Responsibilities and participants

[CareSnapshot](../../src/OpenFantasy.Core/Care/CareSnapshot.cs) retains pinned remedies, prior
care agreements, recovery acknowledgments and actual treatments. It does not own current HP,
inventory, body capability or encounter time. Those facts remain in their canonical owners.
The composition uses the complete-world authority and explicit schema-seven durable storage for
profiles25/26. The [coordinated world](coordinated-world-continuation.md) uses schema eight to close
care, material and resource history together while preserving pending obligations.
Fresh admission excludes earned care/material/resource history. Recovery replays the exact
acknowledgment and treatment commands, preserving a pending obligation and its existing terminal
acknowledgment receipt. Reopening grants no helper action, fabricated dose or treatment receipt.
The [archive contract](resource-archive-continuation.md) retains older causal resource evidence.
The local session binds patient acknowledgment and host-helper selection separately. The
[native controls](native-encounters.md#care-interaction) capture that acknowledgment without granting
the patient control over the helper.

A `CareAgreement` names the patient, helper and permitted remedy before treatment. An
`AcknowledgeCareIntent` records the patient's controller acknowledging an exact incapacitating
encounter outcome. It neither creates medical permission nor performs bodily work. The separate
helper-controlled `AdministerRemedyIntent` commits the helper and supplies the actual dose.
The resulting treatment witness and command receipt record different facts.

Remedy definitions and agreements retain their canonical `Version` digest lazily on the exact immutable
object. The first successful read invokes the canonical encoder; atomic publication makes concurrent
readers observe one complete immutable hash. A failed encoding is not retained. The private digest
does not enter canonical bytes, cross object identities or certify admission. A new definition or
agreement computes from its own facts, while complete-world treatment checks remain authoritative.

## Eligibility and complete publication

[WorldCareActions](../../src/OpenFantasy.Core/Worlds/WorldCareActions.cs) requires the exact
outcome, original lethal damage, unchanged action/timing boundary and a patient at zero HP.
The helper is an admitted neutral actor outside the patient's combat side, is alive and ready,
has manipulation capability, and can reach the patient under the remedy's range and spatial
rules. Reading holds and pending excavation exclude this action boundary. Zero HP means this
profile's incapacity; it does not define death, consciousness or oral delivery for all medical systems.

Dose selection uses actual owned raw or produced stock of the pinned remedy item, with one exact
unit and revisions for patient presence/progression, care, storage and production. Selection is
deterministic by ingredient kind and lot identity; an offer does not authenticate or commit a request.
Stale or unavailable requests preserve material and resources.

[WorldCareProcessor](../../src/OpenFantasy.Core/Worlds/WorldCareProcessor.cs) authenticates actor
control and builds a private complete candidate. Acknowledgment preserves the unadvanced outcome
and spends no dose or resource. Treatment composes `RestorativeItemPlanner` with capped HP
restoration, one retained material-consumption claim, terrain/reservoir reconciliation, helper
standard readiness debt, and resumption of the same encounter. It earns no exploration or tactical
time and does not teleport the helper or fabricate supplies. World publication includes all effects
and the corresponding [resource-history batch](world-resource-history.md) together.

[WorldCareAdmission](../../src/OpenFantasy.Core/Worlds/WorldCareAdmission.cs) joins every
treatment to its prior acknowledgment/agreement, exact one-dose claim, participant identities,
resource revision and remedy. Resource admission preserves and replays the original defeat and
later restoration; it does not replace the last attack's historical HP with current HP.

## Local session and public observations

[LocalWorldSession](../../src/OpenFantasy.Hosting/Simulation/LocalWorldSession.cs) admits the patient's
`AcknowledgeCareIntent` into one isolated Care batch. It returns that command's terminal receipt before
a later ready cutoff may select helper work. Both boundaries clear sampled movement and select no
ordinary NPC work, tactical tick or exploration time. Exact receipt retrieval precedes readiness and
new-action restrictions, so retrying an acknowledged request neither repeats consent nor treats the patient.
While care is pending, other local actions are refused; another acknowledgment can receive its ordinary
durable rejection. Stale acknowledgments are evaluated against the actual boundary, never refreshed silently.

[LocalCareController](../../src/OpenFantasy.Hosting/Simulation/LocalCareController.cs) selects the actual
remedy only for a Host resident with an admitted control binding. It constructs the treatment command
with that helper's principal and session. The player's adapter neither accepts a foreign actor command
nor impersonates the helper. Core agreements remain controller-neutral; another player-controlled helper
needs a separately authenticated adapter. This local policy exposes that route as unavailable.

One automatic helper selection identity belongs to each scoped acknowledgment. It is the first sixteen
bytes of SHA-256 over ASCII `local.care-selection.v1`, followed by the sixteen-byte world, branch,
acknowledgment and helper identities in that order. The authority's complete occupied-identity ledger
guards selection. An occupied identity, including a rejected earlier selection or collision, requires
attention; the controller does not mint another identity to retry. A missing eligible dose or helper
produces no selected command. Dispatch/storage failures remain host failures rather than fabricated care.

Reopening reconstructs the public view but performs no helper work until readiness. Losing readiness
prevents later cutoffs; an already selected transaction may finish under the session's ordinary serialized
commit policy. Pending care retains Outcome and therefore reports Waiting when ready, Paused otherwise.

[LocalCareView](../../src/OpenFantasy.Hosting/Simulation/LocalCareView.cs), available through
`LocalWorldView.Care`, projects only the current patient's applicable lethal outcome or pending care.
Offers contain helper identity/name, eligibility/reason and the exact acknowledgment intent. Its opaque
action hash is a stale-boundary token. Pending status distinguishes available automatic work, unavailable
care and an occupied selection needing attention. It exposes no helper credentials, material lots,
quantities or provenance. The projection grants no physical action to an incapacitated actor and is not
a general perception, consent or medical UI model.

## Canonical scope and extension boundaries

[CareJourneyAdmission](../../src/OpenFantasy.Hosting/Content/CareJourneyAdmission.cs) explicitly
creates a fresh care-enabled encounter journey. It composes finite custody and helper placement before
geography binds the complete root, then admits care and resource history through `WorldCareAdmission`.
The ordinary encounter selection retains its own profile and admission bytes. Opening a care world
checks the selected world, branch, profile and admission; it never upgrades a lived encounter save.

[CareFarmSources](../../src/OpenFantasy.Hosting/Content/CareFarmSources.cs) pins the retained item
packet and original item/recipe files, including Potion's 40 HP effect and `craft_potion`'s three berries
to one Potion. [CareFarmBuilder](../../src/OpenFantasy.Hosting/Content/CareFarmBuilder.cs) gives Wren
two raw Potions and six berries with actual source rights and storage capacity. There is no earned
crafted stock. Physical calibration conserves all three admitted berry units' mass and volume in one
dose, without inventing a vessel. This calibration is distinct from the original recipe payload.
Wren's authored home lies one cell north of the player with zero wander radius, admitted body clearance
and a 256-subunit remedy reach. His existing control, dialogue and quest roles remain intact; combat
statistics and abilities retain their original source witnesses. These are fresh scenario parameters,
not universal helper behavior, medical range or resource supply rules.

Fresh composition requires an unearned encounter/material basis and pins its complete basis with
the care definitions. `CareCodecV1`, `WorldActionCodecV13`, `WorldStepCodecV25` and
`WorldSnapshotCodecV25` retain explicit bounded formats. Earlier profiles keep their own rules.
Canonical decoding proves internal relationships, not historical authorization or trusted genesis.

[Ordinary restorative use](ordinary-care.md) binds self/companion delivery through a separate exploration
policy and explicit Care V2 interpretation. Anatomy, conditions, ongoing consent and passive recovery
require their own eligibility and time contracts. They may reuse the actual material and
resource operations without inheriting this narrow aftermath policy. Continuous play also requires
authenticated archival continuation across resource, care and material evidence; bounded retained
collections are storage envelopes, not accepted lifetime treatment limits. The standalone
[care continuation frontier](care-continuation.md) preserves pending aid, cumulative totals and
latest patient treatment witnesses across explicit domain cuts. Existing world profiles refuse
that continued owner until a coordinated world interpretation supplies the other authority contracts.
