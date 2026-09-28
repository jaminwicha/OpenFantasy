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

## Selection and complete publication

[WorldRestorativeActions](../../src/OpenFantasy.Core/Worlds/WorldRestorativeActions.cs) evaluates actual
eligibility and can prepare a specific owned dose for an explicitly selected policy and permission.
Preparation is neither authentication nor reservation. The request captures patient presence/resources,
storage, production, definition and permission revisions. Publication never silently chooses another dose.

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
calendar, experience and processes exactly once. It neither resets debt nor grants eligibility from
readiness earned later in the step. A selected advancing step still advances when a request is rejected;
inspection and cancellation that submit no command do not create a step.

[LocalWorldSession](../../src/OpenFantasy.Hosting/Simulation/LocalWorldSession.cs) accepts an explicit
controlled-player ordinary request only at its admitted, unheld exploration boundary. The session's
readiness gate and ordinary selected cutoff still apply. A reading hold or encounter boundary refuses
selection before an incompatible batch can be built. Exact completed receipts remain reusable while
paused; receipt lookup cannot repeat the treatment. Generic command submission does not itself supply
public offers, player controls or an assisted-use controller.

Full health, zero HP, an unsupported form, revoked permission, stale selection, missing capability or
unavailable dose cannot spend a restorative item. Zero HP remains governed by the separate aftermath
policy. Active-combat item use requires its own due-actor choice/resolution binding under Wait; it cannot
use an ordinary advancing command or a zero-time Care batch to bypass encounter timing.

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

[RestorativeJourneyAdmission](../../src/OpenFantasy.Hosting/Content/RestorativeJourneyAdmission.cs)
composes a fresh source-backed scenario with the original Potion effect, recipe, combat stats and
quest rewards. It partitions the same two raw source doses between player and Wren, retaining six
berries in Wren's custody. This explicit fresh distribution changes neither deposit policy nor the
existing care scenario. The player's corporeal definition/version binds the delivery policy; stock,
range, prior permission and 1,800 ms use duration are authored adaptations rather than original item facts.

Native menus, ordinary helper selection, combat use, ongoing activities, passive recovery, conditions,
injury repair and broad medical delivery require separate application and domain bindings. Bounded
care/material collections require admitted continuation; they do not define a character's lifetime
number of treatments. In the bounded interpreter, `Prepare` checks an actual dose and delivery eligibility
but does not reserve or check retained-history headroom. At 256 total consumption claims, the material
planner refuses with `Unavailable` and preserves the dose and resource/care state; a resource-history archive cut
does not free that separate material owner. Ordinary use records have their own 256-entry bound.
Player-facing offers require a distinct capacity/preflight outcome and admitted continuation so an
implementation limit cannot masquerade as missing medicine or denied permission. Human/device, art
and production acceptance are separate from these contracts.
