# Encounter completion and deliberate continuation — design

Status: design written before implementation (this file is the first owned write; it confirms edit access).
Base `cdeb919`. Nothing here changes profiles 1–34, their codecs, bytes, journals or native selections.

## Problem (from source)

Under profiles 24–34 an Outcome leaves only through `ReleaseEncounterIntent`, which
`WorldEncounterLifecycleProcessor` admits only from the *living initiator*. A host-initiated episode whose
initiator is incapacitated (the Bandit Chief beaten) or whose player is incapacitated (the player beaten by the
chief) can never leave Outcome: the winner is unauthorized and the initiator cannot cooperate. The defect is a
permission dead end, not a missing button.

## Selected rule (profile 35, law `encounter.outcome.v1`)

Three distinct facts, three distinct owners/operations:

| Fact | Owner | Operation | Requires | Grants |
| --- | --- | --- | --- | --- |
| The episode is resolved | lifecycle (unchanged) | Active resolution leaves ≤1 living positive side → Outcome | actual HP/body state at the exact resolution boundary | nothing (unchanged law) |
| A required human has read the result | `EncounterOutcomeState` (new root owner) | `AcknowledgeEncounterOutcomeIntent` (controller command) | Outcome phase; exact episode + final action hash; authenticated control of the actor (`CanControl`) and its presence revision; the actor is a **positive-side participant controlled by a Player**; not already acknowledged | a durable receipt and one acknowledgment row; **no** bodily action, HP, time, debt change |
| The episode is complete | world authority | actionless lifecycle boundary at Outcome (system, no actor receipt) | every required human acknowledged this exact outcome; no pending care obligation | Outcome → Exploration retaining everything (`EncounterLifecycleSnapshot.Release` + `WorldPacingState.EndEncounter`), one finalization record |

* **Completion never needs a living initiator, a survivor or a bystander.** It is world-owned and fires on the
  same rule for victory, defeat and zero survivors. Side-zero bystanders, unrelated humans and host actors are
  never required and cannot acknowledge (`NotAuthorized`). A host cannot acknowledge for a human: the command
  must carry the human actor's own control binding, and the local adapter only submits its own player.
* **Episodes with no required human** (NPC vs NPC) finalize at the next lifecycle boundary: there is nobody to
  read, so nothing waits. This keeps the rule extensible to 1–4 players and to world simulation.
* **Acknowledgment ≠ action.** It is admitted for an incapacitated actor because reading is a controller fact,
  not a bodily act: `ValidateControl` checks principal/session and presence revision but not positive HP.
* **Finalization earns zero time** (no exploration step, no tactical tick), and preserves bodies, positions,
  HP/MP, every readiness debt (copied to the permanent ledger exactly as Outcome retained it), possessions and
  source intervals, histories, exposure/fractional carry, scenario and episode identity (the action archive stays
  in the lifecycle as the last episode). It issues no reward, deletes no body, heals/resurrects nobody, teleports
  nobody and advances no other actor's choice.
* **`ReleaseEncounterIntent` is not part of profile 35** (refused at batch construction), so the initiator-only
  law cannot coexist with, or impersonate, the new completion. Profiles 24–34 keep it unchanged.
* **HP 0 remains incapacity**, not biological death: no corpse, succession, ghost or resurrection rule is added.

### Determination (read from actual state, never stored twice)

`WorldEncounterOutcomes.Determine(world)` reads the lifecycle's selected scenario, the action archive's final
content hash and each positive participant's actual HP: `Victory(side)` when exactly one positive side has a
living member, `NoSurvivors` when none does. Pending acknowledgments store only (actor, command, episode, action
hash, world sequence); the result is *recomputed* while pending. The finalization record stores the result as a
historical receipt of what was finalized (HP may later change by lawful care), together with the acknowledging
actors and world sequence.

## Human readiness

Outcome holds the world (Full Wait: `Waiting` run state; no tactical tick, exploration step or NPC work) until
every required human acknowledges. One acknowledgment of two required humans is recorded and the world keeps
holding; a disconnected/not-ready human simply never acknowledges and the hold persists (no timeout, no host
substitute). A not-ready local session selects no cutoff at all, so it cannot finalize either. Exact retries
return the original receipt (authority receipt lookup precedes evaluation); a different command for an already
acknowledged actor is refused (`Unavailable`) without changing state.

## Care interaction (profile 35)

