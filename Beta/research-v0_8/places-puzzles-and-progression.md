# Places, puzzles and progression through space and time

## 1. The spatial and logical world

A rich location is more than a connected floor plan. Its inhabitants need access to work, food, shelter and each other; its defenses control approaches; its older layers explain present structures; and its opportunities depend on knowledge, equipment, social permission and time. Beta's generated settlements, dungeons, castles and otherworlds should therefore carry both a physical topology and a graph of possible activities and advancement. Neither graph can substitute for the other.

The local code and reference designs provide useful foundations, but none already establishes the complete requirement. This paper separates inspected code, primary design testimony, reported player experience and proposed Beta validation. No new generator or gameplay test was executed for this research expansion. Earlier original-game and RogueFiction results remain in the [review evidence](../review-evidence/README.md).

## 2. Local generator lineage

Original OpenFantasy already incorporates RogueFiction's topology intensity into `SiteRecipe.cs`: short set pieces, standard floors and more heavily interconnected floors. Its floor generator derives separate seeds from world, site, floor and retry salt, chooses a cyclic motif and permits up to 32 candidate layouts. The supported motifs distinguish two routes, a lock/key loop, a hidden shortcut and a dangerous detour. This is concrete reusable design and algorithm material.[^1]

`SiteValidator.cs` checks the embedded grid rather than trusting the room graph alone. It requires a route that does not depend on secret or locked doors, minimum loop counts, a second descent on eligible interconnected floors, and useful secret shortcuts. It also approximates room articulation by removing a room's actual tiles and checking how many other rooms become unreachable. Set pieces deliberately have different constraints. Those distinctions support pacing; every floor does not need maximum complexity.

There are important limits. The loop count records generated structure, not proof that two routes offer different decisions. A valid ordinary walking route does not prove access for a laden animal, a large vehicle, an injured character or a party split across time. Floor-local checks do not establish a multi-floor support model or settlement-wide logistics. Retry exhaustion is a reported generator failure, not an automatically safe live fallback. Beta should retain the tests' intent and expand their domains instead of declaring the old validator sufficient.

RogueFiction's `Map::generate` retries until minimum room count is reached; its shown outer loop is not bounded. Its generation connects nearby rooms, adds deliberate loops and treats secondary stairs and secret detours as explicit structure.[^2] These are valuable lineage lessons, while the unlimited outer retry should not be copied into a streaming host. The original OpenFantasy adaptation already makes the attempt count explicit.

Two other local projects offer narrower lessons. PacRogueSkillVet proposes walls and retains them only if all open cells remain connected; its fixed-size layout and reserved start/exit cells make its invariant easy to inspect.[^3] Rogue Links creates caves with cellular automata, retains the largest open component and applies placement rules for its golf-like course. Its actual generator also has an unbounded retry loop.[^4] These are useful studies in small, understandable proofs and collision clearance. Filling every disconnected space with rock would be unsuitable as a universal Beta rule: a sealed ancient crypt or an ethereal-only chamber may be intentional content.

OpenFantasy's settlement generator uses seeded site cells, biome suitability and tiered stamping around a plaza/road arrangement.[^5] It supplies a practical starting family, but is not a demonstrated generator for a historically evolved city with thousands of residents, infrastructure, property, underground levels and political districts. Preserve its content and suitable geometry routines while adding explicit civic purpose, scale and history constraints.

## 3. Jaquays-style design is history and use, not a loop quota

In a 2021 response, Jennell Jaquays connected her designs to historical architecture, multi-level interconnected structures and buildings changed by successive occupants. She described buried architecture and later excavation as part of Dark Tower's premise, and an interest in ecosystems within locations.[^6] This directly supports Beta's accumulated history, subfloors, urban crypts and changed uses. It is stronger evidence for the design intent than reducing her work to a fixed number of corridors.

Justin Alexander's design essay systematizes nonlinear exploration with multiple entrances and interconnections.[^7] It is a designer's analysis, not the original games' source code or a mandate that every structure follow the same pattern. Beta should distinguish the source designer's testimony from later terminology. The practical target is meaningful freedom to choose approaches, retreat, learn, return and exploit spatial understanding.

