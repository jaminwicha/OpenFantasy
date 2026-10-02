# Encounter outcome completion (profile 35)

[General assisted recovery](general-recovery.md) explicitly extends the controller-aware world in
profile 37. Completion still closes encounter-specific aftermath care, but preserves separately
authorized ordinary recovery. Zero HP after completion is therefore not terminal in that interpretation;
the world continues without silently healing, moving or promising rescue to the character.

Under profiles 24–34 an Outcome leaves only through `ReleaseEncounterIntent` from the *living initiator*. When a
host-initiated episode ends with the initiator incapacitated (the Bandit Chief beaten), or with the player
incapacitated, nobody can release it. Profile 35 separates three facts that the old law fused, so no outcome depends on
which actor initiated or survived. Profiles 1–34 keep their codecs, bytes, journals and meaning.

## The law (`encounter.outcome.v1`)

| Fact | Owner | Operation | Requires | Grants |
| --- | --- | --- | --- | --- |
| Resolved | lifecycle (unchanged) | Active resolution leaves ≤ 1 living positive side → Outcome | actual HP at the exact resolution boundary | nothing |
| Read | [EncounterOutcomeState](../../src/OpenFantasy.Core/Encounters/EncounterOutcomeState.cs) | [AcknowledgeEncounterOutcomeIntent](../../src/OpenFantasy.Core/Encounters/AcknowledgeEncounterOutcomeIntent.cs), lifecycle kind 8 | Outcome; exact episode and final action-archive hash; the actor's own control binding and presence revision; the actor is a positive-side participant controlled by a player; not yet read | one reading row and a receipt; no bodily action, time, HP, debt or reward |
| Completed | world authority | actionless lifecycle boundary at Outcome (no actor receipt) | every required human has read this exact outcome; no care obligation pending; no reading hold | Outcome → Exploration (`Release` + `EndEncounter`) and one completion record |

[WorldEncounterOutcomes](../../src/OpenFantasy.Core/Worlds/WorldEncounterOutcomes.cs) recomputes the result from the
canonical owners (lifecycle archive, progression HP, resident control) while it is pending: `Victory(side)` when one
positive side has a living member, `NoSurvivors` otherwise. Readings store only actor, command, episode, archive hash and
world sequence; the completion record keeps what was finalized (episode, archive, result, standing side, readers,
sequence) as history and never stores anyone's health. Reading is admitted for an incapacitated actor because it is a
controller fact, not a bodily act. Side-zero bystanders, host residents and unrelated humans are never required and
cannot read (`NotAuthorized`); another controller's delivery is refused at dispatch. An episode with no required human
completes at its next lifecycle boundary. `ReleaseEncounterIntent` is refused in profile 35 at batch construction and by
the local adapter.

