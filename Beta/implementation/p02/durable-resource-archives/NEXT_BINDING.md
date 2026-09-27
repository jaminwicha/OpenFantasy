# Next care binding and retained obligations

The SQLite authority accepts trusted selected profile 25 Care batches. This does not bind those
batches to the ordinary scheduler, player projection or Godot controls. Source mapping below was
reviewed independently while durable tests ran; it is an implementation dependency, not a new scope.

## Host and controller selection

`Simulation/LocalWorldSession.ValidateEncounterSelection` rejects care during Outcome, and
`TickEncounter` returns while the outcome is held. Add a narrowly scoped care selection/dispatch
before those branches; use exactly one command and no movement. `ReadRunState` must distinguish
pending care from active scheduling without earning time. Preserve player-only `SubmitAction` and
its current `_delivery`: never submit a helper command as the patient. Acknowledgment receives its
own terminal receipt through the existing commit path; helper work is a later independent selection.

`LocalNpcController.SelectActions` excludes encounters and its encounter selector excludes outcomes.
Add a cohesive helper selection operation using `WorldCareActions.PrepareRemedy`, actual Host resident
status and that helper's admitted control binding. The authored agreement is permission, not actual
commitment. Define rejected-helper behavior explicitly; do not repeatedly auto-retry a failed action.

## Projection and presentation

`LocalWorldView` needs a bounded public care view with the patient's exact acknowledgment inputs
(agreement, encounter, lifecycle revision, action hash), eligibility/reason, pending status and known
helper identity. Keep helper control credentials, private inventory and provenance out of that view.
Do not weaken `LocalEncounterActionView`'s physical-incapacity restrictions to make acknowledgment fit.

`EncounterActionOffers.Create` currently exposes release at Outcome. Add a separate acknowledgment
offer and pending status, without offering exploration release from an unresolved care obligation.
`FarmActionOffers`' encounter/zero-HP branches must respect that distinction. Preserve captured intents,
issued-offer registration, session binding and same-offer idempotency through `FarmClientSession`.
Update action routing, wheel slots/stale comparisons, icon/label mapping, HUD/status and scene controls
coherently. Refresh must not silently substitute agreement, helper, acknowledgment or selected dose.

## Admission and capability preservation

`Content/EncounterJourneyAdmission.Create` and `FarmClientSession.OpenEncounterAsync` bind profile24.
Add explicit fresh care composition using actual retained remedy content and real helper stock.
Preserve old save selection and bytes; do not silently upgrade lived worlds. The session excavation
allow-list stops at24 although25 retains its worksite: extend that exact supported contract with tests.
Do not mechanically enable assisted climbing, transport or runoff merely because25 is newer; those
capabilities have distinct profile/spatial contracts.

## Proof sequence

Before broad durable-care acceptance, add the combined boundary: fill the active resource segment
through actual combat ending in defeat, acknowledge it, and publish the archive with the subsequent
raw/crafted treatment in the same commit. Kill/reopen that exact combined transaction and preserve
the acknowledged obligation/receipt, archived lethal witness, one dose and helper debt. Current
durable probes exercise resource publication and treatment separately; the Core pending-care cut
does not by itself qualify that combined stored journey. Also retain adversarial pending-care/currency
edits and treatment→later reward/refill→attack replay as explicit trace combinations.

Prove real session player/helper controller separation, exact offers, stale/duplicate rejection,
pending acknowledgment reload, one raw/crafted dose, no free time and resumed combat/debt. Keep
counterexamples for an unavailable/helper-uncontrolled actor and a stale dose. Then exercise actual
Godot wheel/keyboard/controller/pointer actions, save/reopen and visible feedback under the existing
shared-desktop rules. Native evidence and physical/human acceptance remain distinct.

Core aftermath care requires an actual retained lethal outcome. Ordinary healing, conscious/incapacitated
delivery, anatomy, conditions and passive processes remain their own medical policies over the same
resource/material laws. Retain every original full-scope requirement and feasibility gate.
