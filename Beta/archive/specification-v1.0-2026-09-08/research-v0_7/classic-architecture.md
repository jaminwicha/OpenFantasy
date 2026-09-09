# Classic JRPG architecture for a persistent world

## Architectural conclusion

Final Fantasy IV/VI and Chrono Trigger support Beta's chosen direction: a compact, expressive event vocabulary; separate rules and presentation responsibilities; reusable character, equipment and ability data; and deliberate encounter choreography. They do **not** establish an architecture for autonomous, player-equivalent NPCs living across an indefinitely persistent multiplayer world. Beta should preserve the authoring spirit and presentation continuity while building its own durable authority underneath them.

This review refines the [v0.6 architecture synthesis](../architecture-synthesis-v0_6.md) and [earlier reference study](../reference-architecture-research-2026-09-08.md). The adopted engine-independent C# core, Godot presentation, on-field Wait encounters and full original OpenFantasy migration remain appropriate. The launch context is Windows, macOS and Linux with one to four simultaneous players per hosted world; mobile is a later supported phase. The recommendations below are contract refinements, not claims that implementation has passed them.

## Evidence and source availability

No officially released original Square source tree for these three games was identified. The publicly accessible `everything8215/ff4` and `ff6` projects describe themselves as disassemblies. FFVI's maintainer explicitly identifies its modern directory structure as a reconstruction of likely original organization and says the original assembler/linker is unknown. The FFVI event documentation also identifies incomplete reconstructed script coverage. These are substantial implementation clues, not Square's original named source files or a verified reproduction of its entire production environment.[^1][^2][^3]

Three distinct evidence classes govern this report:

| Evidence | What it can establish | Limit |
|---|---|---|
| Square-published developer interviews and original manual | Production practice, design intent, shipped user-facing rules | Recollections contain uncertainty; modern ports are not interchangeable with SNES versions |
| Maintainer-authored disassembly and annotations | Specific reconstructed routines, data accesses and control flow | Labels, organization and some explanations are modern interpretation |
| Public editor code | What the editor parses, represents and changes | Its Python/JavaScript architecture is not the commercial engine's architecture |

No game ROM, extracted scenario, sprite or proprietary asset was acquired or incorporated. The routines cited below were inspected as published text; no ROM reconstruction was built or executed. Publication on GitHub is not an assertion that all associated game material is reusable. Beta's implementation and content should remain original apart from its separately authorized OpenFantasy lineage.

## Event authoring: readable intentions over raw instructions

Square's July 2026 Tokita/Kitase interview provides unusually direct evidence about SNES production. Kitase recalls movement being authored numerically, then improved with readable directional terms. Tokita explains that a converter translated these words into the unchanged numeric format. They also describe manual ROM address allocation, accidental data overlap, and the slow conversion/hardware feedback loop. The value of the tools was reducing authoring friction, not increasing theoretical runtime generality.[^4]

The reconstructed FFIV `field/event.asm` shows an interpreter selecting event-script pointers, consuming commands and coordinating field updates. FFVI's documented vocabulary extends across conditions, subroutine calls, timers, object movement, dialogue, map changes, party changes, sound and battle invocation. Object scripts can run with different waiting behavior. This supports a concrete separation between a scene's composition and the engine operations that make each step work.[^5][^3]

**Beta refinement:** author scenes using typed operations such as `Approach`, `Face`, `Speak`, `OfferTrade`, `Board`, `StartEncounter` and `AwaitOutcome`. Compile them into validated scene definitions. Do not expose arbitrary memory writes or a universal string command bus. Every consequential operation submits a normal core command; camera movement, expressive idles and music cues consume the result.

A scene definition needs participant predicates, permitted substitutions, spatial requirements, interruption policies and bounded waits. A scene instance needs stable participant bindings, durable phase, committed choices and operation receipts. A farmer who is dead, occupied or hostile cannot silently be recreated to satisfy a cutscene. A replacement speaker is a deliberate branch with appropriate knowledge, not an actor-ID swap that transfers memories.

The useful homage is that a content author can stage a departure or confrontation without changing engine code. The modern improvement is that invalid references, contradictory reservations and unreachable phases produce actionable validation errors before release.

## Field, actors and battle: preserve the people through the transition

FFVI's reconstructed `field/battle.asm` contains an explicit `ExecBattle` adapter: preserve field context, enter the battle routine, restore context and request reloading of the same map. FFIV's event battle command likewise invokes battle and restores field presentation. These are concrete mode boundaries, not evidence of two independently advancing worlds.[^6][^5]