**Beta application.** Generate a place's purpose and material history before treating rooms as containers for random monsters. A chapel needs approaches, worship space, service access and a relation to its dead. An older crypt may predate the chapel, a later sewer may breach it, and a political boundary may regulate entry. Those causes can yield the alternate routes: public stairs, a service tunnel, an excavated breach, a roof approach or a shadow-plane correspondence. Routes differ through visibility, effort, permission, danger, supply and information.

A city needs a hierarchy of readable districts and local landmarks, not an enormous uniform grid. Gates, waterways, roads, markets and institutions organize navigation. The player sees a comprehensible neighborhood while the persistent city has credible homes, work and residents beyond that view. Underground connections should preserve actual vertical position and effects: draining a cistern can affect a lower chamber; damaging a support can affect a floor above. An overlay that merely looks subterranean is insufficient.

Reddit discussions expose a useful disagreement. Some players and referees value loops for bypassing enemies, returning efficiently and understanding a place; others report that their groups merely revisit rooms and do not notice the supposed richness.[^8] The lesson is to test whether the route matters in play. Patrols, supply, clues, danger and changing access can make a loop useful. Extra walking distance alone can turn a beautiful graph into tiresome backtracking.

## 4. Structured generation should remain authorable

Ludomotion's level-generation account describes a staged generation approach, while its later “Theory of the Place” emphasizes conceptual consistency, multiple entries and trajectories.[^9] This supports separating intended situations and relationships from their geometric realization. A location can be generated from a purpose and a set of choices, then embedded in terrain and dressed with culture and age.

A 2025 paper involving Ludomotion investigates maintenance of grammar-based level generation and the difficulty of carrying design intent into rules.[^10] The relevant production lesson is that more powerful generation also creates a tooling and diagnosis problem. Beta must let authors inspect why a place was produced, which rule supplied a gate or route, and which later pass invalidated an earlier assumption. Merely exposing a giant grammar file would not make procedural content maintainable.

Keep topology motifs, scene footprints and authored adventurelets inspectable. A generation record should identify source definitions, inputs, accepted alternatives, validation outcomes and the actual embedded geometry. Preview tools need representative seeds and intentional counterexamples. A content author should be able to answer why a passage exists, why it is blocked and which capabilities solve it without reading engine internals.

Preserve unsuccessful candidates and bounded retry reasons for diagnostics. A failure should produce a recoverable preparation error or a validated compatible fallback, not a partially generated live site. Never repair a generated puzzle by silently deleting a quest-bearing chamber after it has already entered history. Once the world commits a place, corrections become explicit migrations or world events.

## 5. Action puzzles and combination design

Nintendo's Echoes of Wisdom developer interview discusses the number of situations created even by one reusable object and the decision to permit inventive solutions. The team describes developing shared rules for acceptable player improvisation.[^11] That is directly relevant to a system with mining, magic, tools, acrobatics, animals and multiple planes: authored puzzles cannot assume a single intended move when the world already supports alternatives.

Beta should model a puzzle as a stateful situation with goals, constraints and observable feedback. A sluice gate can be opened by restoring a mechanism, arranging cooperative force, changing a flow or using a valid alternative route. A social gate may admit a recognized worker, a persuasive visitor or a convincing actor. The successful command must still use real resources, knowledge and permissions. If a solution works through general rules, content should normally honor it rather than detecting and rejecting the “wrong” solution after success.

Real-time exploration needs responsive movement and interactions, but not every skill should become a timed minigame. Fishing can offer technique and timing with adjustable assistance; writing may be a deliberate planning activity; a joint acrobatic maneuver may need coordination. Wait combat remains a separate selected decision policy. At transition, coupled hazards and mechanisms must use a coherent clock so a character held in tactical choice is not struck by a puzzle operating elsewhere in real seconds.

