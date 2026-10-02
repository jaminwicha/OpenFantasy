# Encounter outcome corrections — design

Access confirmed by an owned write of this file at unit start. Base `cdeb919`; profile 35 law `encounter.outcome.v1`
is unchanged in meaning; profiles 1–34 untouched. Inputs read: Beta/AGENTS, original ASSIGNMENT/RESULT/DESIGN/
INTEGRATION_DECISIONS/LEAD_DURABILITY_REVIEW, critic REVIEW (F1–F6), immutable architecture snapshots.

## Corrections (smallest owner for each)

| Finding | Owner | Change |
| --- | --- | --- |
| F1 pin | `EncounterOutcomeState` ctor | refuse `rulesPin != Pin` → owner codec and full root decode refuse (FormatException) |
| F6 cross-owner | `WorldEncounterOutcomeAdmission.Validate` | when the lifecycle still retains the record's episode archive, record hash must equal it; documented as structural (replay authenticates history) |
| F2 facts vs condition | `LocalEncounterOutcomeView` | participants carry side + `OnStandingSide` (outcome fact) and `Condition` (present HP) only for the observer or a participant observed under `LocalRoadThreatView.Observes` (same floor, sightline, 3 cells); never history |
| F5 observer | view + text + offers | `ObserverIsParticipant`; observer gets no reading offer, "between others" wording, never "you have read" |
| F3 care copy | `EncounterOutcomeText` | every reason from `LocalCareView` (no agreement / OutOfReach / RevisionConflict / NotAuthorized / unable; pending-but-unavailable); terminal states completion policy; "Accept outcome" for an incapacitated winner; decline wording when care is offered |
| F4 window | text + docs + router | reading = acceptance; next ready cutoff completes and closes care; ordering of already-committed care unchanged; router lets the profile-35 reading stand beside admitted care (`HasOutcomeReadingBesideCare`, default keeps old law) |
| Layout | `EncounterOutcomeOverlay.tscn` | scene-owned panel (upper-left of field, content-sized above the encounter footer); C# keeps semantics |

## Proofs

Managed (Worlds.EncounterOutcome namespace, outcome-unit): complete-root decode negatives over real two-human roots
(foreign pin, side-zero / host reader, stale hash, other episode, future sequence, record naming current episode,
impossible count, out-of-phase reading, retained-archive hash mismatch, non-resident reader) plus a structural-admission
positive; two-human care contrast (player HP1 + human wolf; wolf reads, patient requests Wren's care, one treatment
commit resumes the same episode and clears the reading; FindReceipt retry returns the old receipt; stale hash refused
next outcome); deliberate decline at the next ordinary cutoff with terminal reopening; two completed episodes over the
two inherited wolves (count 2, Last replaced, view cleared at entry, occupied command identities, retained receipt);
F2 victory→Kegga and side-zero observer assertions in existing journeys; pure-client text reasons.

Native: `NativeEncounterOutcomeFixtureTests` (managed preparation only, declared calibrations recorded),
`EncounterOutcomePlayProbe` (victory / defeat + Save and leave / care-decline / reopen), `check_native_encounter_outcome.py`
(original limits, background rendering set before windows, post-draw bounds), `NativeEncounterOutcomeResultTests`
(re-derived genesis bytes, whole-journal replay, native command identities). Durability checker: explicit elapsed checks
after successful signal/kill waits and operations, bounds unchanged.

No-survivor: investigated, not lawfully reachable under the single-target resolver (documented; no new combat rule).