FFIV also reveals a particularly relevant trap. Its reconstructed solo-battle paths temporarily hide other party members, manipulate character records and refill selected HP/MP before designated dramatic fights. Subsequent code restores the party. Such bespoke intervention serves a bounded authored scene, but it conflicts with Beta's promise that NPCs and players retain the same persistent state and consequences.[^5]

**Beta refinement:** encounter membership references existing actor IDs. Encounter state may hold tactical readiness, reservations and decision progress; it cannot own a second inventory, body or relationship graph. Starting or finishing combat must not heal, recreate, reposition through obstacles, erase a passenger or refresh a weapon unless a declared rule causes that change. Changing presentation mode does not change simulation identity.

For Chrono Trigger, developer testimony describes experimenting with encounter presentation and hand-authoring more than one hundred individual battle lead-ins. The team explicitly connects visible enemy behavior to personality before combat. The same interview collection includes a more ambitious pre-release interactive event proposal and later testimony about cutting ideas after trying them. The proposal must not be mistaken for an entirely shipped freeform story engine.[^7]

**Beta refinement:** a generated location must provide a legal combat stage, or support a simpler valid staging pattern. An encounter grammar can combine approach, warning, ambush, negotiation failure and escalation with usable terrain. Do not promise one bespoke introduction per generated encounter. Ship a small, demonstrably varied vocabulary and reserve exceptional staging for authored moments.

## Wait timing: an explicit scheduler, not a stopped renderer

The original English FFVI manual, titled *Final Fantasy III*, describes Wait as stopping battle progression while choosing items or spells. This is a useful historical user contract, but it is narrower than Beta's adopted full required-decision hold.[^8] FFVI's reconstructed `UpdateBattleTime` reads player actions, checks a battle-stop/Wait condition, then advances counters against a frame counter and updates actor readiness. It also separates readiness from queuing actions and opening menus.[^9]

FFIV's reconstructed timer-effect routines separately handle states including stop, poison, petrification and doom; they dispatch effects and request graphics. This establishes that “battle time” includes more than choosing the next actor. An adaptation that freezes readiness but continues damaging statuses has changed the rule even if the menu looks familiar.[^10]

**Beta refinement:** use the core's deterministic scheduler, independent of Godot frame rate. Explicitly assign every duration to its domain: tactical action/phase progress, world calendar, or cosmetic time. Costs, cast preparation, periodic damage, concentration, summons, hunger, cargo decay and environmental exposure require declared behavior at encounter entry, hold, merge and exit. A cosmetic breathing animation must not advance poison or consume simulation RNG.

The classics do not resolve independent multiplayer surroundings, moving ships or an advancing political calendar during unlimited local Wait. Beta's bounded encounter policy remains a deliberate abstraction. Cross-boundary influence must be admitted before it becomes consequential, and connected hazards/resources need one accountable timing policy. A party on a moving vessel cannot simultaneously treat the deck as frozen and accept arbitrary real-time collisions. These rules must be proved with the complete host, not inferred from ATB's popularity.

## Data-driven powers and equipment, with checked composition

The reconstructed FFVI equipment path is concrete: `UpdateEquip` reads character properties and equipped items, then `CalcEquipEffect` combines field effects, status protection, elemental responses, stat modifications and weapon-triggered spells. The code also contains special-case branches. “Data-driven” therefore means reusable data interpreted by rules; it does not mean the original game had an unlimited compositional effect language.[^11]

FF6Tools exposes maps, battles, characters, monsters, items, attacks and scripts through JSON/YAML definition files. Its FFVI schema links spell-learning and monster action records to attack definitions. This corroborates the value of shared definitions and references, while remaining evidence about an editor's understanding of ROM data.[^12][^13]

**Beta refinement:** retain versioned power definitions with separate school, elemental facets, delivery, target predicates, costs, duration, stacking group, resistance rules and observable effects. A class grants eligibility or learning paths; species affinity modifies declared capabilities; neither hard-codes every spell combination. Player and NPC researchers submit the same validated composition process.

A fire-bound blade, lingering illusion and shipboard ward should share effect primitives where behavior actually matches. Keep semantic differences explicit: misleading perception differs from changing physical terrain; adding heat differs from applying a visual flame. Persist created spell definitions and their provenance so updating a recipe cannot retroactively change an already researched power. Validate bounded execution, recursive trigger limits, resource cycles, incompatible facets and deterministic ordering before accepting a new definition.

## Choreography and animation: concurrent performance needs failure semantics

