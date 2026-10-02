# Lead feedback

The assignment and prior lead notes define the scope. This file is an explicit mutable observation
channel; it grants no new tools, permissions, deadlines or scope.

## Earlier native care failure: exact finite-stock cause

Lead inspection now diagnoses the specific retained native run `010653Z-83306` (see
`lead-care-investigation/FINDING.json` and `INSPECTION.json`). Its fixture is at revision0. At the
first native advancing commit, Wren submits an applied `world.action.restorative-use` for Traveler,
consuming one unit from lot `9b1a39e2fea3fa0a4deead93338e00f1`. That is the only owned medicine unit
whose item/version matches the aftermath-care agreement's remedy. At defeat revision132 no matching
source dose remains and the world has no crafted lots. Wren has HP1, zero recovery delay and distance256;
the last resolution's timing revision equals current timing. The exact saved root was replayed with
the existing Host on a copy, and originals remain unchanged.

Thus helper readiness is disproved as the explanation for this particular failure: ordinary assistance
spent the same finite dose before combat. Do not change care laws, give stock, suppress ordinary healing,
or expand this unit to a new journey. Where this unit revises fixture descriptions/remaining obligations,
distinguish this diagnosed run from other uninvestigated earlier runs and from still-unproved native
accepted care. Prepared Active is still a narrower native boundary. A future legal user-directed route
may explicitly revoke ordinary assistance before combat while preserving the separate aftermath
agreement; that is a hypothesis requiring actual commands and native proof, not a passed test.

## Submission seam design review

The DESIGN.md direction preserves the intended ownership. Check that observation stays passive even
when a listener fails: once World.SubmitAction has queued a command, an observer exception must not make
FarmGame discard its receipt task and appear to reject an action that is already being committed.
Keep failures visible to the probe through its own recorded failure state. Canonical byte exposure
should not let listeners mutate the captured command/receipt evidence. A retry must report the actual
original command stored with the issued offer, not regenerate an identity from the old offer.

For readiness, compare the actual selected issued offer at the submission boundary. The normal game
can regenerate its presented offer objects each frame; an input injected after a frame boundary must
not silently match a later replacement merely because its label looks identical. A failed identity
comparison is diagnostic evidence, not permission to recapture and retry. Use the current production
selection and its exact semantic/canonical identity, with explicit frame/readiness evidence.

Source confirmation at the first seam edit: `SubmitOffer` currently calls `Submitted?.Invoke(...)`
after `World.SubmitAction`, and `FarmGame.Submit` assigns `_action` only after `SubmitOffer` returns.
A throwing callback therefore leaves `_action` unset even though the real command is queued. Resolve
and regress this concrete path; preserve visible probe failure rather than swallowing assertions into
a false pass. The current `ReadOnlyMemory<byte>` also wraps the mutable private array directly; review
the promised immutable evidence boundary (ordinary array extraction can expose that buffer).

The first compact Hint edit says `V: accept defeat (declines care)`. V opens the wheel; it does not
itself accept or decline anything. Prefer an accurate compact hint such as `V: choices`/`V: wheel`,
with the acceptance consequence on its actual button/sector/inspection. Keep the tooltip's distinction
between navigation and commitment consistent with the corrected E hint.

## Native selection evidence: nested market intent

The new submission/journal byte comparison is useful. One remaining gap in the first implementation:
`Selections.Ready.Intent` and `CapturedSubmission.IntentText` use `Intent.ToString()`.
`AcceptSpotQuoteIntent.Acceptance` is the `SpotAcceptance` class, which has no ToString override; its
quote hash, quantity, payment and fees are not represented by that diagnostic text. The current
Python checker ties selection IDs/order to submissions, but not those independent pre-input facts;
the managed authenticator does not read Selections. This leaves the serialized pre-input selection
weaker than the complete submitted command/receipt proof.

Retain the already captured pre-input offer and, once the actual generated command ID is observed,
record its canonical expected command bytes using that ID (do not recapture a fresh offer). Compare
those bytes and ordered selection IDs with the actual submission and stored journal during managed
authentication. Include a counterexample that alters that expected selection, including a nested
market quote/payment fact without merely changing its diagnostic ToString, while leaving actual
command/receipt bytes intact. This closes the original exact-selection obligation without changing
any authority, input, receipt or retry behavior.

## Follow-up on passive observation cost

The revised observer-failure isolation, defensive canonical byte copies and `V: choices` resolve those
source findings; the focused seam test now exercises the throwing listener and copied bytes.
One small regression remains in the revised call boundary: `Observe(new FarmSubmission(...))`
constructs and canonical-encodes on **every** normal submission before Observe checks for listeners.
Move the no-listener check ahead of construction/encoding (capture the delegate once, if useful).
The disposable probe should pay this diagnostic cost; an ordinary client with no observer should
retain its existing path and not gain a post-queue encoding failure path. This is the original passive
seam scope, not an optimization project or new authority requirement.

## Lead source and rendered review

The revised pre-input selection now retains its captured offer, fills in only the actual command ID,
encodes full canonical bytes and compares them with the submitted command. The managed authenticator
reads ordered selections and verifies those bytes against submissions and journal commands; the
new nested market-payment counterexample leaves actual command/receipt bytes intact. This addresses
the earlier source-level correlation gap; the matching native/authentication run must still pass.

Viewed the actual `outcome-native-20261002T024903Z-fc95d/native-encounter-outcome-o20fog7c/`
`victory.victory-outcome.png` and `victory.victory-completed.png`: ordinary HUD/card text fits the
viewport and both actors remain clear. This is the victory-image review only, not care-route,
physical-device, art-style, human acceptance or full-slice completion.