Repeated solutions are a risk. If one cheap box, spell or pet solves every obstacle, nominally vast possibility becomes a dominant routine. Generate situations with understandable constraints and different tradeoffs, rather than arbitrary immunity to the popular tool. Test traversal, sensing, stealth, social access, material manipulation and coordinated action as genuinely different approaches. Advanced gates can be visible early without being mandatory for introductory progress.

## 6. Mysteries require deduction, not merely hidden labels

Lucas Pope's discussion of Obra Dinn describes designing clues in the environment so players could work out identities through reasoning.[^12] A Golden Idol developer interview emphasizes repeated testing to refine mystery-solving.[^13] These are useful precedents for the desired Sherlock-like experience: presentation and evidence must support an actual inference the player can make.

A procedural Beta incident needs a committed causal model: who acted, what they could do, where and when they acted, what changed, and which evidence could survive. Testimony is a claim attributed to a witness. A dream is an impression. A material trace is an observation with acquisition conditions. A written record can be accurate, mistaken, forged or altered, with corresponding provenance. The journal must keep these categories distinct.

Build a clue graph from the incident rather than scatter independent hints that might contradict each other. A needed conclusion should have more than one usable evidence route where the scenario promises solvability. These routes may be physical, social, scholarly, magical or temporal. The solver should not require a clue behind the same gate whose answer that clue provides. A character's skill may reveal a new interpretation or technique, while the player still chooses a hypothesis.

Autonomous society makes the contract harder. Witnesses can travel or die; rain can alter tracks; a building can burn; another investigator can solve the case. The game should preserve alternate evidence where causally available, or transition truthfully to a changed outcome or cold case. It must not invent a new eyewitness after every destructive act simply to preserve a quest promise. An accusation can have legal and relationship consequences even when uncertain; the narrative must not equate confidence with truth.

Time travel is a legitimate investigative capability, not a forbidden loophole. The player can seek an earlier state, but must still establish the right place, time, identity and branch. Observing a crime in another branch does not prove an identical crime occurred in the current one. Intervening may prevent the original event and create a different mystery. Dream imagery can reveal what someone believed without supplying objective evidence of the event itself.

## 7. Challenge rating across events and capabilities

A BattleTech developer interview explains that procedural mission difficulty depends on context beyond the selected enemy group, including terrain and environmental conditions.[^14] Beta has still more interacting factors. Summing nominal enemy ratings will not account for an exhausted party, missing tool, narrow bridge, closed time window or unavailable clue.

Use a challenge profile containing execution difficulty, knowledge and capability prerequisites, resource commitment, party coordination, timing constraints, exposure and consequence severity. A DC can drive a particular uncertain action and a summary rating can communicate a broad assessment. Neither is proof of complete encounter difficulty or of puzzle solvability. Human puzzle difficulty especially depends on noticing, interpreting and combining information; a d100 check should not replace all reasoning.

The procedural planner should produce a **constructive advancement witness** for supported starting profiles: a feasible sequence of obtainable actions, resources and information leading to an appropriate next opportunity. Its state includes skill/knowledge, inventory, companions, social access, location, plane, date and relevant world revision. The witness must account for consuming a key ingredient, missing a ferry, needing to rest or visiting a teacher before their departure. A route that assumes the same one-use item twice is invalid even if every individual edge is legal.

Generation-time assurance and live assurance are different. A fresh supported start can require a validated accessible progression chain. Later world changes require invalidation and a new analysis of legitimate alternatives. The game cannot guarantee every former quest remains solvable while also permitting all its enabling people and resources to disappear. The [final tradeoff review](../spec-v1_0/PAIN_POINTS_AND_RESOLUTIONS.md) compares continuity options; a design must state its chosen guarantee honestly.

Optional hard routes remain important. A novice can see a sealed ruin, a difficult performance opportunity or a shadow-plane doorway without being expected to complete it. Show useful evidence of the obstacle and let later knowledge transform its meaning. Avoid universal level scaling that makes the same mountain or veteran arbitrarily weaker because a novice arrives. Route ratings belong to context and capabilities, with uncertainty when facts are unknown.

