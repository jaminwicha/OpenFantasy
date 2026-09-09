# Adopted encounter direction and seamless combat transitions

8 September 2026. This decision follows the owner's instruction to go with the recommended approach and make combat transitions in the mould of Chrono Trigger and Sea of Stars. It supersedes the adoption status in the [v0.4 proposal](combat-time-and-world-streaming-proposal-v0_4.md); that proposal and its research remain preserved. This is a design decision, not completed runtime or animation evidence.

## Adopted direction

- Use bounded on-field encounters with full Wait decision holds, deterministic shared encounter authority, cooperative planning and controlled admission of outside influences. Adopt the seven encounter principles in v0.4, including safe exit/re-entry and no default decision countdown.
- Retain shared world pause for solo play and coordinated sessions. Independent cooperative exploration uses the disclosed distinction between tactical progress and outside world time. This accepts a game-time abstraction; it does not establish one physically uniform clock.
- Use seamless grid streaming with predictive loading, persistent identities and mutations, and separate detailed/coarse simulation. Chunks remain loading units independent of encounter membership, narrative geography and vehicles.
- Exploration, confrontation, battle and aftermath take place in the same visible location, with the continuity and staged character performance requested from Chrono Trigger and Sea of Stars.

The exact calendar/deadline policy, environmental coupling limits, encounter merges, disconnected-player handling and platform/performance budgets still require the implementation contracts listed below. Their remaining work does not reopen the adopted direction.

## Reference lessons and evidence limits

