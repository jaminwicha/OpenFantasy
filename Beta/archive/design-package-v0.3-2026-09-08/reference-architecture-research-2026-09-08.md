# Reference architectures and what Beta should learn

Research completed 8 September 2026. Companion to the [architecture decision review](/Users/alucinor/Workspace/Projects/OpenFantasy/Beta/architecture-decision-review-2026-09-08.md).

The most useful lineage is a combination of **authored event choreography, reusable gameplay rules, durable world identity, and carefully bounded simulation**. The cited games do not collectively demonstrate an already-solved engine for Beta's entire ambition. Their differences expose the decisions Beta must make.

## Evidence standard and coverage

This study distinguishes four kinds of evidence. **Developer evidence** includes papers, talks, interviews and official documentation. **Implementation evidence** includes public tool/reimplementation code and maintainer documentation, with its scope identified. **Product evidence** establishes observable or advertised gameplay, not private internals. **Proposal** means a design inference for Beta, not a factual statement about a reference game's code.

The deepest available material concerns FFVI's reconstructed event system, Chrono Trigger's documented modding/event surface, Dwarf Fortress, Caves of Qud, Terraria's tModLoader boundary, Stardew's content tooling, and modern narrative scripting. Several commercial references disclose little internal architecture. Their sections explicitly limit conclusions; no ECS, database, networking or renderer implementation is invented to fill those gaps. GDC session abstracts are identified where the full recording was not examined. Source versions span different releases and must not be read as one contemporary cross-game benchmark.

Public reverse engineering is useful for understanding design. It is not original released source, and none of the reconstructed commercial code, ROM data, character sprites or story scripts is proposed for incorporation.

## 1. Final Fantasy VI: an event engine, not a universal world simulator

### What the evidence supports