## 8. Time as learned opportunity

Nintendo's Majora's Mask manual describes a notebook that records people and their schedules.[^15] The useful Beta lesson is externalizing learned temporal opportunities. The original reset loop is a distinct design choice; Beta explicitly does not adopt a global three-day failure date or automatic reset.

People can have recurring routines, exceptional duties and changes caused by world events. A notebook should preserve known appointments and observations, show whether a schedule is confirmed or inferred, and indicate when news makes it stale. An approaching festival or caravan departure can create anticipation without pretending every quest is an urgent catastrophe. Waiting, resting, traveling and historical access should expose the cost of passing time and stop at meaningful interruptions under the selected co-op policy.

Reddit reactions to Majora's Mask range from finding its timing stressful to valuing the eventual understanding of its world and side stories.[^16] These observations are not evidence that Beta should remove all deadlines. They support testing the distinction between informative timing and constant pressure. A longer day alone does not solve overloaded schedules or excessive travel; activity duration, recurrence, rest and progression rewards must be tuned together.

## 9. Proposed retained evidence

| Scenario | What must be demonstrated |
|---|---|
| PLACE-01 — Embedded alternatives | A generated dungeon has two meaningfully different approaches after actual geometry, doors, hazards and affordances are placed; one approach can be blocked without falsely counting a merged corridor twice. |
| PLACE-02 — Inhabited layers | A chapel, crypt, sewer and neighboring homes have coherent elevations, access, water and history; an edit propagates relevant effects across boundaries. |
| PLACE-03 — City and supplies | Residents, visitors and an incoming merchant use real routes and stock; the city remains navigable at ordinary scale while its population persists outside detailed view. |
| PLACE-04 — Capability progression | An introductory character obtains the means for a suitable next challenge without a circular prerequisite, while an optional advanced gate remains available for later. |
| PLACE-05 — Temporal witness | A feasible-looking quest fails if its teacher leaves too early or an ingredient is consumed twice; the validator catches this across locations and planes. |
| PLACE-06 — Shared-rule puzzle | A physical, social and magical approach produce valid distinct consequences; real-time hazards stop or couple correctly at Wait admission. |
| PLACE-07 — Mystery under change | A witness leaves, rain alters a trace and a suspect dies; the case yields supported alternatives or a truthful changed/cold outcome without fabricated evidence. |
| PLACE-08 — Historical observation | Visit an unprepared earlier instant, investigate, return and reload; evidence preserves time/branch provenance and does not rewrite another player's history. |
| PLACE-09 — Legible nonlinear play | Human players notice and use a loop, landmark or alternate entry for a practical reason rather than merely walking farther. |
| PLACE-10 — Recovery after injury | A rare lasting injury changes the feasible advancement graph, but a supported care, adaptation or restoration route remains understandable for the qualified start profile. |

These are proposed scenarios, not passed tests. Geometry metrics, generator seed sweeps, solver witnesses, actual-build virtual playtests and human sessions answer different questions. Together they can show whether the world is coherent, navigable, balanced and enjoyable without reducing its possibilities to one prescribed route.

## Sources

