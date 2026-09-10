# Native ration crafting: bounded visual review

The three actual macOS rendered screenshots from `native-rendered-01.json` show no blocking HUD or clipping defect at **960 × 540**. The coordinator executed the native three-process journey; this reviewer inspected its images and public report after the game had stopped. No independent native process was launched by this reviewer.

## Observed result

| Capture | Visible own-inventory counts | Visual assessment |
| --- | --- | --- |
| [Prepare](visual-evidence/rendered-01/production.prepare.png) | Wood 8, berries 2 | Clock, counts, pause/resume, movement instructions and context controls are readable. Traveler and the live berry bush are distinct. |
| [Upgrade and first craft](visual-evidence/rendered-01/production.upgrade.png) | Wood 7, berries 0, rations 2, offcuts 1 | The header accommodates two inventory lines. The new `Pack rations [C]` control fits alongside existing actions without clipping or overlapping movement instructions. |
| [Reopen and second craft](visual-evidence/rendered-01/production.reopen.png) | Wood 6, berries 0, rations 4, offcuts 2 | The updated totals remain readable and the berry node is visibly depleted. The header is opaque, the bounded field fills the viewport, and no nameplate overlaps the header in these shots. |

All three captures are paused, and action controls are visibly disabled consistently with that state. The images demonstrate displayed results, not the timing of crafting feedback or animation. Wren is outside these camera views; neither his private inventory nor offscreen appearance was inspected or inferred from this UI.

## Remaining observations

| Priority | Observation | Disposition and owner |
| --- | --- | --- |
| P3 | The depleted berry stump has weak contrast against the grass. | Retain the original provisional sprite for this increment. The later artist/presentation pass should make depletion recognizable without relying on a text label; verify at the eventual target scale. High confidence in the current contrast observation. |
| P3 | The field remains a visibly repetitive grass plane with little authored farm structure in view. | This is the explicitly bounded terrain adaptation, not accepted final art or completed source geography. Content/artist owners should qualify a richer representative scene separately. High confidence in the repetition observation. |
| Follow-up evidence | The generic paused message replaces any short-lived action feedback. | A later active-play capture or brief footage should assess success/rejection feedback, tool gestures and crafting response. The present screenshots cannot establish that those animations or cues exist. Owner: client interaction/playtest pass. |

There is no new art generation or accepted high-detail style in this result. The source paperdoll and plant assets remain the provisional retained baseline. Static screenshots do not qualify animation cadence, directional transitions, gamepad/mobile layouts, other resolutions, audio, eating or a complete Wren quest.

## Retained evidence

[visual-evidence/rendered-01/VALIDATION.json](visual-evidence/rendered-01/VALIDATION.json) contains hashes for the three retained PNGs, three phase reports and aggregate report. The reviewer independently verified all eight artifact hashes listed by the aggregate native report, including both database files, without altering either database. Read-only JSON checks also confirmed that migration preserved the entire reported `Retained` public projection while introducing crafting revision zero, and that the third process's initial public state exactly matched the prior process's final state.

Those report comparisons are narrower than durable journal and identity replay qualification, which belongs to the coordinator's runtime and persistence evidence. The coordinator noted a probe-handshake wording change after this rendered build; a later final run will bind the final executable sources. These original captures remain retained rather than overwritten.

## Final integrated capture

After the shared SQLite publication guard and native input-observation refinement, the coordinator repeated all three rendered stages using the final application code. The [final report](qualification/native-rendered-final.json) and [final reopened frame](qualification/native-production-8a6d34fb1d6a478fb5ecd23b20cfdacd/production.reopen.png) are retained. Counts remain six wood, zero berries, four rations and two offcuts with two tools. The coordinator visually checked the final frame at960×540; controls and both inventory lines remain legible without clipping. The final reopen needed one actual window-focus request; normal production focus-pausing remained active. The independent earlier visual review above remains scoped to its retained captures; no new art/animation acceptance is asserted.
