# Aftermath care

## Responsibilities and participants

[CareSnapshot](../../src/OpenFantasy.Core/Care/CareSnapshot.cs) retains pinned remedies, prior
care agreements, recovery acknowledgments and actual treatments. It does not own current HP,
inventory, body capability or encounter time. Those facts remain in their canonical owners.
The composition uses the complete-world authority and explicit schema-seven durable storage.
Fresh admission excludes earned care/material/resource history. Recovery replays the exact
acknowledgment and treatment commands, preserving a pending obligation and its existing terminal
acknowledgment receipt. Reopening grants no helper action, fabricated dose or treatment receipt.
The [archive contract](resource-archive-continuation.md) retains older causal resource evidence.
The ordinary session scheduler and presentation require separate care action bindings.

A `CareAgreement` names the patient, helper and permitted remedy before treatment. An
`AcknowledgeCareIntent` records the patient's controller acknowledging an exact incapacitating
encounter outcome. It neither creates medical permission nor performs bodily work. The separate
helper-controlled `AdministerRemedyIntent` commits the helper and supplies the actual dose.
The resulting treatment witness and command receipt record different facts.

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

## Canonical scope and extension boundaries

Fresh composition requires an unearned encounter/material basis and pins its complete basis with
the care definitions. `CareCodecV1`, `WorldActionCodecV13`, `WorldStepCodecV25` and
`WorldSnapshotCodecV25` retain explicit bounded formats. Earlier profiles keep their own rules.
Canonical decoding proves internal relationships, not historical authorization or trusted genesis.

Ordinary treatment, delivery modes, self-care, anatomy, conditions, ongoing consent and passive
recovery require their own eligibility and time contracts. They may reuse the actual material and
resource operations without inheriting this narrow aftermath policy. Continuous play also requires
authenticated archival continuation across resource, care and material evidence; bounded retained
collections are storage envelopes, not accepted lifetime treatment limits.
