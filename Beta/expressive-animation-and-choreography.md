# Living characters, interactive objects and scene choreography

8 September 2026. Owner requirement: graphics and animation must accommodate personality quirks, idle movements, specific actions, emotions, evocative gestures and interactions across people, animals, creatures and objects. Puppetry is a candidate technique for choreography during events, narration, conversations and encounters. This is an implementation contract to develop, not a claim that the reference illustrations demonstrate it.

## Performance direction

Preserve the approved pixel-art style, readable silhouettes and restrained visual density. Variety comes from meaningful performance and asset families, not perpetual fidgeting. A proud guard, an anxious apprentice, a tired farmer and an inquisitive animal should differ in posture, attention, timing and reactions as well as clothing. Recurring named people retain recognizable mannerisms across locations. Avoid using the same traveler, idle cycle, gesture, plant silhouette or prop arrangement as the default in every scene.

Separate lasting personality from current emotion, physical condition and current purpose. A character can be reserved yet delighted, proud yet injured, or impatient while trying to be polite. Those states can influence posture, gaze, gesture frequency, interpersonal distance and voice/text delivery. Emotional presentation should follow authored characterization and relevant world state; it cannot invent a grievance, promise, secret or relationship change.

Stillness is part of acting. Alternate resting poses, small gaze changes and selected gestures with quiet holds. A meaningful hesitation or a shared glance can carry more than constant bouncing and emote icons. Essential intentions remain readable when decorative motion or effects are reduced.

## A shared interaction vocabulary

| Family | Representative performances to support | Connection to play |
|---|---|---|
| People | Look/turn, approach, greet, listen, think, point, refuse, offer, take, sit, stand, work, inspect, celebrate, recoil, recover | Attention and gestures frame actual dialogue, movement, choices, work and consequences |
| Animals and creatures | Rest, forage, sniff, orient, approach, avoid, groom, warn, startle, interact with a handler or habitat | Perception, temperament, needs and current intent inform behavior; wildlife remains part of the simulation |
| Objects and mechanisms | Open/close, latch, lever, crank, carry, place, bend under load, operate, break, repair | The same object identity and state support gameplay, art and history; scenery is not a parallel pretend interaction |
| Paired/group actions | Hand over an item, help lift, converse, share attention, cooperate at a workstation, execute a combo | Participants, space, contact points and resources have explicit ownership and interruption rules |
| Environmental response | Cloth/foliage motion, rain sheltering, tracks, wetness, seasonal changes, functional wear | Cosmetic response is distinguished from the consequential environment and condition contract |

This is an initial vocabulary to specify, not a complete release animation inventory or permission to omit required interactions. The GDD must map interactions to player-facing verbs and outcomes. The art contract then maps those verbs to supported body/prop families, facing directions, poses, attachments, timings and fallback performances.

## Recommended implementation shape

Use three cooperating responsibilities inside the selected Godot/C# architecture:

1. **The portable simulation owns intent and consequences.** It resolves movement legality, object custody, work, relationships, emotions when mechanically relevant, combat, damage and repair. It emits scoped semantic events and state.
2. **An activity/performance controller selects a presentation.** Personality, current mood, activity and relevant physical state choose a supported performance from the appropriate family. Attention/gaze, locomotion, hands/tools and expressions need priority rules so incompatible clips do not fight. Cosmetic variation uses a stable object/person key and its own visual random domain; it cannot advance authoritative RNG.
3. **A scene director stages authored choreography.** It coordinates actors, marks, facing, gaze, pauses, gestures, props, dialogue cues, camera and audio. Its requests for consequential actions use the same validated commands as ordinary play. It is not a second gameplay engine.

“Puppetry” need not mean a universal skeletal rig or stretched bitmap limbs. Evaluate authored pixel keyframes, attachment points, held poses and controlled transforms; use a rig where it preserves the chosen pixel treatment. Motion at the native grid and gameplay camera decides whether a technique is suitable. Keep editable art sources and a stable export contract so production does not depend on regenerating an illustration for every gesture.

A proposed performance definition carries: identity/version, compatible body/prop family, supported facing, pose/clip references, attachment/contact points, authored duration/markers, transition and interruption rules, optional expression/gaze layers and accessibility fallback. Exact frame counts, resolutions and animation tools remain to be validated.

## Choreography, authority and interruption

An event sequence needs stable bindings for its actual participants, props, place and branch. Reserve actors or workstation/contact slots only when the action requires it; release those reservations on completion or interruption. Ambient idles must yield to important actions. Navigation to a stage mark still obeys the navigable world; a cutscene may not teleport a person or ignore a locked door without an explicit gameplay action.

An item handoff is an atomic custody transaction with a presentation cue. Disconnecting, skipping or replaying the conversation must not duplicate the item or reward. A late joiner sees the committed current situation and may receive a short restaging; it must not re-run consequences merely to catch up visually.

Define authored policies for walking away, danger, death, absent actors, interrupted work, skipped dialogue, reconnection and restarting a save mid-sequence. Persist the semantic scene phase and outstanding commitments needed to resume correctly. Exact cosmetic frame replay is unnecessary unless a feature depends on it. A narration line can reference known facts and cue a performance, but generated prose cannot directly take control of the world.

For Wait-style combat, harmless breathing, gaze and presentation may continue during a decision hold while readiness, damage, resource use and other paused authoritative effects do not. Decide the encounter/world-clock relationship explicitly. A long gesture must not accidentally extend a status effect, spend extra work time or provide free invulnerability.

## First retained proof

Use Wren's existing sapling/windbreak quest as one connected performance:

- Wren notices the player, turns from work, conveys concern about the saplings, points to the damaged shelter and listens to the response.
- The player offers the original six wood through the real quest/material transaction. Hands, tool and board positions support the visible exchange without defining a second inventory.
- A helper joins a repair activity; Wren's manner changes after the completed work. One animal responds to a nearby action, and the repaired windbreak remains a persistent interactive object.
- Return later to a distinct remembered greeting. Interrupt, skip and reload the event; run the same interaction with two clients and confirm a single consequence.

Compare at least two personality performances using a shared supported body family, plus a creature and an interactive prop. Review the native-size moving result, camera scale, contact alignment, quiet holds, emotional legibility, repetition across placements, interruption and reduced-motion presentation. These samples must become usable assets/controllers/tests in the actual project. No bulk production precedes a demonstrated pipeline.

This proof develops the existing [narrative design](narrative-simulation-design.md), [environment and maintenance contract](environment-aging-and-maintenance.md) and [readiness register](implementation-readiness-audit-2026-09-08.md). The final GDD must budget the resulting body/gear/action/interaction coverage and authoring tools, not count a still image as an animation set.