Completion earns no time (no exploration step, tactical tick or process interval) and keeps every body, position,
HP/MP, readiness debt (the permanent ledger row Outcome already reconciled), possession and source interval, history,
exposure and fractional carry, scenario and episode identity (the action archive stays as the lifecycle's last episode).
It issues no reward, deletes no body, heals or revives nobody, teleports nobody and advances no one's choice. HP 0
remains incapacity, not biological death; no corpse, succession, ghost or resurrection rule is introduced.

The owner holds one latest record and a finalized count; a later completion replaces the record and increments the
count, so the owner stays bounded without any lifetime cap. Command identities are journal-wide: an earlier reading or
entry command identity can never carry new content, and its exact retry returns its original receipt.

## Accepting an outcome and care

Reading is **acceptance**: it asks the world to continue. Once every required human has read, the next ready world
boundary completes the episode, and completion closes the aftermath-care opportunity. Care is therefore chosen *before*
accepting; there is no player-facing care window after reading, and the presentation says so (accepting declines an
admitted offer).

Aftermath care keeps its eligibility and effects unchanged and acts only on an **unfinalized** episode: a patient's
acknowledgment and the capable neutral helper's real dose resume that same episode (Outcome → Active). Completion is
refused while a care obligation is pending, and a reading is refused while care is pending, so completion can never
discard an obligation. The authority's deterministic ordering is unchanged: a care acknowledgment already committed
before the completion boundary (including one submitted after another human's reading) is honoured, and its treatment
clears, in the same transaction, every reading of the outcome that no longer exists. An exact retry of such a cleared
reading returns its original receipt and satisfies nothing in the resumed episode; its stale archive hash is refused for
the next outcome. A pending obligation whose helper becomes unable to act keeps the episode open (the care law has no
expiry). After completion aftermath care is refused (the phase is no longer Outcome) and ordinary restorative use still
refuses an incapacitated recipient: a completed defeat — including an incapacitated member of a winning side — is this
interpretation's explicit terminal state. Profiles 25–34 keep the original care-resumes-the-same-fight law and bytes.

## Formats, admission and selection

| Format | Content |
| --- | --- |
| [EncounterOutcomeCodecV1](../../src/OpenFantasy.Core/Encounters/EncounterOutcomeCodecV1.cs) | `encounter.outcome.v1`: version, rules pin, finalized count, reading rows frame, optional record frame; bounded; re-encoded on decode |
| [Action v20](../../src/OpenFantasy.Core/Worlds/WorldActionCodecV20.cs) | ordinary envelope + `world.action.acknowledge-outcome` (episode, archive hash) |
| [Root v35](../../src/OpenFantasy.Core/Worlds/WorldSnapshotCodecV35.cs) | the exact profile-34 fields and order, then the outcome owner (field 26) |
| [Step v35](../../src/OpenFantasy.Core/Worlds/WorldStepCodecV35.cs) | the shared step frame under profile 35 |

The rules pin names the only law the owner can interpret: the owner constructor refuses any other pin, so the owner
codec and the complete root decode refuse it too, and a future law's owner can never be read as this one.

Structural admission (`WorldEncounterOutcomeAdmission.Validate`, run whenever a root is built or decoded) checks what the
root alone can show: readings exist only at Outcome and name the current episode, its exact final archive hash, a
required human and committed time; a record names residents and committed time, never the current episode, and the
finalized count never exceeds the episodes begun; while the lifecycle still retains the record's own episode archive,
the record names that archive's exact final hash. It authenticates no history: a well-formed reading the journal never
committed is structurally admissible. Replay of the complete journal from genesis (authority open and the native result
authentication) authenticates which readings, completions and records actually happened, including records whose
archive a later entry or retreat has replaced.

No new schema: profile 35 joins the coordinated schema-eight family (lifecycle kind 8 already exists). The explicit
profile lists in batch/frame validation, care/resource/restorative admission, recovery, journal policy, archives and the
paid/proposal/market routes gain 35; nothing else changes meaning.

[WorldEncounterOutcomeAdmission](../../src/OpenFantasy.Core/Worlds/WorldEncounterOutcomeAdmission.cs) admits only an
unplayed fresh profile-34 root (`world.encounter-outcome-admission.v1` frames its basis hash and the rules pin) and never
upgrades a lived save. `CreateCalibratedFresh` admits a *declared* fresh calibration — named actors wounded to 1..current
HP and/or named host residents bound to human control — whose exact rows are framed into a distinct admission identity
(`world.encounter-outcome-calibrated-admission.v1`). Calibrations are qualification fixtures, not balance or history;
evidence that uses one records its seed, player name and rows so the genesis is re-derived and compared byte for byte.

[CargoOutcomeWorldAdmission](../../src/OpenFantasy.Hosting/Content/EncounterOutcome/CargoOutcomeWorldAdmission.cs) is
the host composition: the unchanged threatened cargo world followed by profile 35, plus declared calibrations used in
qualification (a wounded chief; inherited wolves bound to human controllers). The host command
`--create-cargo-outcomes <db> <seed> <name>` creates it; the native entry `NativeEncounterOutcome.tscn`
(`--cargo-outcomes [SAVE]`, `--cargo-outcomes-fixture <save> <genesis>`) opens only this world through
`FarmClientSession.OpenCargoOutcomesAsync`. `--cargo-threat`, `--cargo-retreat` and every other selection keep their
profiles and refuse a profile-35 save, and the profile-35 selection refuses theirs.

Retained-save migration obligation: moving a lived profile-34 save into profile 35 needs an explicit, replayed,
authenticated transition that first resolves any in-flight Outcome under its recorded law. No implicit upgrade exists.

## Session and presentation

### Controller-world acknowledgment

The canonical acknowledgment is a controller fact. Worlds with the controller facet use
`LocalOutcomeAcknowledgmentMode.ControllerAcceptance` in their outcome view; ordinary profile-35
views retain `HumanReading`. Compatibility field names such as `ObserverHasRead` and `RequiredReaders`
describe character acknowledgments in controller mode, not evidence that a person read anything.
Both pending and completed projections derive the mode from the world's interpretation, never the
actor's current Human or Autonomous assignment. A human rejoin therefore cannot change historical
meaning. UI text consistently describes acceptance and neutrally attributed care requests.

The [autonomous aftermath policy](controller-lifecycle.md#autonomous-aftermath) can request currently
legal pre-agreed care for an incapacitated character, or acknowledge an outcome for a living character.
These are ordinary commands with ordinary authority checks. It never automatically accepts a terminal
incapacitated outcome, creates consent or replaces an unavailable pending helper. The unchanged care
and completion laws still determine all effects and holds; unavailable recovery remains an explicit
domain boundary.

### Player choices and completion

[LocalWorldSession](../../src/OpenFantasy.Hosting/Simulation/LocalWorldSession.cs) routes a pending reading to the
lifecycle cutoff and selects the actionless completion boundary at a ready cutoff when
`WorldEncounterOutcomes.IsReadyToFinalize` holds; care cutoffs (a pending care acknowledgment or obligation) are routed
first. Outcome keeps Full Wait (`Waiting`); with no ready player port the host selects no ordinary cutoff and cannot
complete it. [Shared player connections](hosted-player-connections.md) submit each human's reading under its own
control binding and observe their own outcome standing. A ready peer can complete the world boundary even while
the original local port is paused, after every required reading and pending obligation is satisfied. In-process
ports do not by themselves establish networked or native multi-client readiness.

[LocalEncounterOutcomeView](../../src/OpenFantasy.Hosting/Simulation/EncounterOutcome/LocalEncounterOutcomeView.cs)
(`LocalWorldView.EncounterOutcome`, omitted when null) keeps **outcome facts** and **present condition** apart. Facts:
the stage, the result from the observer's side, each participant's side and whether it is the standing side, the
readers remaining and, after completion, the record's readers and sequence. Condition: a participant's current HP is
projected only for the observer itself or while the observer currently observes that participant under the road view's
bound (same floor, clear sightline, three cells — `LocalRoadThreatView.Observes`); otherwise it is absent, before and
after completion. The exact reading intent is offered only to a required reader who has not read. An observer outside
the fight (`ObserverIsParticipant` false) owes no reading, is offered none and is never told it has read. The lifecycle
view offers no release token in profile 35.

Native binding: the encounter controls offer the reading as **Continue** (your side stands and you can act), **Accept
outcome** (your side stands but you are incapacitated) or **Accept defeat**, with every disabled state explained.
Admitted care is offered beside it only when the care view admits it (or it is pending); E / A then requests care.
The reading remains on its HUD button without a duplicate E hint and in the wheel's right sector; care remains in the
left sector. V opens choices without committing either action. The reading's explanation says that accepting declines
available care, and completion waits for every required reading and no pending obligation. Compact status and preview
lines keep the ordinary HUD readable. The wheel and button describe the complete commitment concisely: completion
conditions, retained incapacity or recovery, and any declined care. The outcome card carries the extended explanation.
Status lines derive care reasons
from the care view: a missing view means no care is offered, not that no agreement exists; a pending patient request does
not assert helper commitment. Actual projected refusals distinguish reach, stale terms, coverage and capability.
After a completed defeat, exploration status and
the [overlay](../../client/scripts/EncounterOutcome/EncounterOutcomeOverlay.cs) state the completion policy (aftermath
care closes when an outcome is accepted; ordinary care cannot treat an incapacitated character) and offer **Save and
leave**, which closes the session normally without resetting or erasing the save. The overlay's editable layout is the
scene [EncounterOutcomeOverlay.tscn](../../client/scenes/EncounterOutcomeOverlay.tscn): upper left of the field below the
HUD header, sized to its content above the encounter footer, clear of the target bar and the centred combatants;
semantic wording stays in `EncounterOutcomeText`.

The client submission boundary exposes opt-in passive observations through
[FarmSubmission](../../client/scripts/FarmSubmission.cs): the submitted issued offer, its actual command and canonical
bytes, the original receipt task and whether the same offer was retried. With no observer, the boundary allocates no
observation or diagnostic encoding. An observer failure is retained separately and cannot discard the already queued
command's receipt task. Canonical byte reads return copies; the session retains no observation history. The Godot binding
permits probe subscription only for an explicitly selected disposable save. Ordinary command ownership, admission and
idempotency remain in the authority.

Native evidence captures the presented offer before input, then uses only the generated command identity to encode that
captured offer. Its complete bytes, including nested market terms, must match the actual submission and stored journal
command; receipt bytes and identities are authenticated separately. Navigation that changes presentation finishes before
capture. A stale selected action is sent unchanged and refused; it is never silently recaptured or retried. Probe history
is bounded and observation failures invalidate the evidence. These diagnostics do not grant permission or replace the
authority's validation.

## Native accepted-care observation

The disposable `care-accepted` route in
[EncounterOutcomePlayProbe](../../client/scripts/NativeAcceptedCare/EncounterOutcomePlayProbe.AcceptedCare.cs)
starts from an unplayed, uncalibrated outcome-world genesis. It walks away from the adjacent wolf and back through
ordinary movement input, selects and enters the encounter, and pays for Wait choices until actual attacks incapacitate
the player. E requests admitted care; the probe waits for the separately committed restoration before issuing another
combat choice. It retains the same episode through treatment, a second defeat, outcome acceptance and terminal save.
The `care-reopen` route opens that save paused without issuing commands. Neither route grants resources, suppresses
ordinary assistance, changes health directly or substitutes a prepared encounter for native approach.

The route shares the existing passive submission and bounded post-draw observers. Its new fixture and result checks
live under [NativeAcceptedCare](../../tests/OpenFantasy.Hosting.Tests/Worlds/NativeAcceptedCare/NativeAcceptedCareResultTests.cs).
Full replay authenticates both patient and helper commands, their distinct control bindings and cutoffs, the exact
retired source interval, restoration, episode identity and completion. A patient's acknowledgment is insufficient proof
of treatment. Helper private stock stays out of the player projection; the authority-side verifier reads it from the
saved journal. A solo route with no earlier outcome readings establishes an empty reading set after treatment; the
separate managed contrasts establish invalidation of nonempty prior readings.

## Design boundaries

Mutual incapacity (`NoSurvivors`) is handled by the same determination, record and presentation path. Under the retained
single-target resolver one resolution changes one target and the first resolution that leaves at most one positive side
standing moves the episode to Outcome, cancelling every other pending choice; with the attacker able to act at that
boundary, no admitted entry and action sequence leaves zero sides standing. A simultaneous or area resolution rule would
be needed for a lived no-survivor episode; this law does not add one. Networked co-op, rewards,
death/corpse/ghost/succession and rescue remain separate subjects.
