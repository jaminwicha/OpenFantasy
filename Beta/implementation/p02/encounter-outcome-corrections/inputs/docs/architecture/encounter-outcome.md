# Encounter outcome completion (profile 35)

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
sequence) as history. Reading is admitted for an incapacitated actor because it is a controller fact, not a bodily act.
Side-zero bystanders, host residents and unrelated humans are never required and cannot read (`NotAuthorized`); another
controller's delivery is refused at dispatch. An episode with no required human completes at its next lifecycle
boundary. `ReleaseEncounterIntent` is refused in profile 35 at batch construction and by the local adapter.

Completion earns no time (no exploration step, tactical tick or process interval) and keeps every body, position,
HP/MP, readiness debt (the permanent ledger row Outcome already reconciled), possession and source interval, history,
exposure and fractional carry, scenario and episode identity (the action archive stays as the lifecycle's last episode).
It issues no reward, deletes no body, heals or revives nobody, teleports nobody and advances no one's choice. HP 0
remains incapacity, not biological death; no corpse, succession, ghost or resurrection rule is introduced.

## Care in this interpretation

Aftermath care keeps its eligibility and effects unchanged and acts only on an **unfinalized** episode: a patient's
acknowledgment and the capable neutral helper's real dose resume that same episode (Outcome → Active). Completion is
refused while a care obligation is pending, and a reading is refused while care is pending, so completion can never
discard an obligation. Care cutoffs precede completion in the local session, so a patient who has read the defeat may
still ask for admitted care before the world completes it; the treatment then clears the readings of the outcome that no
longer exists. After completion aftermath care is refused (the phase is no longer Outcome) and ordinary restorative use
still refuses an incapacitated recipient: a completed defeat is this interpretation's explicit terminal state. Profiles
25–34 keep the original care-resumes-the-same-fight law and bytes.

## Formats and selection

| Format | Content |
| --- | --- |
| [EncounterOutcomeCodecV1](../../src/OpenFantasy.Core/Encounters/EncounterOutcomeCodecV1.cs) | `encounter.outcome.v1`: version, rules pin, finalized count, reading rows frame, optional record frame; bounded; re-encoded on decode |
| [Action v20](../../src/OpenFantasy.Core/Worlds/WorldActionCodecV20.cs) | ordinary envelope + `world.action.acknowledge-outcome` (episode, archive hash) |
| [Root v35](../../src/OpenFantasy.Core/Worlds/WorldSnapshotCodecV35.cs) | the exact profile-34 fields and order, then the outcome owner (field 26) |
| [Step v35](../../src/OpenFantasy.Core/Worlds/WorldStepCodecV35.cs) | the shared step frame under profile 35 |

No new schema: profile 35 joins the coordinated schema-eight family (lifecycle kind 8 already exists). The explicit
profile lists in batch/frame validation, care/resource/restorative admission, recovery, journal policy, archives and the
paid/proposal/market routes gain 35; nothing else changes meaning.

[WorldEncounterOutcomeAdmission](../../src/OpenFantasy.Core/Worlds/WorldEncounterOutcomeAdmission.cs) admits only an
unplayed fresh profile-34 root (`world.encounter-outcome-admission.v1` frames its basis hash and the rules pin) and never
upgrades a lived save. `CreateCalibratedFresh` admits a *declared* fresh calibration — named actors wounded to 1..current
HP and/or named host residents bound to human control — whose exact rows are framed into a distinct admission identity
(`world.encounter-outcome-calibrated-admission.v1`). Calibrations are qualification fixtures, not balance or history.

[CargoOutcomeWorldAdmission](../../src/OpenFantasy.Hosting/Content/EncounterOutcome/CargoOutcomeWorldAdmission.cs) is
the host composition: the unchanged threatened cargo world followed by profile 35, plus the two declared calibrations
used in qualification (a wounded chief; two inherited wolves bound to human controllers with the first wounded). The
host command `--create-cargo-outcomes <db> <seed> <name>` creates it; the native entry `NativeEncounterOutcome.tscn`
(`--cargo-outcomes [SAVE]`, `--cargo-outcomes-fixture <save> <genesis>`) opens only this world through
`FarmClientSession.OpenCargoOutcomesAsync`. `--cargo-threat`, `--cargo-retreat` and every other selection keep their
profiles and refuse a profile-35 save, and the profile-35 selection refuses theirs.

Retained-save migration (not implemented): moving a lived profile-34 save into profile 35 needs an explicit, replayed,
authenticated transition that first resolves any in-flight Outcome under its recorded law.

## Session and presentation

[LocalWorldSession](../../src/OpenFantasy.Hosting/Simulation/LocalWorldSession.cs) routes a pending reading to the
lifecycle cutoff and selects the actionless completion boundary at a ready cutoff when
`WorldEncounterOutcomes.IsReadyToFinalize` holds. Outcome keeps Full Wait (`Waiting`); a not-ready session selects no
cutoff, so it cannot complete either. [LocalEncounterOutcomeView](../../src/OpenFantasy.Hosting/Simulation/EncounterOutcome/LocalEncounterOutcomeView.cs)
(`LocalWorldView.EncounterOutcome`, omitted when null) projects the stage, result from the observer's side,
participants' condition, the exact reading intent only for a required reader who has not read, the readers remaining,
the restriction (awaiting completion, incapacitated, recovering) and, after completion, the record and `IsTerminal`.
The lifecycle view offers no release token in profile 35.

Native binding: at an unfinalized outcome the encounter controls offer the reading as **Continue** (your side stands) or
**Accept defeat** (incapacitated), with every disabled state explained; admitted care is offered beside it only when the
care view admits it (or it is pending). Status and footer lines state why ordinary action is unavailable and whether
care can reach the player. After a completed defeat, exploration status explains the terminal state and the
[overlay](../../client/scripts/EncounterOutcome/EncounterOutcomeOverlay.cs) offers **Save and leave**, which closes the
session normally without resetting or erasing the save.

## Scope

Qualified by managed committed sequences (victory over a declared wounded chief with continuation and a sale of the
retained cargo, a real defeat by the source chief with terminal reopening, a declared two-human contrast, and care
before completion). Not yet qualified: rendered native journeys, the native result authentication and process-kill
durability at the reading/completion boundaries. Mutual incapacity (`NoSurvivors`) is handled by the same path but is
not reachable by a real committed sequence under the current single-target action rules (the first one-side-standing
resolution already ends the episode). Networked co-op, rewards, death/corpse/ghost/succession and rescue remain separate.
