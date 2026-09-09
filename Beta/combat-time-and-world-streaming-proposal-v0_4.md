# Wait combat and seamless world streaming

8 September 2026. Research-backed recommendation for the next GDD. Godot/C#, a portable deterministic core and Wait-style command combat are established owner choices. The particular encounter policy below is proposed, not yet adopted or implemented. The owner also requests consideration of seamless block/grid loading through wilderness and vehicle travel, including ships, airships and sandships.

## Recommended reference and its limits

**Baldur's Gate 3 is the strongest reviewed precedent for relaxed tactical multiplayer with independent exploration.** Its reception gives us useful evidence that the overall combination can be enjoyable: it won BAFTA's 2024 Best Game and the publicly voted EE Players' Choice award. Those awards do not independently validate each clock rule or prove our much larger simulation. [BAFTA results](https://www.bafta.org/media-centre/press-releases/20th-bafta-games-awards-winners/).

Larian's developer explanations support deliberate turn-based planning, simultaneous control for adjacent allied turns, and split-party cooperative exploration. Its 2020 combat article also describes reducing animation overhead to improve pace. That article describes development-era behavior; use the supported principle rather than treating every early-access detail as a current specification. [Combat design](https://baldursgate3.game/news/a-little-about-combat-stealth_3), [Larian's multiplayer description](https://blog.playstation.com/?p=376236).

There is stronger technical evidence at the official modding boundary: `SetCanJoinCombat` changes whether an entity participates in combat timing and converts its statuses to turn-based handling. Joining that timing is separate from actively fighting. `EnterCombat` documents distance, sight and combat-group admission rules. This is evidence about an exposed gameplay interface, not a recovered full engine implementation. [Combat participation](https://docs.baldursgate3.game/index.php?title=SetCanJoinCombat), [admission rules](https://docs.baldursgate3.game/index.php?title=EnterCombat).

The pattern is not free of difficult edge cases. Larian's Patch 4 fixed a curse applying in real time instead of turn-based mode; Patch 2 addressed falling objects remaining suspended. These are specific historical fixes, not claims that those bugs still exist. They show why our hazard, physics and timing boundaries need explicit tests. [Patch 4](https://baldursgate3.game/news/patch-4-now-live_96), [Patch 2](https://www.baldursgate3.game/news/patch-2-now-live_89).

Divinity: Original Sin is another shipped cooperative turn-based precedent, documented by its original manual. This pass did not recover a readable full DOS2 manual or verify its detailed encounter-clock internals; BG3 provides the more useful direct evidence for the proposed boundary. [Original Sin manual](https://cdn.akamai.steamstatic.com/steam/apps/230230/manuals/DOS_manual_05_web.pdf).

## The compromise we must make explicit

Unlimited local decision time, uninterrupted independent world activity and unrestricted physical interaction across the two cannot all behave as one uniform clock. A trader cannot freely move, repeatedly heal or exchange goods with combatants who are supposedly frozen in the same physical moment. Streaming chunks does not solve this timing issue.

| Policy | Benefit | Cost |
|---|---|---|
| Shared authoritative pause | Strongest chronological consistency; unlimited consideration | A decision can interrupt every independently roaming player |
| Bounded encounters with tactical time | Closest fit to the reviewed independent-co-op precedent; preserves relaxed choices | Cross-boundary interactions need rules; local tactical duration differs from outside elapsed time |
| Continuous world and combat with decision timers/automation | Uniform progression and fewer pause boundaries | Adds decision pressure or substitutes automatic choices; less aligned with the owner's Wait preference |

**Recommendation:** use bounded on-field encounters with full decision holds, retaining a shared-pause policy for solo and coordinated sessions. Preserve Chrono-like party presentation, readiness, cooperative techniques and expressive actions; do not adopt D&D action rules merely because BG3 demonstrates the multiplayer pattern.

This is an explicit game-time abstraction, not a claim of physically uniform time. If the product requires strict shared chronology under every interaction, shared world pause is the consistent alternative. The default multiplayer calendar policy must be accepted as part of the GDD rather than hidden in implementation.

## Proposed encounter rules

1. **One owner for each participating entity and relevant effect.** The host owns membership, action order, held decisions and outcomes. Nearby noncombatants or hazards may participate in timing without becoming enemies. Rendering chunks are not membership boundaries.
2. **Hold tactical progression while a required decision is pending.** Readiness, damage-over-time, relevant hazards, cast/recovery and encounter deadlines follow the same declared hold. Cosmetic acting, interface feedback and music may continue. One subsystem must not count real seconds while another counts tactical steps.
3. **Allow cooperative planning without a speed advantage.** Compatible allies can prepare commands together; the host resolves conflicts and accepted action order deterministically. The fastest network packet must not win a contested item or combo. Initiative/readiness and command commitments determine legality.
4. **Admit an outside influence before resolving it.** A person attacking, healing, supplying items, moving a relevant obstacle or otherwise affecting the encounter joins its rules before the effect commits. A stealth approach remains possible, but does not create unlimited free strikes from outside turn order. Define the initial strike's cost and initiative placement explicitly.
5. **Handle hazards and geography through the same boundary.** Fire, falling objects, moving platforms, a damaged bridge, water and a vessel under attack need ownership and transition rules. A projectile or flood cannot ignore a hold because its origin lies in another chunk. Interacting encounters merge at a declared safe step; no actor or resource is copied.
6. **Make exit, flight and re-entry safe.** Carry action debt, cooldowns, statuses, custody and pursuit state through transitions. Walking across a chunk boundary does not end battle, refresh a turn or erase enemies. Do not release tactical protection onto an unresolved lethal hazard without a readable continuation policy.
7. **Make waiting socially manageable.** Show who is choosing, allow queued plans and an explicit handoff to a permitted party controller when someone disconnects. Do not impose a default countdown or silently choose an action for a connected player. Dedicated-server inactivity policy remains a disclosed setting.

These rules constrain the proposed implementation; they are not an assertion that BG3 implements this exact set or that the design is already proven.

## Calendar, persistent society and remaining closure

Maintain distinct meanings for host commit order, world calendar and tactical progress. A recorded transaction commits once in current authoritative order. It must not retroactively rewrite past world facts because a battle resolved late.

Before implementation, choose the explicit policy for aging, exposure, crop/work schedules, social appointments and world deadlines during an encounter hold. The leading bounded-encounter proposal lets unrelated society continue while participating activities and dependencies remain reserved. That implies a deliberate temporal concession: waiting in a fight is not a physically faithful passage of the same duration as outside activity.

Do not double-count work, allow paused goods to feed an external factory, secretly charge all accumulated combat damage on exit, or relabel an old outcome as having occurred before already committed outside events. A large disaster that physically couples regions may require admitting its affected area into shared tactical progression. The alternative is to use shared pause for that situation. How far that scope may expand is a gameplay and workload decision, not something to defer until after the scheduler is built.

The deciding retained test is concrete: one group fights at Wren's orchard while another travels or works nearby. Try supplying wood, spreading fire, moving an NPC, missing an appointment and joining the fight. The chosen rules must remain explainable to players and preserve one history, inventory and relationship state. Extend to a moving ship encounter before promising vehicle combat.

## Seamless grid streaming fits the world

The owner's analogy is useful: wilderness, coastlines, seas, deserts and travel can connect dense destinations while the next area becomes ready. Hello Games' GDC session describes continuous generation and seamless movement between space and populated terrain. The reviewed abstract supports the overall pipeline lesson; this pass did not verify its exact chunk sizes, predictive scheduler or internal loading boundaries. [Hello Games developer session](https://www.gdcvault.com/play/1024265/Continuous_World_Generation_in__No_Man_s_Sky_).

For a more directly documented grid-based precedent, Minecraft separates rendering from simulation distance and has changed chunk-build prioritization to address holes while flying quickly. These are transferable responsibilities. Beta's required persistent distant society must extend beyond simply freezing every unloaded actor. [Simulation-distance introduction](https://feedback.minecraft.net/hc/en-us/articles/4409891990285-Minecraft-Java-Edition-Snapshot-21w38a), [Minecraft 1.18 rendering changes](https://feedback.minecraft.net/hc/en-us/articles/4415128577293-Minecraft-Java-Edition-1-18).

### Recommended responsibilities

| Responsibility | Proposal |
|---|---|
| World address | Stable world/branch/layer and integer grid coordinates, with local positions; independent of whether the area is loaded |
| Generation | Macro geography, historical constraints and required landmarks first; deterministic detail keyed by coordinate and pinned generator/package versions |
| Nearby presentation | Render detailed terrain, people, creatures, objects, expressive motion and interactions around each camera |
| Predictive loading | Prepare cells ahead of movement, including possible turns, landing approaches and connected decks/interiors; increase look-ahead with vehicle speed |
| Active simulation | Detailed state where players, encounters, important actors or coupled effects require it |
| Distant simulation | Bounded jobs/aggregates retain named relationships, commitments, stockpiles, travel and consequential environment changes |
| Durable storage | Preserve generated bindings and mutations; unload visual resources without deleting the town, encounter, ship, people or history |

Chunk size is a profiling parameter. A town, battle or ship may span several chunks. A chunk is neither a complete scene, a whole NPC mind, an encounter instance nor a fictional time zone. Keep the streaming grid independent of encounter authority and story geography.

The leading budget relation is **look-ahead distance ≥ maximum supported travel speed × measured preparation latency + a margin for turns and jitter**. This is a sizing model to measure, not a claim of an already sufficient budget. Prioritize landing/collision/navigation data before decorative detail. Background workers can prepare data; canonical results commit in a stable order independent of job completion timing. Godot scene/resource integration belongs to the presentation adapter.

### Travel and continuity

- **Wandering:** paths, ridges, forests, ruins and local discoveries create worthwhile travel. Do not make every wilderness stretch an obligatory fight or a disguised delay screen.
- **Ships:** prefetch shorelines, ports, nearby water routes and relevant interiors/decks. The ship, crew and cargo retain identity while crossing cells.
- **Airships:** faster movement and broader visibility demand earlier loading and a lower-detail view of distant terrain. Landing changes detail, not the canonical location or passengers.
- **Sandships:** include this newly suggested traversal candidate explicitly in the next design pass. Its surface, propulsion, route, boarding and combat rules are still to be designed; it is not asserted to be a legacy OpenFantasy vehicle.
- **Battle during travel:** pin all required cells, actor state and route/vehicle context for the encounter. Moving a camera or changing residency cannot reroll enemies, abandon crew or duplicate cargo.

Shared vehicles need a parent-local coordinate frame for passengers and props plus a stable world transform. Streaming a boundary must not disembark riders, invalidate choreography or turn cargo into independently regenerated objects. This can remain ordinary data and C# modules; it does not require a new process or native bridge.

Seamlessness is a performance target, not an unlimited guarantee. If data is unexpectedly late, preserve correct authoritative movement and give understandable synchronization feedback rather than exposing a void, inventing an encounter to stall the player or rewriting the route. Long-distance portals may require explicit destination preparation; ordinary travel should be tested against its maximum supported speed and worst-case new-region work.

## Retained implementation evidence

Use the actual selected runtime and presentation path, with a small grid spanning land, coast and travel space:

1. Traverse boundaries repeatedly, including negative coordinates and different exploration orders. Terrain, named people, quest bindings and mutations remain consistent after restart.
2. Fight across a boundary with two clients; join, flee, reconnect and retry commands. Statuses, costs, clocks and item ownership remain correct.
3. Have an outside actor and environmental effect attempt to influence a waiting encounter. Verify the explicit admission and calendar policy rather than assuming a circular trigger is sufficient.
4. Move a populated vehicle with cargo and an ongoing interaction through cells; load/unload presentation and save/restart without loss or duplication.
5. Run a remote household relationship/work episode, then return. Reconcile detailed and coarse state without repeating work or erasing the story.
6. Measure cold traversal at the fastest supported vehicle speed, multiple players moving apart, an abrupt turn/landing, memory, frame/tick latency and save growth. Pick chunk/preload budgets from these results.

Keep these modules, fixtures and scenarios as the production foundation. Existing GDD editions remain untouched; this proposal informs the next consolidated version and does not claim to close every timing decision by itself.