Temporal Redux's `eventcommand.py` represents separate object and player function calls, priorities and continuation/synchronization modes. Its command catalogue describes waits until start or completion and potentially indefinite waits under some priority conditions. It also models exploration control, battle range, party coordinates and follow behavior. This is useful public editor evidence of a rich choreography surface, with unknown command details and version differences explicitly present.[^14]

**Beta refinement:** a performance coordinator owns cues and presentation reservations, while authoritative actions remain in the core. Specify whether a wait is for semantic completion, actor arrival, an animation marker or optional viewing time. A failed path, missing prop, interrupted speaker or absent viewer produces a declared alternate phase. Deterministic tie-breaking and bounded scheduling prevent two scenes from reserving the same actor forever.

Personality should affect how a valid action is performed: hesitation, gaze, stance, gesture, pace and response. It should not secretly change action legality through animation length. People, mounts, pets and manipulable objects need semantic action coverage with supported body/attachment variants. Two characters sharing an animation family should still differ coherently; arbitrary decorative variation does not substitute for meaningful behavior.

The art and engine teams therefore need a shared performance contract covering pivots, facing, attachment points, contact markers, allowed interruption and fallback poses. Neither historical bytecode nor a universal skeletal rig determines the right implementation. Retain authored pixel sequences where they produce better acting.

## Persistence: save facts and outcomes, not a nostalgic memory layout

FFVI's reconstructed save routines copy a bounded game-state region into SRAM, preserve timers and calculate/check a save-slot checksum. Its initialization code stores configuration, time and other defaults. This is a real durability mechanism for its bounded game, but a checksum is not a migration system, a transaction protocol or proof that arbitrary mid-scene state is recoverable.[^15][^16]

**Beta refinement:** checkpoint versioned canonical state and the ordered continuation needed after it. Persist actor identity, ownership, learned powers, environment mutations, relationships, reservations, scene phase, command receipts and RNG domains. Keep presentation caches rebuildable. A restart may reconstruct a pose or camera; it must not replay an item transfer, rebuild a destroyed bridge or forget a familiar's hunger.

Authored event flags may remain useful as namespaced arc facts. They should not become an opaque substitute for actual custody, death, belief or political control. “Bridge repaired” should normally be derived from the repaired bridge, with narrative observation tracked separately. A later sabotage then creates a comprehensible new situation instead of contradicting an immutable quest bit.

## Retained validation cases and production implications

These cases belong in the intended modules and content package, not a disposable demonstration. They are specified here, not executed.

| ID | Retained case | Falsifying observation |
|---|---|---|
| CLASSIC-01 | An original OpenFantasy NPC negotiates, boards a crafted vessel and enters an on-deck encounter; interrupt each phase and restart | Duplicate cargo, healed actor, lost passenger, stuck reservation or outcome without a valid action |
| CLASSIC-02 | Replay identical accepted commands headlessly and at varied rendering rates, with different cosmetic idle variants | Different authoritative hash, effect expiry, resource consumption or random outcome |
| CLASSIC-03 | An outsider attempts to heal, trade, damage terrain and move a connected vehicle while participants hold a decision | Any consequential influence bypasses the declared admission/timing contract |
| CLASSIC-04 | A player and NPC independently learn and use the same class-linked composed spell and enchanted item | Different prerequisites, unexplained permissions, free research, missing saved definition or divergent stacking |
| CLASSIC-05 | A scene's intended speaker dies or leaves; an NPC party completes its objective without the player | Scene resurrects/replaces the person, repeats rewards or suppresses the valid independent outcome |
| CLASSIC-06 | Artist and content tasks create a second encounter from existing staging/animation vocabulary | Engine-specific scene code is required for routine variation, or reusable staging produces visibly repetitive results |

Virtual playtesters can exercise legal commands, detect contradictions and compare causal traces. They cannot establish whether a transition feels elegant, whether a character's acting is emotionally convincing, or whether a scene is fun for people. Retain human observation and owner art acceptance alongside automated and critic-agent evaluation.

## Traps of overextending the homage

**Expressive presentation is not autonomous agency.** A convincing idle script does not establish beliefs, obligations or independent goals. Beta needs those systems because of its own scope, not because CT's event objects already supplied them.

**A small cast's authored exceptions do not scale automatically.** Fixed party slots, story-controlled health and guaranteed participant availability must become validated role bindings and legal actions. Conversely, player/NPC parity does not require giving every citizen identical wants or continuously evaluating every action every frame.

**Seamlessness is a production commitment.** Original Chrono testimony describes extensive handwork. Procedural staging, interactive props, species silhouettes and modular vehicles multiply compatibility cases. Prove repeatable production before expanding content count; do not use “the engine supports it” as proof that assets exist.

