# Action-offer integration review

The independent read-only authority review checked the existing WorldActionCommand,
WorldStepProcessor, gather/craft/gift/quest planners and public LocalWorldView. It recommended
preserving actor/world/branch/control identity, actor/target/resource/inventory revisions,
exact recipe/gift/ingredient selections and the explicit Accept versus Complete operation.
The addressed DialogueId is retained because quest intents do not bind it.

The projection does not expose every terrain/custody/body restriction. Preview blockers remain
advisory; the host adjudicates the exact request. Creating or inspecting an offer cannot mutate
that authority. Core, Hosting and durable save formats were not changed by this increment.

| Finding | Integrated resolution and evidence |
|---|---|
| Saved control identities survive local client restart. | A weak per-instance registration refuses an offer issued by another client lifetime, including reopening the same save. Real SQLite reopen regression. |
| Repeated submission generated a fresh command ID. | The issued offer retains its submission task. Reusing it returns the same command receipt and effect; choosing anew requires a fresh presented offer. Session regression checks identical task/result. |
| Unaddressed views cannot supply a quest's title, requirements or rewards. | Generic task discovery replaces authored disclosure when the addressed projection is absent. Dedicated public-view regression. |
| Transformed material is not a valid legacy source gift lot. | Select only source lots. A conserved synthetic craft fixture creates larger transformed wood stock and proves legal wood remains selected; an older captured craft is rejected without substituting currently available stock. |
| Full-width button text clipped the new footer. | Separate verb/key and cost lines, compact movement hint and spacing. Native bounds checks plus five inspected rendered states at 960×540. This does not qualify other resolutions/locales. |
| Old probes acted on host effort before that state was presented. | Wait for the actual displayed executable offer. The retained initial gathering failure had zero command outcomes; no unit was issued. Keep exact one-input/one-yield assertions and do not retry failed commands automatically. |
| A final movement step invalidated the displayed actor revision. | The retained second failure captured revision 74 and reached revision 75 before the berry action. The host recorded RevisionConflict with no berry issued. Scripted journeys now release movement, allow 150 ms to settle, and wait for a matching displayed actor revision; they retain one actual input and never resubmit a failure. |

Seven focused client/host facts cover captured gathering after movement/depletion, gift lot and
inventory revisions, fixed Accept semantics, session/blocked/conversation rejection and duplicate
outcomes, same-save reopen, undisclosed quest information and transformed stock provenance.
The final qualification links in [README](README.md) identify the executed integrated result.
The critic findings were reviewed and corrected locally; there was no second external review
or model run after the owner's explicit-approval override.

Rendered focus uses actual Control.GrabFocus and hover signals; keyboard actions traverse the
native input route. This is not physical gamepad navigation, full modal arbitration, a usability
study or acceptance of the provisional art. Routine stale-world rejection remains visible and
requires a new player choice; the client never repairs an old offer by selecting different goods.
