# Integration review notes (request still running)

These are observations for Astra's terminal integration review, not worker acceptance or a changed assignment.

- The first durability report (`durability-20261001T150505Z-8aed2`) contains six actual SIGKILL returns,
  before/after queue, an intermediate sample and disengagement, with real authority barrier messages.
  Its managed authenticator passes. Later source edits mean it is not final-source evidence yet.
- The design currently describes a 6,000-step ordinary checkpoint during an active retreat. Verify
  the actual material/care archive cut separately on return: checkpoint persistence does not exercise
  `WithCoordinatedCut` and live history segment replacement. The original required archive concern
  must not disappear behind a narrower checkpoint claim.
- Later contrast failures reached real root invariants while constructing counterfactual input:
  timing revision equality and reservoir/inventory ownership. Repair the fixture consistently;
  never weaken the production invariants to make those fixtures build.
- Inspect the final source and test outcomes for every critic finding and additional proof. Old
  development passes do not close a gate after source changes; final matrix pins still apply.

## Confirmed rendered contradiction in the first passing native report

`qualification/native-20261001T154033Z-40db5/native-retreat-yl29xbwt/keyboard.retreat-inspected.png`
shows all three encounter choices marked changed, disabled Confirm and the text
"This choice changed or is no longer visible. Close the wheel and choose again."
The view also clips the top/bottom because the expanded explanation makes the panel too tall.
The same report claims first inspection was current and unchanged. Source `InspectRetreat` checks
CanExecute before `Capture` awaits FramePostDraw, then writes an unconditional success description.
Therefore the check misses the actual rendered stale state. Only a Core test file had changed after
this report at the time of inspection, so the pictured UI was still the delivered application state.

Treat this as an unresolved native acceptance defect, not successful first-open qualification.
Strengthen the assertion across the rendered/readable interval and immediately before commit;
prove a usable single first inspection without hidden close/reopen retries or disabling legitimate
captured-choice invalidation. Keep authoritative revision checks. Inspect whether NPC decision
publication after opening invalidates the shared timing revision; the source change that fixes
previous-frame capture alone does not prove a stable decision window. The already explicit cancel
then second opening can conceal a first-opening failure. Test the common input/state race honestly.
Ensure the full wheel is visible at the qualified viewport, with readable text and no clipping.

Source follow-up: `FarmInputRouter.WorldShouldRun` deliberately does not pause for inspection panels;
`CaptureWheelView` stops movement and reads, but does not quiesce already eligible NPC decisions.
Thus do not fix the race by silently making all menus pause the world, weakening revision checks,
or automatically refreshing the captured command. Investigate exposing the human decision offer
only after the current boundary's automatic NPC choices have settled, or another coherent existing
Host scheduling seam. Preserve Full Wait and explicit stale-choice handling when material conditions
actually change. This is a lead investigation candidate, not a directive or a proven root-cause fix.
The new intent already implements value equality over its route; reference equality alone is not
the remaining explanation. Require a valid rendered interval and pre-confirm state, not merely the
pre-FramePostDraw model flag. Preserve the first failing screenshot even if a later run looks fine.

## Fresh legacy authentication failure

`legacy-auth-20261001T160253Z-842b1` fails protected `NativeTradingResultTests.cs:120`:
expected showing expiry 2723, actual2722. The assertion requires
`showBefore.AdvancingSteps + 1 + NativeTradingPolicy.ShowingWindowSteps == showing.ExpiresAtStep`.
A later legacy-auth run also fails. Retained pre-retreat native recordings authenticate successfully.
Inspect whether this exposes a production scheduling bug or an incorrect full-window-at-publication
assumption for an absolute expiry captured earlier by the UI. Do not merely repeat native runs until
one happens to satisfy the equality, and do not weaken/rewrite a protected assertion to bless drift.
Resolve against the actual captured-choice/expiry contract and prove the correction; any necessary
checker correction requires preserved old evidence, explicit justification and equally strong checks.
No such adjudication or test change has been made while the worker is live.
