# Restrained 16-bit+ movement and animation

Research/owner direction: 2026-09-25. The owner reports strange, excessive movement and requests
classic 16-bit+ game animation, with Reddit research before further changes. This document selects a
direction and an inspection/calibration method; it does not claim a corrected native animation.

## Decision

Keep the accepted restrained visual fidelity. Make locomotion deliberate, readable and grounded:
clear alternating steps, stable identity and foot anchor, small coherent body motion, responsive
start/stop, and quiet idle holds. Add detail through controlled pixel poses and meaningful actions.
Avoid continuous whole-body wobble, squash/stretch, gratuitous head turns and independently oscillating
costume layers as default human movement. Unusual creatures and explicit expressive actions can have
different authored motion; this is not a ban on animation or a global frame-count reduction.

Walking, running, idling, acting, being hit and actual forced displacement are separate meanings.
The sprite must communicate the correct one. A slow walk should not look like running on the spot,
and a hit reaction must not imply that the authoritative body moved through a wall.

## Research evidence

Reddit examples are individual critique discussions, not measured production specifications or a
community consensus. The accessible text and artists' written tutorials were examined; unavailable
Reddit video playback is not claimed as an inspected animation. No exact Chrono Trigger/FF6 frame
counts, timing tables or proprietary internal architecture are inferred from these comments.