Sabotage's creative director explicitly describes seamless navigation-to-combat transitions, encounters designed in the field, and decision-making without a time bar in Sea of Stars. This supports our continuity and relaxed pacing goals. It does not require Beta to adopt that game's timed hits, damage locks, exact initiative model or exclusively handmade encounters. [Developer combat explanation](https://blog.playstation.com/2022/07/07/a-closer-look-at-the-turn-based-combat-in-sea-of-stars/).

Chrono Trigger's original developers describe giving characters personality through motion and developing cooperative techniques from the appeal of synchronized character animation. The source is a translated compilation of 1995 developer interviews, not original engine source. Apply that lesson to our approach poses, gestures and cooperative actions. [Developer interviews, graphics and gameplay sections](https://shmuplations.com/chronotrigger2/).

The earlier [reference architecture study](reference-architecture-research-2026-09-08.md#2-chrono-trigger-and-chrono-cross-staged-continuity-and-readable-differences) separately records encounter staging from Chrono Trigger modding experiments, labelled as reverse-engineering evidence. Our state machine below is a new Beta design, not a claim about either reference game's internal implementation. Use original characters, assets and choreography within the accepted art direction.

## Player-facing transition contract

| Phase | What the player experiences | Required state behavior |
|---|---|---|
| Exploration | Enemies inhabit the world; an ambush can emerge from a believable hiding place or event. | Authored and seeded encounters use persistent entities and explicit triggers. An invisible random roll must not send the party to a detached battle scene. |
| Confrontation | A look, warning, startle, weapon draw or creature response establishes the encounter. | Authority accepts the trigger once and takes ownership of participants and relevant hazards before consequential attacks resolve. Surprise has a defined action/initiative cost. |
| Staging | Characters make short, readable movements into viable positions. The camera gently reframes the existing terrain, combat controls appear, and audio changes with the situation. | Entry positions and routes are legal and deterministic. Choreography cannot bypass walls, grant an extra strike, move across a hazard for free or reset ongoing effects. |
| Wait combat | The same people fight among the same trees, furniture, ruins or ship fittings. Personality remains visible through restrained stance, gesture and reaction. | Required decisions hold tactical readiness, costs, statuses and coupled hazards. Harmless animation, music and interface feedback may continue. Outside influence must join encounter rules before taking effect. |
| Outcome | Defeat, surrender, flight or victory receives a brief appropriate response; rewards and consequences become readable. | Commit the outcome once. Preserve injuries, spent resources, deaths, damage, fleeing actors, memories, relationships and quest changes as applicable. Presentation does not award or erase them. |
| Exploration resumes | Combat controls withdraw, the camera returns smoothly, and surviving characters continue from their valid current positions. | Release encounter ownership at a legal step. Resume from the resulting world state, never a pristine copy of the pre-battle scene. Flight and pursuit keep their costs and history. |

Ordinary battle entry/exit uses no screen wipe, blackout, loading screen or replacement arena. Dramatic boss or story staging can be longer where justified; routine encounters keep staging brief. Camera motion must preserve pixel readability, with reduced-motion handling. Establish exact durations and easing through the first native animation acceptance, not an untested promise in the GDD.

Combat controls begin at the authoritative ready phase. The interface makes that phase apparent and cannot accidentally reuse a held exploration input as an attack. Clients may finish cosmetic motion at different times; animation length, frame rate and packet arrival must not change initiative or the result.

Personality adds distinction without clutter. A cautious companion may brace, an eager one lean forward, an injured one guard a wound, and an animal orient toward a threat. Select those performances from actual character state. An idle animation cannot secretly change location, relations or combat stats. Meaningful reactions and position changes remain core actions.

## Geometry, streaming and vehicles

Use authored staging anchors in important places and deterministic spatial constraints in generated terrain. Account for actor footprints, elevation, walkable routes, blockers, reachable exits and a readable camera frame. Revalidate against current construction, destruction and weather-related changes; a generation-time clearance check alone is insufficient.

Staging adjusts to the location. Narrow bridges and furnished interiors do not need the same formation as an open clearing. If a preferred formation does not fit, retain legal positions and choose a compact framing. Never relocate the party through a locked door or fabricate a clearing. If the geometry cannot support a legal hostile action, retain the obstruction and resolve approach/pursuit under the declared encounter rules instead of manufacturing a remote arena.

Pin required world data and presentation resources while the encounter needs them. A battle may cross several chunks. Loading completion cannot decide who participates, reroll an enemy or duplicate an object. Unrelated regions can stream independently. Unexpected missing data follows the explicit synchronization fallback from v0.4, rather than being disguised as an invented encounter.

A vessel encounter retains that actual vessel, crew, passengers, cargo, damage and local deck coordinates. The vessel and its motion-related dependencies must share a declared timing policy before ship combat is enabled. Boarding or landing may lead to another connected surface through an explicit action; a visual transition alone cannot move a fight there. Sandship travel remains a candidate requiring its own surface/propulsion rules.

## Implementation boundary and retained verification

The portable C# core owns encounter identity, membership, phase, positions, command legality, reservations and committed outcomes. The Godot presentation adapter plays camera, UI, audio and performance cues. A reusable scene director requests semantic actions through the core and consumes their results; it has no second copy of gameplay authority. This remains one decoupled implementation without introducing a native language or IPC layer.

Persist enough semantic phase and command receipts to resume safely after interruption. A reconnecting client reconstructs the current encounter; it need not replay a cosmetic introduction. Skipping or shortening permitted presentation cannot duplicate a trigger, reward, injury or relationship change.

Retain these checks in the first actual implementation and review their rendered output:

1. **TRANS-01 — Ordinary field encounter:** enter, choose without time pressure, resolve and resume in the same location. No replacement actors/terrain, input leakage, mandatory wipe or resource reset.
2. **TRANS-02 — Constrained geometry:** repeat on a bridge, in a furnished room and around a player-built obstacle. Positions remain legal, formations adapt and exits remain understandable.
3. **TRANS-03 — Boundary continuity:** fight across a streaming boundary and resume/reconnect during staging and aftermath. Entity IDs, authoritative outcomes and scenery mutations agree, regardless of client animation timing.
4. **TRANS-04 — Living performance:** review distinct approach, Wait idle, cooperative action, injury and aftermath motion at native scale. Respect the accepted style, restrained detail and reduced sprite repetition. Dedicated artists own performance assets; engine builders implement their contracts.
5. **TRANS-05 — Outside influence and vehicle:** admit a healer, projectile and coupled environmental effect before applying consequences; test a vessel encounter with persistent crew/cargo once its timing contract is defined.

These are acceptance scenarios to implement, not passing tests. They extend the v0.4 TIME/STREAM scenarios and existing expressive choreography requirements.

Before scheduling and persistence contracts harden, close the explicit treatment of calendar aging, appointments and deadlines during long holds; entry costs and merge ordering; environment/vehicle dependency scope; disconnect handoff; and maximum supported multiplayer spread. Those decisions need concrete examples and retained evidence. The transition style is now settled and should guide the consolidated GDD and implementation plan.
