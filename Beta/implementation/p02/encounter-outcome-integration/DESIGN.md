# Outcome input and presentation integration — design

Access confirmed by an owned write of this file at unit start. Base `cdeb919` plus the delivered corrective overlay
(checkpoint `e4b1bb8`). Core/Hosting/Host production source and laws are frozen; profile-35 bytes are unchanged.
Inputs read: shared instructions/handoff, the corrective RESULT/DESIGN/DELIVERY_REVIEW, LEAD_REVIEW_NOTES, the UI
supplement, the encounter-outcome architecture page, and the actual client/probe/checker/test sources and images.

## 1. HUD readability (established cause)

`FarmHud.tscn` (protected) puts the non-wrapping `Movement` hint label in the `Actions` row beside the buttons. In
exploration the cargo world shows four farm buttons, so the row is already ~at the footer width with `WASD / arrows`.
The terminal hint (`… Save and leave keeps this save`) and long outcome hints/blockers raise the row's minimum width
above the viewport; the footer `PanelContainer` (grow both) then extends past both edges, and its autowrapped labels
wrap at the overflowed width (observed `defeat.terminal.png`, `care.care-declined.png`). Long autowrapped status/preview
text also grows the footer upward into the field.

Correction, layout unchanged: every outcome hint is two short lines (`Incapacitated` / `Esc: pause` at a terminal
defeat); HUD status uses compact `EncounterOutcomeText` forms (`TerminalStatus`, `Status`) with the full text in the
status tooltip and the outcome card; the inline preview of outcome/care offers is one concise line (full `Explain` stays
in the button tooltip, the wheel inspector and the card). Legacy offers keep their exact preview/tooltip text.
Proof: the probe captures the image first, then records post-draw rects of the header, footer, target bar, every visible
footer label/button and the card, checking viewport containment, unclipped text (visible lines ≥ lines; the
non-wrapping hint's text width ≤ its rect), card clear of target bar/sprites, and footer/header clear of both sprites at
pending care, terminal defeat (including after a decline) and victory (outcome and completed).

## 2. Independent care and outcome choices

Router: E/A keeps requesting care whenever care is offered (unchanged); `HasOutcomeReadingBesideCare` (profile 35 only)
still permits the separate reading; legacy care keeps refusing release. HUD: the reading button beside care shows its
label (`Accept defeat`/`Accept outcome`) and no `[E]`; its tooltip names pointer/wheel routes. Wheel: when both offers
exist the reading takes the otherwise unused right slot (1) and care keeps the left slot (3); every other layout is
unchanged. Sector wording comes from the reading offer's label (`Accept defeat` / `Accept outcome` / `Continue`).
Pure regressions cover router, wheel layout/selection/labels and key hints. Native: a separate `care-wheel` route
from the same care fixture declines through V → right sector → Enter and must submit only the reading.

## 3. Exact selection-to-receipt evidence

`FarmClientSession.SubmitOffer` (the only world-command boundary of the client) raises a passive `Submitted`
notification with a new immutable `FarmSubmission`: the exact offer object passed in, the command built from it, its
canonical bytes and the receipt task, plus whether it is a retry. The session stores nothing new: a retry of the same
offer object returns the original task and reports the original command. `FarmGame` only forwards the notification;
no observer exists in normal play. The probe (bounded to 128 rows) snapshots the presented offer before each injected
input (readiness: present, executable, scope = current view's actor revision/world/branch, encounter intent at the
current timing revision or the view's exact reading), then requires exactly one observed submission whose offer equals
that snapshot and an applied receipt; no retry or reselection is attempted, so a stale offer still meets the host's
ordinary rejection. Report rows: stage, action, label, target, scope, intent type/text, command id, command/receipt
canonical bytes (hex) and receipt fields. `NativeEncounterOutcomeResultTests` authenticates each row against the saved
`world_command_outcomes` row and the containing journal commit, requires the captured set to equal every player-principal
command after the fixture prefix (host-owned chief entry and merchant answers, and the actionless completion, stay
distinct), and proves altered intent text, command bytes, receipt bytes and command identity each fail. Managed client
tests prove retry identity, stale-rejection recording and no substitution at the seam.

## 4. Honest care text

`CareReason(null)` → "No care is offered to you" (a null view does not establish no agreement). Pending status states the
patient's request, never a helper acceptance: "Care requested · treatment can proceed" / "· the requested care is
unavailable now". The available-helper detail states completion only after every required player has read and no care
is pending. Text tests become exact and gain negatives.

## 5. Proofs and descriptions

Root decode: the human first wolf's reading decodes (two-human root) while the host-controlled first wolf's reading is
refused at an ordinary outcome root — same participant role, different control kind. The care hold asserts the
processor's exact `InvalidOperationException` ("An acknowledged care obligation must resolve before another world
activity.") and the authority's exact `AuthorityDispatchException`. Victory derivation asserts the declared chief HP is
exactly 1 and the derived genesis holds it. Fixture header/manifest/metadata, probe header, checker docstring/limitations
state that the care fixture's managed approach/entry stops at the Active first decision and native input owns only
resolution/refusal/completion; native care-specific approach/entry timing, no-survivor and real two-session readiness
remain unproved.

## Verification order

Fixture-relevant edits (tests outside `Client/`, linked client classes used by fixture support) land before fresh
`outcome-fixtures` and `fixtures`; later iterations touch only Godot-only client files, the checker and result tests.
build → outcome-unit → client-regression → outcome-fixtures → fixtures → outcome-native → outcome-auth → road-native →
road-auth → native → native-auth. Core/retained-auth/six-kill evidence is inherited only by an exact dependency comparison.