**Classic time travel is not arbitrary history editing.** The materials inspected do not establish conservation across freely branching live worlds. Beta's world/branch identity, material transfer and individual lineage rules need independent tests.

**Preservation does not require preserving defects.** Keep all original OpenFantasy content and meaningful concepts in the migration ledger. Preserve source behavior honestly, then explicitly reconcile behavior that conflicts with the successor's rules. A classic-inspired architecture succeeds when authors and players understand cause and consequence, rather than when modern code imitates cartridge constraints.

## Sources

All sources accessed 8 September 2026. Live repository branches are version-sensitive; routine names below identify the inspected surface. No claim of exhaustive source recovery or executed historical-game validation is made.

[^1]: everything8215, [*Final Fantasy IV Disassembly*, README](https://github.com/everything8215/ff4). Public reconstruction, `main`; version coverage and provenance.
[^2]: everything8215, [*Final Fantasy VI Disassembly*, README](https://github.com/everything8215/ff6). Public reconstruction, `main`; “Format and Organization” explicitly qualifies source organization.
[^3]: everything8215, [*FF6 Event Script*](https://github.com/everything8215/ff6/blob/main/src/event/README.md). Maintainer documentation; work-in-progress warning and event/object commands.
[^4]: Square Enix, [*Final Fantasy IV 35th Anniversary Celebration & Final Fantasy X 25th Anniversary Celebration Special Interview*](https://na.finalfantasy.com/news/2831), 18 July 2026. Tokita and Kitase, especially “The Development Environment of Final Fantasy IV”; first-person retrospective with acknowledged memory limits.
[^5]: everything8215, [FFIV `field/event.asm`](https://github.com/everything8215/ff4/blob/main/field/event.asm), `ExecEvent`, `EventCmd_ec`, `RestorePartyAfterSoloBattle`. Reconstructed code and annotations, not original symbol names.
[^6]: everything8215, [FFVI `src/field/battle.asm`](https://github.com/everything8215/ff6/blob/main/src/field/battle.asm), `ExecBattle`. Reconstructed mode-transition routine.
[^7]: Square developers, [*Chrono Trigger—1994/95 Developer Interviews*](https://shmuplations.com/chronotrigger/), English translation by shmuplations. Historical first-person testimony mediated by translation; distinguishes pre-release ATL proposal from post-release discussion. Original Japanese magazine pages were not independently verified here.
[^8]: Square/Nintendo, [*Final Fantasy III* SNES instruction manual](https://www.nintendo.com/es-es/games/oms/snes-classic/manuals/final-fantasy-3/manual.pdf), configuration and battle sections. Primary manual for the English SNES FFVI release.
[^9]: everything8215, [FFVI `src/battle/atb.asm`](https://github.com/everything8215/ff6/blob/main/src/battle/atb.asm), `UpdateBattleTime` and readiness/queue routines. Reconstructed code.
[^10]: everything8215, [FFIV `battle/timer.asm`](https://github.com/everything8215/ff4/blob/main/battle/timer.asm), `DoTimerEffect` and timer-effect handlers. Reconstructed code; not a complete audit of Wait behavior across FFIV ports.
[^11]: everything8215, [FFVI `src/battle/equip.asm`](https://github.com/everything8215/ff6/blob/main/src/battle/equip.asm), `UpdateEquip`, `CalcEquipEffect`. Reconstructed property evaluation.
[^12]: everything8215, [*FF6Tools*, README](https://github.com/everything8215/ff6tools). Editor implementation and maintainer documentation, `gh-pages`.
[^13]: everything8215, [FF6Tools `ff6/ff3u-sfc.json`](https://github.com/everything8215/ff6tools/blob/gh-pages/ff6/ff3u-sfc.json). Editor schema: spell-learning, monster actions and attack-definition references.
[^14]: OnemusCT and contributors, [Temporal Redux `sourcefiles/jetsoftime/eventcommand.py`](https://github.com/OnemusCT/temporal-redux/blob/main/sourcefiles/jetsoftime/eventcommand.py). Editor code: `FuncSync`, object/player calls, exploration, battle and movement commands. Unknown fields and SNES/PC differences limit inference.
[^15]: everything8215, [FFVI `src/menu/save.asm`](https://github.com/everything8215/ff6/blob/main/src/menu/save.asm), `CopyGameDataToSRAM`, `LoadSavedGame`, checksum routines. Reconstructed code.
[^16]: everything8215, [FFVI `src/menu/menu_sram.asm`](https://github.com/everything8215/ff6/blob/main/src/menu/menu_sram.asm), `CheckSRAM`, `InitSaveSlot`. Reconstructed code.