[^1]: OpenFantasy, [SiteRecipe.cs](../../src/Sim/SiteGen/SiteRecipe.cs), [SiteGenerator.cs](../../src/Sim/SiteGen/SiteGenerator.cs), [SiteValidator.cs](../../src/Sim/SiteGen/SiteValidator.cs), local staged working tree inspected 8 September 2026; no edits or new tests.
[^2]: RogueFiction, [map.rs](/Users/alucinor/Workspace/Projects/RogueFiction/src/game/map.rs), especially `Map::generate` and `try_generate`; local source inspected 8 September 2026.
[^3]: PacRogueSkillVet, [MazeGenerator.cs](/Users/alucinor/Workspace/Projects/PacRogueSkillVet/src/GameLogic/MazeGenerator.cs), local source inspected 8 September 2026; no new test execution.
[^4]: Rogue Links, [level-generator.ts](/Users/alucinor/Workspace/Projects/roguelinks/game/src/level-generator.ts) and [generation guidance](/Users/alucinor/Workspace/Projects/roguelinks/steering/procedural-generation.md), local source inspected 8 September 2026; guidance and actual code distinguished.
[^5]: OpenFantasy, [SettlementGen.cs](../../src/Sim/SettlementGen.cs), local staged working tree inspected 8 September 2026.
[^6]: Warren D., [Jennell Jaquays on Jaquaysing the Dungeon](https://icastlight.blogspot.com/2021/09/jennell-jaquays-on-jaquaying-dungeon.html), 5 September 2021; direct response reproduced with attribution.
[^7]: Justin Alexander, [Xandering the Dungeon](https://thealexandrian.net/wordpress/13085/roleplaying-games/xandering-the-dungeon), design essay and historical examples, accessed 8 September 2026.
[^8]: Reddit, [Are looping dungeons for DMs or for Players?](https://www.reddit.com/r/rpg/comments/1f4milm/are_looping_dungeons_for_dms_or_for_players/), August 2024; direct thread retrieved. [How do I design dungeons that are more than just a series of rooms?](https://www.reddit.com/r/rpg/comments/1emoje6), August 2024; indexed excerpt, direct fetch failed. Nonrepresentative experiences.
[^9]: Joris Dormans/Ludomotion, [Level Generation](https://www.ludomotion.com/blogs/level-generation/), June 2019; [The Theory of the Place](https://www.ludomotion.com/blogs/the-place/), October 2021. Primary developer explanations.
[^10]: Protsenko and collaborators, [Maintenance in Procedural Level Design: Lessons from Ludoscope](https://pcgworkshop.com/archive/protsenko2025maintenance.pdf), 2025 PCG Workshop, four-page research paper.
[^11]: Nintendo/Grezzo, [Ask the Developer: Echoes of Wisdom, chapter 2](https://www.nintendo.com/en-za/News/2024/September/Ask-the-Developer-Vol-13-The-Legend-of-Zelda-Echoes-of-Wisdom-Chapter-2-2659795.html), September 2024. Primary developer interview.
[^12]: Alex Wawro interviewing Lucas Pope, [For Lucas Pope, Return of the Obra Dinn was a bunch of appealing design problems](https://www.gamedeveloper.com/design/for-lucas-pope-i-return-of-the-obra-dinn-i-was-a-bunch-of-appealing-design-problems), December 2018.
[^13]: Joel Couture, [The Case of the Golden Idol used frequent testing to sharpen its mystery-solving](https://www.gamedeveloper.com/business/-the-case-of-the-golden-idol-i-used-frequent-testing-to-improve-its-mystery-solving), 2023; primary developer interview.
[^14]: Alex Wawro, [Q&A: Building and balancing BattleTech's procedural missions](https://www.gamedeveloper.com/design/q-a-building-and-balancing-i-battletech-i-s-procedural-missions), 2018; primary developer interview with Kiva Maginn.
[^15]: Nintendo, [The Legend of Zelda: Majora's Mask 3D electronic manual](https://www.nintendo.com/eu/media/downloads/games_8/emanuals/nintendo_3ds_2/the_legend_of_zelda__majora_s_mask_3d/ElectronicManual_Nintendo3DS_TheLegendOfZeldaMajorasMask3D_EN.pdf), 2015 edition, notebook and schedules. Official indexed text consulted; linked interview context: [Iwata Asks, Adding New Discoveries](https://iwataasks.nintendo.com/interviews/3ds/majoras-mask-3d/0/6/).
[^16]: Reddit, [Majora's Mask finally clicked for me](https://www.reddit.com/r/truezelda/comments/ncawhf), May 2021; [Wanting to play Majora's Mask, how is the game?](https://www.reddit.com/r/zelda/comments/1cao1j5), April 2024. Indexed excerpts; self-selected experience rather than representative research.
