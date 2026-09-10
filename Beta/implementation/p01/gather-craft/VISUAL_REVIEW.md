# Bounded native gathering visual review

This review inspects actual rendered 960 × 540 macOS captures, with a separate static check of the retained original node atlas adapter. It is a legibility and source-presentation check, not acceptance of the game's eventual art quality, an animation timing playtest or a claim of autonomous NPC gathering. Inventory feedback refers to the controlled player's public projection; this review does not require private NPC inventory.

## Initial captures: defects found and corrected for recapture

The first create stage rendered and completed its gathering checks. Its subsequent reopen probe timed out, so the combined `native-rendered-01.json` result is **failed**, not a full native pass. The coordinator is investigating and repeating that run separately; the still images below remain valid evidence of the initial visual layout.

| Initial evidence | SHA-256 |
| --- | --- |
| [Initial oak capture](visual-evidence/initial-gathering.oak-stump.png) | `af67740e7b41f716fcdf7a896741ee1f5acb0cc166b551724b655f4f5477f1e8` |
| [Initial gathered capture](visual-evidence/initial-gathering.gathered.png) | `c93cf277c5c7f1cbef858d803a028324b3a6d7b4284cf56ae922856b905b10b6` |
| [Initial combined native report](visual-evidence/initial-native-report.json) | `145232783ddf97a926cbfedd76e3e3193c240b5e217c6c3ece7913421d19541d` |
| First capture directory, `gathering.json` | `34aaf6680aefceced18da0ea9a7df9d792605fa155688fceac67b7cc2dee8519` |

Both stills visibly showed a resource nameplate crossing Traveler's face or chest when standing north of the resource. `FarmGatherNodeView` now omits those redundant labels; the context action already names the nearby resource. The original sprite, frames, position and depletion tint remain unchanged. The updated view hash is bound in [ADMISSION_EVIDENCE.json](ADMISSION_EVIDENCE.json).

The oak scene exposed a gray band along the left admitted-region boundary. It also placed Wren's sprite behind clock text near the top edge. The coordinator added camera limits and an opaque HUD backing. The earlier native farm review's successful dialogue layout was bounded to that capture; this new camera position revealed a more general need to separate world drawing from readable HUD text.

Despite its filename, the initial `oak-stump` image showed a live oak and Wood 12, before the fifth harvest appeared. It does **not** visually prove depletion. The coordinator moved screenshot capture to follow a pause and a rendering delay. The gathered image showed Wood 13, Berries 4 and a dim depleted berry presentation; its overlapping label made the resource harder to read.

## Source fidelity and quality limits

The tree and berry textures are byte-identical copies of the frozen original 48 × 96 atlases. The adapter uses their 16 × 24 frames, three columns/four rows, nearest filtering and twofold display scale. The source idle sequence uses columns 0, 1, 2, 1. The original depleted renderer dims the same sprite; there is no original separate stump sheet to import. The temporary dim tree/bush is therefore an explicit source-preserving presentation, not a claim of a newly authored stump asset.

These sparse original sprites and the flat admitted grass are suitable for verifying this retained loop. They do not establish the requested Sea of Stars detail, calibrated magical atmosphere, comprehensive animation, gathering gestures, tool contact effects or sound feedback. Those need artist-owned assets and later motion/audio evaluation. Static screenshots alone cannot verify idle cadence or responsiveness. No collider, work timer or inventory outcome is inferred from the sprite's dimensions or presentation clock.

## Corrected rendered run: closed within the captured layout

After the coordinator signaled completion, the reviewer inspected all three corrected captures from native run 04. The [combined corrected report](visual-evidence/corrected-native-report.json) passed both create and reopen stages under Godot 4.6/.NET 10.0.12 on macOS, with exit code zero and empty stderr for both processes. The report records one test-only focus request during create and none during reopen. The review did not start another game process or inspect images while the active probe was running.

| Corrected evidence | Visible result | SHA-256 |
| --- | --- | --- |
| [Oak depletion](visual-evidence/corrected-gathering.oak-stump.png) | Paused, Wood 13/Berries 0, dim original oak below Traveler | `cc7c811095064c74577e1465480fe341492bf760c3558fabc3b7c6be9a1ae1ce` |
| [Gathered supplies](visual-evidence/corrected-gathering.gathered.png) | Paused, Wood 13/Berries 4, unobstructed player and nameplate | `53df11db4a4eebe867d1c919f5c5115e9ba31c6bafbe9633779fa8a547f83e84` |
| [Regrowth after reopen](visual-evidence/corrected-gathering.regrown.png) | Paused, 09:33, same Wood 13/Berries 4, visible restored berry bush | `9e553f4a55131a12d7fa89ad2f97cf3ada5c70d9cb48cdf0f6793d4e0fd723e3` |
| [Combined report](visual-evidence/corrected-native-report.json) | Both stages passed; public restored state exactly equals the saved final state | `b2dafb639d7055fa694ab0cdb07ec1fb85bed6fa2f38e928d613da3266159c51` |

The gray left band is gone in the boundary-constrained oak view. The top HUD remains legible behind an opaque backing even when Wren's world sprite approaches that boundary. Resource nameplates no longer cross Traveler's face or body. The corrected oak image is now captured after the fifth wood appears and shows the expected dim depleted sprite. These four findings are **closed for the inspected 960 × 540 layout**; no further runtime change was requested by this review.

The regrown still visibly establishes berry restoration. Oak is offscreen at that camera position, so its restoration is established by the coordinator's native report rather than visual inspection. The report's final public node state has oak revision 6/five swings and berry revision 5/four swings, while player custody quantities are unchanged by regrowth. This reviewer independently checked the exact create-final/reopen-initial public serialization match and every SHA-256 in the native report's six-artifact manifest. [visual-evidence/VALIDATION.json](visual-evidence/VALIDATION.json) binds the original capture paths, retained byte-identical files and nine presentation inputs.

One minor UI follow-up remains: in the paused depleted-oak view, the disabled context button defaults to **Pick berries** even though no eligible berry is the current nearby action. It cannot initiate an incorrect action while disabled, but a neutral label or hidden action would be clearer when no gather target exists. The source's dim depleted sprite also has low contrast against this repetitive grass; it is retained source presentation, not the intended final stump/harvest readability treatment. Both are declared polish work, alongside the art, gesture, effects and sound limits above. Neither changes the verified source/custody/time behavior or constitutes final user art acceptance.
