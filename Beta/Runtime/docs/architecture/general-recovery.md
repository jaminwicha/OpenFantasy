# General assisted recovery

## Ownership and admission

[WorldRecoveryPolicy](../../src/OpenFantasy.Core/Worlds/WorldRecoveryPolicy.cs) pins the law permitting
assisted restoration from incapacity after combat. It owns no health, injury, possession, permission
or encounter history. [WorldRecoveryAdmission](../../src/OpenFantasy.Core/Worlds/WorldRecoveryAdmission.cs)
selects profile 37 over an unplayed controller-aware world with explicitly authored recovery definitions
and grants. Existing catalogs remain intact; admission pins the basis, policy and new care owner. A
lived save requires a separate deliberate migration.

Progression remains the current HP owner; body condition owns anatomy and injury. HP zero means
incapacity under this policy, not biological death. HP restoration does not regrow a limb, remove a
condition, teleport, clear readiness debt or guarantee safety. Players and NPCs use the same law.

## Permission and complete publication

`RestorativeUseDefinition.CreateIncapacitatedAssistance` pins an existing remedy and exact corporeal
form versions. Its distinct delivery mode requires current zero HP. Self-use and living assistance
retain their positive-HP rules; their original constructor cannot accidentally admit the new mode.

A separate exact `CarePermission` names the patient, helper and recovery definition/version. Living
assistance, aftermath agreements, party relations and custody permissions cannot substitute for it.
Captured revisions and active grants are checked against ordered revocations. Authored permission is
not inferred consent; runtime grant creation remains a separate operation.

[WorldRestorativeActions](../../src/OpenFantasy.Core/Worlds/WorldRestorativeActions.cs) captures and
rechecks the ordinary `UseRestorativeItemIntent`. The supplier needs actual control, positive HP,
readiness, manipulation, spatial reach and the exact owned dose. Receiving aid requires neither the
patient's manipulation nor zero debt. Missing/revoked permission, unsupported form, stale selection,
exhaustion or unavailable stock cannot publish treatment.

[WorldRestorativeResolver](../../src/OpenFantasy.Core/Worlds/WorldRestorativeResolver.cs) composes the
existing material planner, an explicit zero-before use witness and causal resource evidence. One
ordinary advancing transaction publishes the consumed dose, capped HP gain, 1,800-ms exploration
interval and terminal receipt. Supplier work excludes its movement sample. Calendar, processes and
debt follow ordinary rules. A rejected advancing command still advances time but spends no remedy
and restores no HP.

General recovery requires no latest encounter, lethal attack, surviving initiator or aftermath
acknowledgment. A later encounter may replace the retained archive without removing treatment
eligibility. Encounter entry still requires capable positive-side participants; an incapacitated
bystander cannot be placed in an active combat role to bypass that law.

## History and formats

[CareCodecV4](../../src/OpenFantasy.Core/Care/CareCodecV4.cs) explicitly encodes inline/continued
recovery catalogs, distinct recovery definition/use records and continuation markers. Care V1–V3 and
legacy constructors refuse those semantics while retaining old bytes.
[WorldSnapshotCodecV37](../../src/OpenFantasy.Core/Worlds/WorldSnapshotCodecV37.cs) carries the exact
policy pin, complete controllers and Care V4. Its step codec retains ordinary actions and isolated
controller events; schema-nine journal transactions preserve the existing durability guarantees.

Resource admission reconstructs the actual historical zero-HP effect and exact permission/dose
evidence. Coordinated cuts preserve grants, revocations, counts, chronology and occupied identities.
Cuts do not create supply or eligibility. Reopening authenticates commands and archives; exact
receipt retrieval cannot consume or restore twice.

## Host and presentation

Autonomous outcome acknowledgment at zero HP requires this recovery policy. Completion retains
the body, HP, injuries and possessions; it promises neither rescue nor supplies. The ordinary host
helper controller selects permitted recovery through its own entitlement and shared preflight.
Player-controlled helpers are not conscripted by that NPC policy.

Outcome views disclose `AllowsAssistedRecovery` as a policy meaning, not proof of an available helper
or individual grant. Observation body version 3 carries it; older false defaults remain omitted.
Completed incapacity is not labeled terminal under this law. Status/tooltips explain permission,
medicine and work; movement and combat hints remain incapacitated even during a later encounter.
An absent old outcome does not imply healed HP: current progression remains authoritative. Available
aftermath care retains its acceptance warning: closing that encounter declines that offered care;
the general recovery policy does not establish a separate usable patient permission.

An unmatched aftermath request still blocks completion. Supporting explicit patient cancellation or
helper decline requires retained disposition/actor/command evidence, an admitted archive relation
between acknowledgments, treatments and closures, and a bounded closed-defeat frontier. It cannot
be fabricated as successful treatment. A later general recovery remains a separate operation.