| Source | Useful observation | Application |
| --- | --- | --- |
| [r/PixelArt: ten-frame walk critique](https://www.reddit.com/r/PixelArt/comments/16rwt0f) | Reviewers identify excessive head motion and long strides despite otherwise appealing art | More frames do not correct the amplitude or character of movement |
| [r/PixelArt: walk-cycle study](https://www.reddit.com/r/PixelArt/comments/tkgvv7/walk_cycle_study_still_learning_feedbacks_are/) | Feedback identifies excess vertical bounce and an incomplete/asymmetric step that reads as a limp | Review each leg's contact/passing sequence and symmetry before adding interpolation |
| [r/PixelArt: natural movement critique](https://www.reddit.com/r/PixelArt/comments/1lgqipb) | Repeated blinks tied to every walk cycle make the character busy | Separate incidental idle/face behavior from gait cadence |
| [r/AnimatedPixelArt: hat/torso critique](https://www.reddit.com/r/AnimatedPixelArt/comments/1u5y9pm/feedback_on_my_walking_animation/) | Independently bouncing headgear can look disconnected from the body | Keep fitted clothing attached; select a small appropriate secondary detail instead of moving every layer |
| [r/gamedev: Stardew/Chrono-style frame counts](https://www.reddit.com/r/gamedev/comments/o7iefe/amount_of_frames_for_basic_animations/) | Discussion emphasizes key poses and admits personal preference/uncertainty about specific games | Use it to motivate comparison, not to prescribe a purported historical frame count |
| [Manning Krull: low-resolution walk cycles](https://manningkrull.com/__pixel-art/walking.php) | Demonstrates two-, four- and eight-frame approaches, readable alternating legs, contact and pose cleanup | Build a coherent cycle at the actual sprite scale; additional frames must contribute useful motion |
| [SLYNYRD: top-down character sprites](https://www.slynyrd.com/blog/2019/10/21/pixelblog-22-top-down-character-sprites) | Demonstrates a four-frame walk and eight-frame run; recommends establishing simple motion before detail | Compare a restrained baseline before multiplying clothing/detail frames; walk and run need different stride/timing |
| [Sabotage: Sea of Stars press kit](https://sabotagestudio.com/presskits/sea-of-stars/) | Frames its modern presentation and traversal as development of classic RPG design | Modern richness and responsiveness remain goals; historical hardware constraints are not requirements |

The comments do not all recommend less motion: some suggest adding missing torso response or foot
lift. The synthesis is **coherent motion at the right amplitude**, not freezing the upper body or
deleting necessary poses. Grounded timing can require a small body response. A limp or a pronounced
gait belongs to an actual character/condition, not an accidental default cycle.

## What this checkout actually does

Inspected [FarmActorView](../Runtime/client/scripts/Presentation/FarmActorView.cs),
[FarmVisualAssets](../Runtime/client/scripts/Presentation/FarmVisualAssets.cs), and the actual
[Squire sheet](../Runtime/client/assets/native-farm/squire.png). The sheet is 48×96 with twelve 16×24
frames arranged as three columns/four direction rows; the current sprite displays at 2× scale.

- The walk sequence is `[0, 1, 2, 1]` at seven pose changes per second. A repeated passing pose is
  not intrinsically wrong and the existing frame count alone does not establish overanimation.
- Position is interpolated over 50 ms and rounded. Walking can remain active for 120 ms after the
  last observed movement; its phase is retained between bursts. These are candidates to inspect for
  stepping after a stop or an awkward restart, not yet proven causes of the owner's observation.
- There is no continuous idle bob, torso scale change or walking rotation in this class. A newly
  observed HP loss produces a 220 ms sinusoidal horizontal sprite shake with amplitude two display
  pixels, plus flash/text. Check this separately from gait rather than calling it walking motion.
- Multilevel elevation changes are already distinguished from horizontal walking. Preserve that
  distinction along with authoritative positions, input responsiveness and the shared-desktop policy.

The exact reported window/build/clip has not been matched to a retained observation. Other tasks can
run other Godot games on this desktop. This source inspection does not establish that this class
caused the specific visual complaint. The next motion comparison must identify its build and actor.

## Native calibration contract

Start with a four-pose walk baseline; compare a six- or eight-pose alternative only if it improves
contact/readability at the selected native scale. Those numbers are candidate tests, not a permanent
asset restriction or a claim about Chrono Trigger. Preserve four-direction coverage and consistent
anchors; directional expansion follows actual approved movement needs and art cost.

| Aspect | Proposed calibration rule |
| --- | --- |
| Root/contact | A consistent grounded foot origin; no cosmetic translation mistaken for physical travel |
| Body | No runtime squash, stretch or continuous rotation for ordinary walking; compare a still head with a small authored pixel response |
| Gait | Complete left/right contact and passing poses, coordinated arm opposition, coherent silhouette; feet do not visibly skate |
| Timing | Match cadence to observed travel and the authored stride, independent of render rate; bounded smoothing must not predict uncommitted travel |
| Stop/restart | Settle promptly to an authored standing pose; no long recovery flourish or walk cycle after a blocked move |
| Idle | Quiet holds, occasional contextual glance/blink/gesture; no synchronized marching or identical periodic quirks across a crowd |
| Clothing/equipment | Attachments retain grip and orientation; use only secondary motion justified by that material and movement |
| Pixel stability | Review on the actual pixel grid at native/integer scales, including camera motion; avoid subpixel jitter or decorative filtering |
| Hit feedback | A brief readable authored response, separately calibrated from locomotion; presentation cannot move the authoritative body |
| Purposeful actions | Treatment, work and dialogue use clear contact/gesture beats and restraint; no decorative motion needed to fill every moment |

The first comparable capture should show the same actor/art/route/zoom in four directions, starts and
stops, a blocked move, short direction changes, a quiet idle, a hit, and one interaction. Record the
observed position trace separately from rendered feet and pose phase. Test alternate render rates
and a delayed observation without changing authority pacing. Compare normal speed first, then
frame-step for contact/attachment defects; a slowed video alone is not the acceptance view.

Use the existing restrained style approval. Do not ask for approval of that same style again. The
new movement specimen still needs the owner's actual response before expanding the animation kit.
Keep the earlier assets and raw observations, including unsatisfactory results. Source inspection,
unit checks and Reddit advice do not substitute for that moving specimen or player judgment.

## Integration with the world model

The [composition contract](../Runtime/docs/architecture/world-composition.md) provides semantic
inputs—locomotion mode, applied injury, action/contact stage, attention and observed emotion.
Presentation selects an authored performance; it does not invent fatigue, cure an injury on a frame
callback or advance paused processes. State depth can grow while ordinary walking remains simple.
This preserves expressive NPCs, animals and props without making the scene perpetually restless.