The original aftermath-care law is retained *unchanged in meaning* for profiles 25–34 (acknowledge care →
helper's real dose → **resume the same fight**). In profile 35 it is specified as:

* Care acts on an **unfinalized** episode only: `AcknowledgeCareIntent` is admitted at Outcome (unchanged
  eligibility: exact outcome, lethal damage, unchanged boundary, HP 0, capable neutral helper with owned dose).
  Treatment resumes that same unfinalized episode (Outcome → Active) exactly as before.
* **Finalization never occurs while a care obligation is pending** (it is refused, so it can never discard it).
  An outcome acknowledgment is refused while care is pending.
* When treatment resumes the episode, the outcome owner's pending acknowledgments are cleared in the same
  transaction: they read an outcome that no longer exists, so they cannot complete a later outcome.
* After finalization the episode is complete: aftermath care is refused (phase is no longer Outcome), so a
  completed fight is never resumed. An incapacitated actor in exploration has no ordinary action, and ordinary
  restorative use still refuses an incapacitated recipient. That is the explicit terminal state of this
  interpretation; no automatic heal, invented helper, stock, immunity window or reward is added.

## Product continuation (native)

* **Victory:** the panel names the result and the defeated opponent, pauses for reading, and offers *Continue*
  (= the player's acknowledgment). The world then finalizes; the player walks and uses retained inventory
  afterwards, paying retained readiness debt first.
* **Defeat:** the panel states why ordinary action is unavailable (HP 0 — incapacitated), lists care only when the
  existing care view offers an *eligible* helper (otherwise says why care is unavailable), and offers *Accept
  defeat* (acknowledgment). After finalization a terminal panel explains that this character cannot act, that no
  admitted care can reach them, and offers *Save and leave* (closes the session; the database is never reset,
  erased or overwritten; reopening shows the same state).
* **No survivors:** same as defeat with a "no side is standing" result.
* No unexplained disabled Continue exists: every disabled choice carries its reason.

## Versioning and persistence

| Item | Change |
| --- | --- |
| Profile 35 | `WorldEncounterOutcomeAdmission.CreateFresh` admits only an **unplayed fresh profile-34** root plus the new owner; frame `world.encounter-outcome-admission.v1` pins the basis hash and the rules pin. Never upgrades a lived save. |
| Owner | `EncounterOutcomeState` + `EncounterOutcomeCodecV1` (`encounter.outcome.v1`, bounded, re-encoded on decode). |
| Action | `AcknowledgeEncounterOutcomeIntent` + `WorldActionCodecV20` (ordinary envelope). Profile 35 only, lifecycle kind 8. |
| Root | `WorldSnapshotCodecV35`: the exact profile-34 field order, then the outcome owner (field 26). |
| Step | `WorldStepCodecV35`: the shared step frame under profile 35. |
| Schema | No new schema: profile 35 joins the coordinated schema-eight family (kind 8 already exists). Only the explicit profile lists gain 35. |
| Selection | Host `--create-cargo-outcomes <db> <seed> <name>`; native `--cargo-outcomes [SAVE]` / `NativeEncounterOutcome.tscn`. A save of any other profile/admission is refused, never upgraded. Existing `--cargo-threat`, `--cargo-retreat` etc. keep their profiles. |

Retained-save migration obligation (not implemented): moving a lived profile-34 world into profile 35 would need an
explicit, replayed, authenticated transition that resolves any in-flight Outcome under the old law first. Until
then such saves stay profile 34 (and their stuck Outcomes stay stuck under their own recorded law).

Composition: `CargoOutcomeWorldAdmission` = the unchanged road-threat composition (farm → cargo → chief → paid →
counters → profile 34) followed by profile-35 admission. Declared fresh calibrations for the native proof (e.g.
a wounded chief) are separate, labelled admissions of fresh worlds, never lived history or production balance.

## Verification plan

* Core (`Encounters/EncounterOutcome`, `Worlds/EncounterOutcome`): codec canonical/malformed/bounds; admission of
  acknowledgments (wrong episode, wrong hash, wrong controller, side-zero, host actor, duplicate, wrong phase);
  determination for victory/defeat/no survivors; finalization refused with missing acks or pending care;
  finalization preserves every field listed above and earns zero time; care-resume clears acks; release refused.
* Hosting (`Simulation/EncounterOutcome`, `Content/EncounterOutcome`): committed sequences through the SQLite
  authority and local session — entry, resolution, acknowledgment, finalization, continuation, reopening at each
  boundary, exact retries, changed content under the same command id, occupied identities, two-human contrast.
* Native/durability per the assignment's wrappers and fixture conventions.