The `everything8215/ff6` project reconstructs FFVI into field, battle, battle graphics, menu, sound, world and other functional groups. Its maintainer explicitly describes the directory organization as a best guess about the original source organization. This supports studying separable responsibilities, but not asserting that these exact modern folders existed at Square. [FFVI reconstruction repository](https://github.com/everything8215/ff6)[^ff6-code]

The event documentation describes a field interpreter that runs scenario commands, conditions, branches, calls and loops, coordinates object action sequences, and supports waiting versus asynchronous execution. It also bridges dialogue, maps, battles, music and special sequences. Different execution modes reflect the original game's needs and timing constraints. This is substantially richer than a chain of dialogue boxes, yet still a bounded authoring vocabulary. [FFVI event documentation](https://github.com/everything8215/ff6/blob/main/src/event/README.md)[^ff6-events]

### What Beta should take from it

The homage should be structural: designers can express a memorable scene through a small set of reliable operations while the engine handles movement, presentation and gameplay. “The captain leaves the inn, the party follows, the ship departs” should be authorable without writing a new scene controller each time.

For Beta, split that into two contracts. A **semantic action** changes authoritative facts through the host: board vessel, transfer item, accept obligation, open passage. A **presentation cue** stages what a player sees: walk animation, camera framing, music transition, dialogue timing. The same resulting fact may be presented differently to a nearby player, a late joiner and a remote observer.

An event instance should have a stable ID, resolved participants, current step, durable choices and declared cancellation/recovery behavior. Restarting halfway through a scene must not give its reward twice or trap a player behind a finished cutscene. A missing or dead participant needs an alternate resolution, not a raw script crash.

Do not import memory addresses, fixed flag budgets, bank layouts, hardware sprite limits, global pause assumptions or a requirement that every story be a linear authored script. FFVI offers an excellent authoring spirit; Beta's history and multiplayer authority need modern semantics underneath it.

**Proof:** author one departure sequence from reusable commands; interrupt it by disconnecting and restarting; replay the same semantic result with different camera and animation timing. The authoritative result must agree while the presentation remains free to improve.

## 2. Chrono Trigger and Chrono Cross: staged continuity and readable differences

### What the evidence supports

Chrono Trigger event experiments document object startup/idle routines and callable functions, memory conditions, movement, coordinate tests, exploration-mode changes, camera scrolling and battle triggers. Their worked encounter sequence moves party and enemies into position, runs a battle, removes objects and restores exploration. This is direct reverse-engineering/mod-author evidence for **coordinated encounter staging**; it is not a complete recovered engine specification. [Temporal Flux event experiments](https://datacrystal.tcrf.net/wiki/Chrono_Trigger_%28SNES%29/Temporal_Flux_Event_Tricks)[^ct-events]

Temporal Redux is an actual editor implementation built on Temporal Flux and Jets of Time work. Its README identifies incomplete testing and editing limitations. Its existence corroborates a manipulable event-data surface, but its code organization and current limitations must not be attributed to the original game. [Temporal Redux](https://github.com/OnemusCT/temporal-redux)[^ct-tool]

Square's Chrono Cross description explicitly centers two interlinked parallel worlds and a large recruitable cast. That establishes the dramatic device, not dynamic database branching or a continuously simulated alternate history. [Chrono Cross product description](https://eu.store.square-enix-games.com/chrono-cross-_-the-radical-dreams-code-in-a-box)[^cross]

### What Beta should take from it

On-field battle works because an encounter has a usable stage. For procedural maps, reserve combat footprints, entry/escape space, readable positions and camera bounds before decorative placement. Ensure that furniture, bridges, slopes and roofs do not break the encounter's legibility. “No separate battle screen” is a level-generation constraint as much as a renderer feature.

Temporal play needs recognizable anchors. An altered bridge, a descendant, a changed profession and a missing monument are comprehensible connections between versions of a place. A branch identifier and a huge generated chronicle are not enough. Beta should carry a place/person/artifact lineage across its supported epochs while distinguishing each live instance.

The attractive mistake would be to infer that Chrono Trigger solved arbitrary intervention in generated history. It did not provide evidence for that claim here. Beta must choose causality and ownership rules independently: when a branch is created, whether travelers can return, what happens to carried material, whether two versions of a person can coexist, and how another player's world is protected.

**Proof:** visit a ruined crossing, travel to a supported earlier checkpoint, alter one event and return through an explicit branch. The town should differ for a traceable reason. The unaffected source history stays unchanged. Two players must understand which history they occupy without reading an administrative interface.

## 3. Sea of Stars: presentation requires a production system

### What the evidence supports

Sabotage's official press kit describes a custom render pipeline with dynamic lighting, traversal including swimming/climbing/vaulting, and turn-based combat with timed inputs, combinations and damage-type locks, integrated with the field rather than a separate battleground. These are verified design/presentation commitments; the press kit does not disclose shader code, spatial storage or save architecture. [Official press kit](https://sabotagestudio.com/presskits/sea-of-stars/)[^sos-press]

In a direct 2025 interview, Thierry Boulanger describes developing a new project's pipeline with a small core team while others work within the known DLC pipeline. He also describes explicitly budgeting a small number of exceptionally expensive presentation moments. This is evidence for deliberate production planning and selected bespoke work, not a claim that all scenes cost the same or that a generic shader produces the style. [Boulanger interview](https://gameinformer.com/interview/2025/07/30/sea-of-stars-was-seven-times-more-successful-than-anticipated-and-more-from)[^sos-production]

### What Beta should take from it

The requested presentation bar requires coordinated sprites, tiles, props, animation, environmental layering, lighting, camera, interface and sound. The right architecture preserves artist control over those surfaces. A modular body composer may help variation, but it cannot be the only way to express an important character's silhouette or acting.

Keep a separate artist role with an explicit export contract. Artists define the accepted appearance, native dimensions, frame identity and source assets. Engine builders provide predictable pivots, sorting/occlusion, animation timing, import settings and profiling. Agree on whether light is painted, dynamic, or a controlled combination. A lighting change can require new art; it is not always a final polish pass.

Prove a town street and a dungeon room early in motion. Include a moving character, tall props, a roof/foreground occluder, water, a transition in elevation, an action effect and a readable UI. Compare the art at native scale and intended display scale. Do not postpone this until a large simulation makes it expensive to change sprite scale or world geometry.

Reserve bespoke set pieces for moments that deserve their production cost. Most locations must be buildable through the repeatable environment kit. If every procedurally generated room requires individual artist rescue, the campaign scope is not yet viable.

**Proof:** a second artist task produces a new room and creature using the accepted style kit; the engine imports them without bespoke gameplay changes. The user accepts the result in motion. Measure cleanup and rejected attempts as production work.

## 4. Stardew Valley and Fields of Mistria: coherent content, explicit change

### What the evidence supports

ConcernedApe's 2021 announcement documents the Windows game's move from 32-bit XNA to 64-bit MonoGame, a modder preview, compatibility concerns and a legacy option. It demonstrates that a recognizable game can preserve its identity while changing supporting technology. It does not disclose every internal object or scheduler. [Developer announcement](https://forums.stardewvalley.net/threads/important-announcement-for-modders-stardew-valley-64-bit.8669/)[^sdv-migration]

Content Patcher's maintainer guide exposes a useful content architecture at the **mod-tool boundary**: versioned JSON patches, replacements and targeted data/image/map edits, conditions/tokens, and organized inclusion of multiple files. It prefers targeted edits when possible over replacing an entire asset. This is evidence about Content Patcher, not proof that vanilla Stardew is implemented internally as a fully declarative engine. [Content Patcher author guide](https://github.com/Pathoschild/StardewMods/blob/develop/ContentPatcher/docs/author-guide.md)[^sdv-content]

Fields of Mistria is a particularly relevant correction to a superficial engine comparison. GameMaker's showcase records its earlier engine association. NPC Studio's 2026 announcement describes migration to an internal engine, intended stability/performance/platform/modding benefits, a separate beta, save import, and broken compatibility with older mods. The studio's announcement is newer and should take precedence over stale “made in GameMaker” lists. [GameMaker showcase](https://gamemaker.io/en/showcase/fields-of-mistria), [NPC Studio migration notice](https://www.fieldsofmistria.com/post/new-engine-beta-branch)[^mistria]

### What Beta should take from them

A small, legible community can carry many interacting activities when people and places remain recognizable. Treat work, residence, dialogue and relationships as different views of one persistent person. That does not require simulating an entire civilization before the first villager becomes interesting.

Use namespaced definitions and targeted composition. A content package that adds a crop should not replace an entire base data file; a portrait variation should not need to mutate character identity. Define conflict resolution and removal behavior before promising every mod combination. Beta's first-party packages and community packages can share a contract while having different trust/capability permissions.

The two migration examples do not mean “write a custom engine.” They mean technology can change for concrete reasons, and migration itself needs a product contract. Preserve an old save, import to a separately identified target, report incompatibilities, and give the user a recoverable path. For Beta, the state model mismatch is a demonstrated reason to build a new runtime boundary; there is no comparable demonstrated reason to replace Godot's renderer today.

Avoid equating calendar routines or relationship meters with the GDD's household obligations, political loyalties and labor economy. Those require additional rules. Start with one actual obligation that changes what an NPC can do, rather than many personality fields without consequences.

**Proof:** one worker has a home, dependent and job. Recruiting that worker changes labor and household state; both persist after restart. A package changes their visual appearance without changing their identity, save ownership or relationship history. Removing the package produces an explicit fallback/migration result.

## 5. Terraria: bounded world mutation and a real extension lifecycle

### What the evidence supports

tModLoader's `ModSystem` API exposes hooks for ordered world-generation tasks, world load/unload, world save/load data, and network send/receive. These are useful examples of **separate lifecycle responsibilities**. Saving custom data and synchronizing it are separate operations. Hook availability is not proof that arbitrary mods compose deterministically or that Terraria itself uses Beta's proposed authority model. [Stable ModSystem API](https://docs.tmodloader.net/docs/stable/class_mod_system.html)[^tml-system]

The maintainer's netcode guide distinguishes single-player, server and multiplayer-client execution. It explains that information must be synchronized deliberately and describes packets and hooks for relevant state changes. A mod working locally is not evidence of correct multiplayer behavior. [Basic Netcode](https://github.com/tModLoader/tModLoader/wiki/Basic-Netcode)[^tml-net]

### What Beta should take from it

Making and exploration should change the navigable world. The fundamental unit is an authoritative mutation with consequences: remove material, open a route, place a mechanism, transform an ingredient, trigger a hazard. Terrain visuals are an output of that state, not the state itself.

The dependency graph matters more than the recipe count. A newly available tool should unlock a route, resource or transformation that changes the next expedition. Avoid crafting catalogs where every recipe is merely an interchangeable stat upgrade. Define a small graph with alternate inputs and constraints, then play it before scaling.

World-generation passes need order, preconditions and validation. A settlement pass should not overwrite a river after the river's historical meaning has been fixed. A late mod pass that adds a dungeon must declare required terrain and preserve world invariants. Keep generator errors reproducible through seed, version, pass and decision records.

The save/load/net separation is especially important for Beta. “The item exists in local memory,” “the item is on disk,” and “another client knows about it” are three different facts. A retried craft must not consume material twice; a disconnect must not create two rewards. The current OpenFantasy recipe path is a useful gameplay starting point, but needs a durable action contract.

**Proof:** two clients attempt a recipe against the same finite resource. At most one authorized consumption occurs unless sufficient stock exists. Kill/restart the host at each transaction boundary and retry. The terrain, inventory, receipt and other client's view agree. Then add one package-defined process and repeat.

## 6. Dwarf Fortress: cross-domain consistency has a real cost

### What the evidence supports

Tarn Adams describes world generation as a sequence involving terrain, climate/rain effects, biomes, checks and historical simulation. He characterizes the historical phase as a zero-player strategy-like process with agents and logged events, and discusses extracting interesting stories from much less interesting raw activity. This supports staged causality and narrative selection rather than “generate random lore beside random terrain.” [Developer Q&A](https://www.gamedeveloper.com/design/q-a-dissecting-the-development-of-i-dwarf-fortress-i-with-creator-tarn-adams)[^df-history]

In the 2023 technical interview, Adams discusses the move from 2D to 3D, the way later live-world work shared historical event structures while differing from coarse history generation, and how body-part mechanics spread into other systems such as remains and syndromes. The interview emphasizes the consistency obligations created by interconnected systems. It does not establish identical simulation resolution everywhere or perfect parity between fortress, adventure and historical generation. [Technical interview](https://www.gamedeveloper.com/programming/how-tarn-adams-upgraded-and-optimized-dwarf-fortress-for-its-official-steam-release)[^df-tech]

### What Beta should take from it

DF-style depth means rules remain meaningful when they meet. If an injured creature loses a body part, combat, equipment, labor, healing, death and item provenance must not each invent a contradictory result. A giant, dragon or merchant cannot be a historical actor in one subsystem and a nameless respawning loot bag in another without an explicit distinction.

Beta should define shared primitives only where multiple real mechanics need them. “Material,” “person,” “location,” “process,” “relationship” and “obligation” are promising. A universal bag of arbitrary properties and an untyped event bus are not enough: they move coupling into conventions that cannot be checked reliably.

Resolution changes should preserve consequences while reducing work. A named worker making a finite number of iron ingots in an unloaded town must not simultaneously be counted in an anonymous labor aggregate. When the town becomes active, conserved inventory and identity must agree. An approximation should be named and measurable: for example, aggregate migration flows are not individual turn-by-turn pathfinding.

Do not let “Dwarf Fortress or beyond” substitute for an acceptance test. Choose representative interactions: historical conquest leaves ownership and material evidence; a dependent prevents an NPC's long journey; a disrupted supply route affects production; a destroyed bridge changes both movement and trade. For each, test local versus coarse execution and determine the permissible difference.

This research makes Beta's full scope more challenging, not less. An interconnected rules vocabulary creates implementation reuse **and** a combinatorial verification burden. Limit the first supported domain combinations, reveal the limits to players, and expand through demonstrated consistency.

**Proof:** simulate the same bounded settlement episode in detailed and coarse modes. Compare exact conserved materials, unique identities and consequential events, plus declared tolerances for approximated quantities. Promote/demote the region repeatedly and ensure it does not duplicate workers, goods or historical consequences.

## 7. Caves of Qud and RimWorld: history is not automatically story

### What the evidence supports

Grinblat and Bucklew's 2017 paper explicitly separates history as a simulated process from history as an artifact interpreted in the present. Qud's sultan-history system uses state transitions and a replacement grammar, selecting events and rationalizing them afterward. Shared properties make biographies cohere; the paper also describes a hybrid of authored narrative and procedural local areas. It is a deliberate alternative to full historical causality, not evidence of one. [Developer paper](https://pcgworkshop.com/archive/grinblat2017subverting.pdf)[^qud-paper]

Grinblat's 2018 GDC slides describe that history-generation approach; the 2019 session overview extends the research trail to generated villages, cultures, histories, architecture, NPCs and quests. The full 2019 recording/large slide deck was not examined in this review, so detailed village algorithms are not asserted. [2018 slides](https://media.gdcvault.com/gdc2018/presentations/Grinblat_Jason_Procedurally_Generating_History.pdf), [2019 session](https://www.gdcvault.com/play/1026313/Math-for-Game-Developers-End)[^qud-talks]

Tynan Sylvester's own essay argues that complex simulated processes do not automatically create a compelling experienced story; selection, interpretation and presentation matter. His RimWorld GDC session frames the product around generating stories. These sources provide design reasoning, not a disclosed database or complete storyteller implementation. [The Simulation Dream](https://tynansylvester.com/2013/06/the-simulation-dream/), [RimWorld GDC session](https://www.gdcvault.com/play/1024232/-RimWorld-Contrarian-Ridiculous-and)[^rimworld]

### What Beta should take from them

Beta's canonical history and its inhabitants' accounts must be separate. A physical artifact can have an actual provenance, while a shrine inscription exaggerates or misattributes it. This supports discovery, political propaganda, unreliable witnesses and scholarly introspection without making the server's truth arbitrary.

For generated prehistory, causality can be coarser than moment-to-moment simulation, but committed facts need consistency with the world they produced. For live play, never retroactively invent an NPC's motive or material supply simply to rationalize a convenient narrative event. Use Qud's account-generation lesson for **accounts**, and test any additional fact-generation rules against Beta's explicit historical contracts.

A narrative layer should find consequential situations and bind them to readable opportunities: an unpaid obligation, a disputed artifact, a broken supply chain, a refugee who knows an old route. It should not invent a disconnected quest that mentions a non-existent settlement or asks for a killed unique actor without adapting.

Decide whether a pacing director is allowed to inject new events or only select among real opportunities. Both can be good designs; disguising intervention as an inevitable simulation outcome would undermine the promised introspection. If injection is permitted, make it a declared rule with recorded decisions and resource/actor constraints.

Preserve an authored voice. Procedural grammars, constraints and optional model-assisted proposals can generate variation within that voice. A long generated biography is not a substitute for one convincing interaction with a person the player remembers. The AI-off version must still make those interactions coherent and playable.

**Proof:** generate several histories, then inspect an artifact, site, rumor and quest derived from each. Trace every factual assertion to committed state or an explicitly marked belief. Have players recount the consequence in their own words. A technically valid story that they cannot understand or care about has not met the narrative gate.

## 8. Modern scripting: useful boundaries, different authority choices

Larian's published Osiris documentation describes an event-driven declarative language using events, queries, calls and fact databases to manage global game-state transitions. It also states that some operations are guaranteed through failsafes; the example of moving a valid character can fall back to teleportation if a path is blocked. This is a concrete lesson in authoring semantics, not a universal recommendation to bypass world rules. [Osiris introduction](https://docs.baldursgate3.game/Scripting:_Introduction_to_Osiris)[^osiris]

**Beta implication:** typed conditions and consequences are valuable, but commands must disclose their authority. A cinematic “ensure arrival” command is different from a diegetic “attempt to walk there” action. The former can support a controlled cutscene; silently using it for ordinary NPC work would break the simulation promise. Record which operations may guarantee outcomes and what fallback is visible to players.

OpenMW's Lua documentation separates global, local and player scripts, controls object visibility/mutation, and distinguishes simulation/game timers and reliable saved callbacks. It explicitly describes a multiplayer-compatible API concept without claiming that scripting currently implements multiplayer. This is evidence about **OpenMW**, not Bethesda's original Morrowind engine. [OpenMW scripting overview](https://openmw.readthedocs.io/en/stable/reference/lua-scripting/overview.html)[^openmw]

**Beta implication:** scope is an API property. A player observation script should not see all server truth; a local visual script should not mutate another region; a persistent timer needs a stable callback identity and saved arguments. Beta should keep its own server-authoritative contract rather than adopting OpenMW's described delegated-client concept merely because it is documented.

Exult's Usecode documentation identifies scheduled script/animation operations and intrinsic calls for Ultima VII/SI/Exult, with distinctions between game variants and extensions. It is valuable reimplementation/tool evidence for object-centric authoring, but not a released original architecture specification. [Exult Usecode reference](https://exult.info/seventowers/code.php?DATAFILE=ucc_scripting.dat&TITLE_IMAGE=usecodetitle.png)[^exult]

**Beta implication:** objects can expose coherent verbs and reusable schedules. Keep the nostalgic density of interactable objects while replacing opaque opcode conventions with typed, inspectable operations. None of these references justifies giving arbitrary package code unrestricted access to the host.

## 9. Remaining principal GDD references

### Crystal Project: tools and storage determine the maker's real limits

In his May 2023 developer AMA, Andrew Willman explains that he built a custom engine with FNA, encountered shader differences across desktop platforms, and adapted internal tools into the public mod editor. That required changing the tools from editing the game's database directly to storing changed data that could be applied as patches. He also explains that terrain editing was not exposed: the compressed in-memory world representation and unfriendly terrain tooling made editable-world persistence a substantial additional task. These are direct implementation/production disclosures, scoped to that period. [Developer AMA](https://www.reddit.com/r/JRPG/comments/13smxa4/a_year_ago_i_released_crystal_project_a_nonlinear/)[^crystal-tools]

**Beta implication:** shipping developer tools to players is not simply exposing their buttons. Developer authoring may assume a trusted operator, unlimited access, rebuilds and a clean initial world. An in-game creator must operate within permissions, current-world identities, resource costs, save transactions and multiplayer conflicts. Build this difference into the content contract from the beginning.

Use the same definition schema while giving each authoring context its own permitted operations. A designer can author a dungeon grammar in a development package. A player can construct a bounded blueprint from learned primitives. A server validates the result, commits a versioned definition or instance, and records its ownership. The shared engine should not confuse “same schema” with “same privileges.”

A single favorable demo is insufficient: require a second person or artist/author task to create a new meaningful object or room from the documented tools, import it without touching runtime source, save its changed state and migrate it. A tool that feels productive only to its creator is not yet Beta's promised maker ecosystem. Cross-platform rendering also needs actual visual verification, even when the underlying framework supports every target OS.

### The HD-2D lineage: artist autonomy rather than a renderer mandate

Acquire's direct developer interviews provide deeper evidence from the first two Octopath games. The first account describes mixing 2D pixel characters with 3D environments and improving combat effects by coordinating lights with the effects. The second explains changed character proportions and map detail, the difficulty of making flat sprites look natural in 3D scenes, constrained exploration cameras, and extensive Sequencer use for lighting, encounters, abilities and event cameras. Acquire emphasizes artists being able to experiment and preview their work independently. These are facts about **Octopath I/II**, not asserted source-code facts about Octopath 0 or Elliot. [2019 developer interview](https://www.unrealengine.com/spotlights/octopath-traveler-s-hd-2d-art-style-and-story-make-for-a-jrpg-dream-come-true?lang=en-US), [2023 developer interview](https://www.unrealengine.com/developer-interviews/octopath-traveler-ii-builds-a-bigger-bolder-world-in-its-stunning-hd-2d-style?lang=en-US)[^hd2d]

**Beta implication:** the desirable capability is rapid, independent visual iteration. Give artist tasks a stable preview scene, visible import errors, animation controls, lighting alternatives and reproducible captures. Engine builders should not be a manual bottleneck for every palette, camera or timing experiment. Conversely, visual sequencing must read authoritative world/calendar state rather than becoming the source of simulation time.

A Chrono Trigger-style game can use flat 2D, layered 2D with height semantics, or 3D geometry rendered through a controlled camera. Those are alternatives to test against the accepted style. A full 3D camera increases the burden on billboard orientation, occlusion and pixel sampling. It does not automatically increase authenticity. Start with the smallest presentation arrangement that produces the accepted scene and useful elevation/traversal.

The old three-frame, tiny-sprite template should not constrain acting or proportions. Neither should an unexamined HD-2D convention such as heavy blur, bloom or perspective. Freeze the native scale, view angle and lighting contract after the user sees representative art in motion, then build exports around that accepted choice.

**Proof:** the artist changes a character action, scene light and event-camera cue without modifying gameplay code. The engine renders consistent accepted assets across two target configurations. The semantic action results remain unchanged. Only then extrapolate the authoring workflow to a longer campaign.

The following references matter, but the retrieved primary material supports product design more strongly than internal implementation. The architectural column is explicitly a **Beta proposal**. It must not be cited later as a factual account of proprietary source code.

| Reference and evidence | Supported lesson | Beta proposal and meaningful proof |
|---|---|---|
| **Final Fantasy IV.** Square identifies it as the series' first ATB game. [Official note](https://www.square-enix-games.com/en_US/home/final-fantasy-iv-pc-patch-update)[^ff4] | Temporal readiness can distinguish party roles without requiring action combat | Represent readiness/cast/recovery separately from animations. Compare readable command timing against current OpenFantasy ATB. Test menu policy with two players; do not inherit a solo global-pause rule. |
| **Secret of Mana.** Square's remake description confirms action-oriented play; its published Steam page explicitly supports three-player local co-op. This is remake evidence, not a recovered SNES network architecture. [Official page](https://mana.square-enix-games.com/en-gb/games/secret-of-mana), [published co-op description](https://store.steampowered.com/app/637670/2_SECRET_of_MANA)[^mana] | Party adventure can remain direct and social in the field | Prototype action readability, companion ownership, command availability and shared-screen/remote-camera assumptions. Local co-op does not prove asynchronous server play. |
| **Crystal Project.** The developer-published page specifies threat management, visible turn order/damage/probabilities, class combinations, seamless traversal, jumping and exploration freedom. [Developer page](https://store.steampowered.com/app/1637730)[^crystal] | Mechanical transparency and traversal can be central to exploration JRPG identity | Expose truthful previews through the same evaluator used to resolve actions. Disclose uncertainty from hidden information. Test alternate routes and ability combinations rather than relying on invisible progression walls. Do not infer engine internals from voxel appearance. |
| **Octopath Traveler 0.** Square describes a created protagonist, town rebuilding, recruiting residents, Path Actions, transferable skills and parties of up to eight. [Official description](https://na.store.square-enix-games.com/octopath-traveler-0)[^octopath] | Recruitment can connect adventure, combat roles and a home community | One person should retain identity across resident, worker and party views. Test recruitment against household obligations and persistence. The advertised town feature does not establish a DF-like labor economy. |
| **The Adventures of Elliot: The Millennium Tales.** Square describes action battles, a fairy's support abilities, path-opening exploration and history across ages. [Official description](https://na.store.square-enix-games.com/the-adventures-of-elliot_-the-millennium-tales)[^elliot] | Historical travel can be integrated into active exploration, with complementary movement/interaction abilities | Model historical topology and ability-gated routes explicitly. Test camera, collision and readability before adopting a dimensional presentation. HD-2D appearance is not evidence for a general branch simulation or a required renderer. |
| **Rune Factory 4 Special.** Its official feature page combines farming/family, town responsibilities, combat, townsfolk companions and monster friendship. [Official features](https://runefactory.com/rf4/feat.html)[^rune] | Several careers can reinforce one daily adventure/home loop | Share identity and progression across expedition, farm and community activities. Test that a heroic path remains viable without mandatory mastery of farming; support alternate material acquisition. Do not assume the reference simulates hereditary politics. |
| **Dragon Quest Builders 2.** Square describes a single-player campaign and separate multiplayer building support up to four players. [Official description](https://na.store.square-enix-games.com/dragon-quest-builders-2)[^dqb] | Building can carry an authored adventure and become a social activity | A blueprint describes semantic purpose as well as geometry: workshop inputs, capacity, access and work. Test that a recruit can use a built facility. Campaign plus multiplayer building is not proof that the entire narrative is shared co-op. |

The implication is **selective integration**. Beta can combine Crystal Project's readable decisions, Octopath's recruitment/home connection, Rune Factory's reinforcing careers and Builders' meaningful construction. It does not need all their progression systems, pacing assumptions and UI modes simultaneously.

## 10. The comparative backlog: scale and systems, with evidence limits

These games appear as additional comparisons in the GDD. They are useful guardrails, not additional launch requirements.

**Minecraft.** Mojang's 1.18.2 notes document data-driven custom cave/structure generation and changing generation formats. This establishes a supported extension surface at a particular version, not infinite cross-version seed compatibility. **Beta lesson:** serialize generator/package identity, validate generation passes and define how already-generated areas meet new rules. Test old-world loading at a generated/unexplored boundary. [Mojang release notes](https://www.minecraft.net/en-us/article/minecraft-java-edition-1-18-2)[^minecraft]

**No Man's Sky.** Innes McKendrick's GDC session abstract outlines continuous generation from voxels through polygonization/texturing to population/simulation. This review uses the abstract's pipeline description, not unexamined details of its job system. **Beta lesson:** separate baseline generation, resident representation and live simulation, and budget transitions rather than generating everything upfront. A large address space does not imply dense authored adventure. [Developer session](https://www.gdcvault.com/play/1024265/Continuous_World_Generation_in__No_Man_s_Sky_)[^nms]

**Kenshi.** Lo-Fi's published developer AMA describes its engine choices, the difficulty of extending an old engine, and the tension between native-code performance and modding scope. These are the developer's historical statements, not a current comparative performance benchmark. **Beta lesson:** define what packages can extend and what remains native; do not promise unrestricted extension of every optimized inner loop. Choose technology against actual constraints rather than a remembered brand association. [Lo-Fi developer AMA](https://lofigames.com/2549-2/)[^kenshi]

**Mount & Blade.** TaleWorlds' mission-authoring documentation specifies deliberate navigation and layout requirements; its hideout guidance even favors a controlled route and encounter spacing for that mission type. **Beta lesson:** different activity types deserve different topology grammars. An open world need not make every local encounter an unrestricted maze. Validate generated site intent and pacing, not connectivity alone. The precise campaign/mission state-transfer implementation was not established here. [TaleWorlds scene-authoring guide](https://moddocs.bannerlord.com/authoring-mission-scenes/hideouts/)[^mount]

**Crusader Kings.** A publisher-authored CKIII diary discusses surfacing story-cycle content to players. It is evidence that a supported simulation/narrative feature can remain difficult to perceive without dedicated UI. **Beta lesson:** institutions, claims and obligations need readable changes and actor-level consequences. A graph can be correct and still feel absent. Do not infer an exact internal scheduling algorithm from the diary. [Publisher diary archive](https://store.steampowered.com/news/posts/?appgroupname=Crusader+Kings+III&appids=1158310&enddate=1774611464&feed=steam_community_announcements)[^ck]

**Valheim.** The retrieved developer interview discusses engine selection; it does not provide enough primary technical evidence here to specify the game's exact zone ownership/replication machinery. **Beta proposal:** use it as a co-op expedition/home pacing comparator, then independently test disconnection, building ownership and world residency. Do not copy an assumed distributed-authority scheme into Beta. [Developer interview, Japanese](https://gamemakers.jp/article/2025_10_02_118785/)[^valheim]

**Starbound.** Public search surfaced community patch guides and source forks of uncertain relevance/provenance for a primary-source engine assessment. They were not used to assert the proprietary engine's internals. **Beta proposal:** retain it as a future comparison for biome/planet variety and data-driven content composition. Test whether additional generated regions create new decisions rather than repetitive cosmetic permutations. A deeper, version-specific inspection of sanctioned mod documentation is still needed before adopting its package behavior. This is a stated research limit, not a claimed completed source-code audit.

RimWorld, Caves of Qud, Ultima VII, Morrowind and Baldur's Gate 3 are treated more deeply above. The backlog should expand the questions being tested, not inflate the initial feature checklist.

## 11. A combined experiment that earns the lineage

Build a small original place around one connected chain of consequences:

1. A damaged river crossing affects travel and a settlement's supply route.
2. A recruitable craftsperson knows how to repair a mechanism but has a dependent and an obligation.
3. A ruin contains a relevant material/artifact with a traceable history. A rumor gives partial, possibly mistaken information.
4. Players choose between a quiet route and noisy forced entry, causing a readable enemy response.
5. A staged on-field encounter preserves the JRPG presentation and supports complementary party actions.
6. The resulting craft/build transaction changes the crossing and the town. Players can inspect the actual reason.
7. Restart preserves the change. A later temporal proof changes one supported historical event in an explicit branch without duplicating the artifact or corrupting the source world.

This combines classic event choreography, systemic state, sensory play, meaningful making, recruitable people and temporal identity. It also makes each proposed architectural boundary falsifiable. If players do not enjoy the expedition or recognize its consequences, a larger simulation will not rescue the design.

The artwork must make that same chain clear. Artist agents produce the accepted character, creature, mechanism and environment kit; engine builders support them through stable metadata and actual rendering. The [art tooling study](/Users/alucinor/Workspace/Projects/OpenFantasy/Beta/art-style-calibration/pipeline-research.md) separately evaluates MCP coverage and distinguishes currently callable tools from candidates. The generated calibration sheets remain proposals, not accepted production sprites.

## Sources and provenance

All links were accessed on 8 September 2026. Publication/version dates below identify the evidence's scope; undated live documentation means the accessed version, not a guaranteed future API. Near-claim links identify the source supporting each factual paragraph. Beta recommendations and experiments are original synthesis.

[^ff6-code]: everything8215, *FFVI disassembly/reconstruction*, undated live repository. Primary reconstruction-author evidence; directory interpretation explicitly tentative.
[^ff6-events]: Same project, *FF6 Event Script*, undated live documentation. Primary reconstruction-author account of the event surface.
[^ct-events]: Data Crystal, *Chrono Trigger / Temporal Flux Event Tricks*, live contributor experiments. Reverse-engineering evidence, not original developer documentation.
[^ct-tool]: OnemusCT, *Temporal Redux*, live maintainer repository. Editor implementation, known limitations disclosed.
[^cross]: Square Enix, *Chrono Cross: The Radical Dreamers Edition* product description. Product evidence only.
[^sos-press]: Sabotage Studio, *Sea of Stars press kit*, live official page. Feature/rendering claims, no private engine code.
[^sos-production]: Kyle Hilliard interviewing Thierry Boulanger, *Game Informer*, 30 July 2025. Direct developer testimony about production.
[^sdv-migration]: ConcernedApe, *Important Announcement for Modders*, 17 August 2021. Official developer announcement for the 1.5.5 migration.
[^sdv-content]: Pathoschild, *Content Patcher author guide*, live `develop` branch. Maintainer API/format documentation, not vanilla source.
[^mistria]: GameMaker showcase (historical engine association); NPC Studio, *New Engine Beta Branch*, 3 June 2026. The newer official migration notice governs the assessment.
[^tml-system]: tModLoader, *ModSystem Class Reference*, stable documentation accessed above. Maintainer SDK boundary.
[^tml-net]: tModLoader, *Basic Netcode*, live maintainer wiki. Mod/network lifecycle guidance.
[^df-history]: John Harris interviewing Tarn Adams, *Q&A: Dissecting the development of Dwarf Fortress*, Game Developer, 3 June 2019. Direct developer testimony; not current source code.
[^df-tech]: John Harris interviewing Tarn Adams, *How Tarn Adams upgraded and optimized Dwarf Fortress for its official Steam release*, Game Developer, 14 March 2023.
[^qud-paper]: Jason Grinblat and C. Brian Bucklew, *Subverting Historical Cause & Effect: Generation of Mythic Biographies in Caves of Qud*, FDG 2017, DOI 10.1145/3102071.3110574. Developer-authored paper.
[^qud-talks]: Jason Grinblat, GDC 2018 history slides; Brian Bucklew/Jason Grinblat, GDC 2019 village-generation session overview. The full 2019 talk was not reviewed.
[^rimworld]: Tynan Sylvester, *The Simulation Dream*, June 2013; GDC 2017 RimWorld session overview. Developer design reasoning.
[^osiris]: Larian, *Scripting: Introduction to Osiris*, official Baldur's Gate 3 toolkit documentation. Declared scripting semantics.
[^openmw]: OpenMW maintainers, *Lua scripting overview*, stable documentation. Reimplementation API; not original Bethesda architecture.
[^exult]: Exult/Seven Towers, *Usecode C scripts and execute_usecode_array*, live reference. Reimplementation and variant-specific scripting evidence.
[^ff4]: Square Enix, *Final Fantasy IV PC Patch update*, 9 November 2020. ATB product-history claim only.
[^mana]: Square Enix Secret of Mana official page and 2018 remake Steam description. Remake feature evidence; original SNES internals not inferred.
[^crystal]: Andrew Willman, developer-published *Crystal Project* Steam page. Gameplay/transparency/traversal claims.
[^crystal-tools]: Andrew Willman (`riverrunner_512`), developer AMA, 26 May 2023. Developer replies on engine, shaders, compressed terrain, authoring tools and mod patches; later thread replies are not used for these historical claims.
[^hd2d]: Daniel Kayser interviewing Acquire, Unreal Engine, 25 October 2019; Mike Williams interviewing Acquire/Square Enix, Unreal Engine, 22 August 2023. Primary developer interviews about Octopath I/II, used as lineage evidence rather than asserted Octopath 0/Elliot internals.
[^octopath]: Square Enix, *Octopath Traveler 0* product description, accessed version. Town/recruitment/combat feature evidence.
[^elliot]: Square Enix, *The Adventures of Elliot: The Millennium Tales* product description, accessed version. Exploration/action/history features, not engine internals.
[^rune]: Marvelous/XSEED, *Rune Factory 4 Special features*. Official gameplay evidence.
[^dqb]: Square Enix, *Dragon Quest Builders 2* description. Campaign and multiplayer-building scope.
[^minecraft]: Adrian Östergård/Mojang, *Minecraft Java Edition 1.18.2*, 28 February 2022. Version-specific generation extension evidence.
[^nms]: Innes McKendrick, *Continuous World Generation in No Man's Sky*, GDC 2017. Developer session abstract reviewed; full talk not reviewed.
[^kenshi]: Lo-Fi Games, developer AMA answers, 14 August 2019. Historical technology/modding constraints.
[^mount]: TaleWorlds, *What Makes a Hideout Scene*, live official authoring documentation.
[^ck]: Paradox, *Realm Maintenance 2026 Part II*, publisher's March 2026 diary archive. Story visibility evidence.
[^valheim]: Game Makers, Iron Gate developer interview at TGS 2025, published 2 October 2025. Limited engine-choice evidence; exact networking architecture remains unverified.
