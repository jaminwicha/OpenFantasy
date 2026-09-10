# Native farm captured-frame review

Review date: 2026-09-10 UTC. Scope: visual inspection with `view_image` of the coordinator's two 960×540 native screenshots, reading the associated walkthrough/reopen reports, checking the retained paperdoll algorithm against its source witness, and rerunning `verify_visuals.py`. This reviewer authored the canvas and admission; this is an explicit self-review of rendered output, not independent art acceptance. No engine process, build or manual interactive playtest was run by this reviewer.

## Initial capture findings

1. **Nameplate/header overlap, concrete layout defect.** Wren's name reaches the clock in both `walkthrough-01` images. The coordinator is correcting the camera's vertical offset from +10 to −26 display pixels. This initial evidence does not establish that the correction is visually successful; it needs the updated render.
2. **Source actors and dialogue are legible.** Wren's red clothing/dark skin/blue hair and the player's lighter hair/blue clothing are distinguishable. The own-actor ring and name color help identify the player. Sprite pixels are crisp without evident atlas bleeding in these stills. The exact admitted line, “Just me, the rows, and the crows. Suits me.”, is readable with a clear continue control.
3. **The farm is visibly incomplete.** Exposed plank patches surrounded by grass read as a ground layout rather than a complete occupied farm. This follows the declared removal of 165 original blocking props from a terrain admission that has no corresponding structures. It is not evidence of a rendering omission or an implemented wall/collider mismatch. Restore structures through their real terrain/structure admission before restoring their visual obstacles. The visible repeated grass dominates the small scene; future accepted art should improve material variation and focal hierarchy without increasing clutter.
4. **UI density is a polish concern.** The dialogue panel and persistent status/action panel together occupy much of the lower view. Text remains readable, but the short exchange has more screen furniture than the final uncluttered exploration target needs. Later UI work can consolidate contextual instructions/status while keeping interaction choices discoverable. This is a prototype polish follow-up, not a canvas correctness blocker.
5. **The enabled give action during dialogue is intentional.** The coordinator's walkthrough explicitly exercised giving six wood while the reading hold remained active. The visible enabled control therefore agrees with that behavior; a reading hold does not imply that all addressed conversation actions are disabled. The HUD's wood count is the player's own public projection. This review draws no conclusion about Wren's private inventory from the UI.

The source compositor check confirms body → outfit A → outfit B → hair → accessory order, the exact Wren palette indices, no race layer for race zero, alpha threshold, and original first-nearest grayscale selection. The read-only verifier passed eight byte-identical retained source copies, seven PNG/resource references, and the complete 1,024-cell crop with its declared substitutions. These checks do not independently establish pixel-perfect output across every rendered frame.

## Execution evidence and limits

The coordinator's JSON reports identify native macOS rendering on .NET 10.0.12 and report successful movement input, a reading hold of at least 500 milliseconds, six-wood handover through the native action, conversation close, and exact paused public-view restoration after process restart. The final captured HUD shows two wood. These are coordinator execution claims supported by the retained reports, not actions performed by this reviewer.

Static images cannot establish walk cadence, all four facing transitions, interpolation quality, stop behavior, input latency, controller usability, or sustained play feel. They also do not establish sound, expressive choreography, world/weather animation, final NPC art detail, or user acceptance. The simple original sheets are retained source content for this native loop; they do not meet or replace the separately calibrated final art target.

## Exact initial evidence

Capture files were read from `/private/tmp/openfantasy-p00/native-loop/native/`. The coordinator retains the native artifacts; the hashes below identify exactly what was inspected even if that temporary location is later cleaned.

| File | Bytes | SHA-256 |
| --- | ---: | --- |
| `walkthrough-01.dialogue.png` | 66184 | `83c089c789ea33651bf2465e6dab6722e9b41a605e17cbe34bd590f4b391636e` |
| `walkthrough-01.final.png` | 53015 | `689f225cbc0ad2da54db7bc4c320c9075968cb22241b0417ac4adc3f4fe0eae5` |
| `walkthrough-01.json` | 1809 | `717c2a3a303ba6ac38e93193a5c9ed2c5c254651a58734a3fd0f04caae7cbd68` |
| `walkthrough-01.reopen.json` | 1497 | `076b7dc3cd0ab9ae13cb07b435f482fa76d0f7505b040f98ef966213d39814e6` |

Source paths below are relative to `Beta/Runtime/`.

| Reviewed input | SHA-256 |
| --- | --- |
| `client/scripts/Presentation/FarmPaperdollComposer.cs` | `8490a8e586d3f1b06400c05189e8287be45ccd0610e5556e96aa8509197ce1d3` |
| `content/legacy/aldermere-first-loop/source-support/src/Client/SpriteComposer.cs.txt` | `bd36d414e8c68a64d79b336dd9b3f0f87617da47e707e447bdabde37a10eebce` |
| `client/assets/native-farm/PROVENANCE.json` | `cf4ddb5f6fb282dee286e3fd49aef10c2445c61454d4c575745fafb4875013ac` |

The detailed current canvas/source-asset hash set remains in `VISUAL_EVIDENCE.json`; the repeated read-only verification completed successfully at `2026-09-10T01:06:19.324139+00:00`. No source, runtime or asset mutation was needed for this review.

## Updated capture: camera issue closed

The coordinator recaptured the native walkthrough at `2026-09-10T01:07:45.603126+00:00` through `2026-09-10T01:07:53.510899+00:00`. This reviewer inspected the new dialogue and final PNGs with `view_image` and independently recomputed the four screenshot/individual-report hashes declared by `rendered-final.json`.

**Finding 1 is closed for the captured 960×540 layout.** Wren's name now sits clearly below the header in both images, with visible separation from the clock. The header has a solid backing, and the clock, own wood and pause/resume control remain legible. Wren's sprite and name remain visible throughout the captured conversation. The dialogue panel covers the lower portion of the player's sprite and ring; this is the coordinator's intentional conversation framing tradeoff, not a new clipping or physics defect. The player is fully visible again in the final paused frame. No further concrete canvas defect is visible in these two captures.

The updated coordinator report records both native create and reopen processes exiting successfully with empty stderr, and repeats the movement/reading-hold/handover/close/restoration checks. Findings 3 and 4 remain presentation follow-ups. Motion quality and all art-acceptance limits above remain unchanged; still-frame camera closure does not generalize to every resolution, position or dialogue arrangement.

The updated screenshot and individual-report files are under `/private/tmp/openfantasy-p00/native-loop/native/native-farm-edb15e6cbb4c495d8aedca9aecd0c06b/`. The aggregate report is one directory above. Original capture evidence is retained above.

| Updated file | Bytes | SHA-256 |
| --- | ---: | --- |
| `walkthrough.dialogue.png` | 62371 | `8c8b65b1417a31e652c789279842e82a393468189486ad1d33f3af3914732c9c` |
| `walkthrough.final.png` | 48902 | `686aa219d592d1b3bf6fab66530603f26c84cd483f9d7e102eb877cb3b0c4918` |
| `walkthrough.json` | 1809 | `50aea8af1565e7a1cc7e19f5c28cec6063eed6fc003b95f4759e95ea534f3be7` |
| `walkthrough.reopen.json` | 1497 | `a899e9cb0cc3de0d41930ca290716d364a062a8372aabd30fdc937710accb911` |
| `rendered-final.json` | 5827 | `46537dbcf1224a2bd22b381b8e0f02356219cccd0552dc56ba2dfe83a7002b2e` |
