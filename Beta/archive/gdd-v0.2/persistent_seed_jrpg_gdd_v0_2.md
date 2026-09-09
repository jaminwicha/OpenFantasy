# Persistent Seed JRPG Platform

Game Design Document & Systems Specification

**Version 0.2 — Consolidated requirements baseline**  
**8 September 2026**  
Working project title • Prepared for Astra implementation analysis

## Document control

This edition supersedes v0.1 as the consolidated design record for the discussion through the owner’s latest request to include all feedback. It preserves the full product ambition while distinguishing binding requirements, quality targets, proposed defaults and unresolved implementation decisions. It is not evidence that the game, engine or acceptance tests already exist.

**Source of truth:** the Markdown and DOCX editions contain the same substantive specification. The machine-readable requirements register mirrors Appendix A. The accompanying handoff brief points to, but does not replace, this document.

**Review performed:** compared all 41 prior sections with the full visible discussion; incorporated the latest town/dungeon, recruitment, job and social-affinity requirements; checked for embedded DOCX comments; corrected internal contradictions and unsupported guarantees; verified selected external reference descriptions against official sources.

## Reading guide

Read §§1–5 for the product and experience; §§6–24 for world/game systems; §§25–30 for combat, hosting, expansions, AI and generation; §§31–36 for presentation, platform and guardrails; §§37–41 for architecture, delivery evidence and implementation decisions. Appendix A is the requirement/feedback map; B contains integrated test stories; C shows conceptual contracts; D records the audit changes; E lists sources; F defines terminology.

**Central promise:** a living, history-bearing world with the depth to support adventurers, makers, households, settlements and civilizations—experienced through an original, polished, comprehensible classic-JRPG presentation.

## 1. Executive summary and product identity

**Product:** an original, commercially oriented, procedural fantasy RPG and world-creation platform. Its approachable surface is a beautiful sprite-based adventure in the visual tradition of Chrono Trigger, Secret of Mana, and Final Fantasy IV/VI. Underneath is an interconnected simulation intended to meet or exceed the breadth and meaningful depth of Dwarf Fortress across both individual adventure and fortress management, with crafting, world manipulation, and discovery at Terraria-class richness or beyond. These are quality ambitions to demonstrate, not claims already achieved.

The flagship product is not a remake, a ROM modification, a transplanted SNES engine, or a research-first decompilation project. Classic architectures are design references: data-driven actors, map/event separation, expressive combat rules, and compact presentation. Implementation must be independently designed. The work should exploit contemporary procedural tooling, static analysis, optional AI assistance, and a first-class package platform without requiring an AI service to play.

A generated server offers one deliberately paced **60–80-hour adventure horizon**, including normal side exploration, with bonus chapters, alternate paths, and deeper activities capable of extending that horizon toward roughly twice the baseline. Its world persists afterward. The campaign is a concentrated experience inside a much larger history-bearing sandbox, not a timer, mandatory grind, or a limit on building space.

Players explore, observe, understand, create, recruit, fight, negotiate, govern, and change history. Farming, survival, jobs, skills, crafting, monsters, economy, politics, construction, quests, and temporal travel must affect one another. Towns and dungeons can be both generated and player-built; people have family, clan, cultural, political, socioeconomic, professional, and personal motives. Players may remain adventurers or become craftspeople, merchants, farmers, dungeon architects, arena champions, rulers, or chronomancers.

Solo, player-hosted co-op, and dedicated hosting share one authoritative simulation. Official content and community expansions use the same sanctioned package system, configurable per server. Optional LLM adapters may propose parameters, evaluate generation, assist creators, or render dialogue, but cannot silently invent canonical truth. Portals, other planes, historical eras, and alternate timelines are native architectural concerns. A later community/distribution layer and eventual MMORPG phase build on these foundations; their delivery is not presumed to be a simple scaling exercise.

## 2. Design pillars and requirement interpretation

**Simulation and consequence.** Places, societies, individuals, objects, and environments have state and history beyond the camera. A consequence must be represented where it matters, not merely described in flavor text.

**Reproducibility.** Deterministic generation is identified by seed, pinned executable/rules/packages/configuration, and any accepted external decisions. Live replay additionally requires the ordered input and decision history. A seed alone does not reproduce an AI-assisted session or a played world.

**Interdisciplinary design.** Reuse shared concepts and typed domain contracts. Prioritize interactions that produce distinct decisions and understandable consequences; do not maximize coupling for its own sake.

**JRPG legibility and polish.** A player can follow a compelling adventure without managing every coefficient. Dense composition, strong characterization, expressive animation, clear combat, and memorable music remain release criteria.

**Creation and introspection.** Making is a normal player verb. Deeper analysis is a trainable, discoverable capability that can reveal real values, formulas, provenance, and causal traces—not permanently conceal them behind developer tools.

**First-class extension and optional augmentation.** The base game is built from packages on the same supported surfaces as expansions. AI providers are replaceable and optional; accepted proposals become inspectable structured data.

**Originality and commercial focus.** Deliver an independently created game, not a source reconstruction. Production decisions must connect to playable outcomes, measurable quality gates, and sustainable maintenance.

### Requirement language
- **MUST / requirement:** captures a stated product requirement or an architectural invariant required to support it. Changes require an explicit decision and traceability update.
- **TARGET:** a desired quality, duration, scale, or performance outcome requiring measurement. It is not established feasibility or a delivery guarantee.
- **PROPOSAL:** a recommended implementation/default introduced here or earlier by the assistant, not an owner-approved constraint.
- **OPEN / ADR:** a decision reserved for the implementation GDD and an architecture decision record, with options and validation evidence.

The full product scope remains captured even where delivery is staged. An architecture hook is not a completed feature. A vertical slice is not the release scope. Specific player counts, tile sizes, world areas, timeline policies, combat timing, and performance budgets below are proposals unless identified otherwise. Appendix A ties the discussion to the design; Appendix D records corrections to earlier assumptions.

## 3. Experience scope, campaign horizon, and release boundary

A **world** is a persistent simulation and its branches; a **server** is a host process/environment running it; a **campaign** is a bounded dramatic arc within it. These are separate identities. Worlds can move between solo and dedicated hosting without changing their history. A server can outlive a campaign and support multiple generations of characters.

| Experience layer | Design target | Interpretation |
|---|---|---|
| Main dramatic path | 40–50 hours | Proposed contribution to the owner’s total horizon; not a mandatory linear route. |
| Normal exploration, party arcs, building/crafting and side adventures | 20–30 additional hours | Included in a typical 60–80-hour first completion; activities can overlap. |
| Bonus chapters, alternate routes, planes, legendary sites | Roughly 80–120+ total hours | Extra polished opportunities, some mutually exclusive in one branch. |
| Deep systems, alternative careers and exhaustive exploration | Roughly 120–160+ total hours | Stretch horizon, not a promise of twice as much unique scripted narrative. |
| Persistent sandbox and creator/community play | Open-ended | No forced reset, resource wipe, or world death when credits roll. |

These are this project’s targets, not factual completion-time claims about Chrono Trigger or Final Fantasy VI. Measure them using representative players, not the sum of all mutually exclusive content or eight players’ combined person-hours. Co-op concurrency, skipping optional activities, and expertise will change individual completion times.

**V1 product scope:** solo and hosted play; procedural world/history; customizable actors; deep skills, magic, items and crafting; survival/farming/ecology; recruitment and social simulation; adventure and settlement/fortress/dungeon management; quests and dialogue; economic, political and military activity; integrated creation and introspection; sanctioned expansions; optional AI; portals, temporal progression and alternate histories. All must be represented in the release plan. Detailed implementations may vary in fidelity, but none may silently disappear merely because a prototype is smaller.

**Later platform horizon:** broad social discovery, managed hosting, distribution scale, creator economies, and true MMORPG operations. Package installation, local sharing, server compatibility, and permissions are foundational V1 capabilities; a global marketplace is not.

A main-story player must not be required to grind every profession, run a kingdom, or farm for dozens of hours. Specialists, trade, companions, and alternative solutions provide access. Conversely, a farmer, merchant, or builder is playing a legitimate game, not failing to follow the plot. Published play presets must disclose whether narrative threats wait at chapter boundaries or advance continuously (§18 and §26).

## 4. Reference studies and independent design lessons

This is a **design comparison**, not a claim that a complete hands-on study, benchmark, or source audit has already occurred. Verified feature statements below come from official developer/publisher descriptions, checked on 8 September 2026. The proposed lessons are our design interpretations. References appear in Appendix E.

| Reference | Supported observation or study lens | Application and guardrail |
|---|---|---|
| Chrono Trigger / Chrono Cross | Owner-selected benchmarks for JRPG essence and era/alternate-history play. | Study encounter readability, party relationships, chapter revelation, cooperative abilities and temporal puzzles. Create original rules, characters and expression; no engine/source parity requirement. |
| Final Fantasy IV / VI | Owner-selected benchmarks for dramatic party adventure, world journeys and presentation. | Study ensemble roles, escalation, world transformation and optional routes; do not inherit abrupt travel compression or assume a particular combat format. |
| Secret of Mana | Owner-selected benchmark for expressive pixel-world presence. | Study environmental readability and immediate spatial interaction; action combat remains a separate decision. |
| Crystal Project | Official description specifies nonlinear exploration, class/subclass builds, traversal, and visible turn/damage/probability information. [R01] | Combine destination-led exploration with discoverable systems. Its open information model challenges excessive stat secrecy: accessible combat explanations and optional analytical detail should coexist. |
| Sea of Stars | Official description includes seamless field battles, cooperative attacks, swimming/climbing/vaulting, and dynamic lighting. [R02] | Budget traversal and encounter staging as core systems. Do not assume visual polish emerges automatically from procedural assembly. |
| Octopath Traveler 0 | Official system descriptions include protagonist customization, recruitment, town building, job/skill systems and an eight-character battle party. [R03] | A recruit should be both a person and an actual resident/worker; combat jobs and civic professions must be distinct but connected. Eight combatants is not our fixed target. |
| The Adventures of Elliot: The Millennium Tales | Official site describes an action RPG with field exploration, opening paths and revealing world history. [R04] | Study tools as traversal affordances, landmarks and time-spanning revelation. Do not infer exact implementation or adopt its setting. |
| Terraria | Publisher description combines generated worlds, excavation, materials, equipment, machinery, city building and exploration. [R05] | The capability graph extends into terrain and production. Measure meaningful access/build diversity, not just item counts. |
| Stardew Valley | Official features connect farming, mining, combat, fishing, professions, community schedules, seasons and domestic relationships. [R06] | Make a satisfying local life, readable daily choices, and recurring people; do not require social behavior to reduce to gift arithmetic. |
| Fields of Mistria | Official site connects farming, magic, town restoration, villagers, seasons and ruin exploration. [R07] | Study community presentation and magic that benefits both daily work and adventure. Treat descriptions as advertised scope, not a completed-feature audit. |
| Rune Factory 4 Special | Publisher description combines crops, monster raising, equipment crafting, social life and dungeons. [R08] | Study one character participating naturally in domestic and adventure loops; avoid isolated progression currencies. |
| Dragon Quest Builders 2 | Publisher description links building, exploration, materials, crop production with villagers and cooperative construction. [R09] | Treat buildings as functional spaces residents use; distinguish narrative construction goals from unrestricted creative tools. |
| Dwarf Fortress, both modes | Bay 12 describes persistent history, outposts and adventurers, multilevel construction, institutions, recruitment, material detail and weather. [R10] | This is the depth benchmark across adventure and management. A settlement spreadsheet or generated lore paragraph alone is not parity. |

**Additional comparative backlog:** Minecraft and Valheim for hosted worlds and travel; RimWorld for incident/relationship storytelling; Caves of Qud for generated history and knowledge; Kenshi and Mount & Blade for personal-to-political progression; Crusader Kings for kinship and claims; Ultima VII, Morrowind and Baldur’s Gate 3 for systemic object/quest interaction; Starbound and No Man’s Sky for investigating repetition and scale tradeoffs. These are study questions, not blanket claims of success or failure. No particular game is labeled a failure on the basis of this document.

Each study should produce an original mechanic brief, a recorded observation, a transferable principle, a non-transferable context, a cross-system test, and an asset/IP exclusion note. Do not spend months reconstructing another game before testing our own loop.

## 5. Core Player Experience

The default experience should still feel like a beautiful, comprehensible JRPG. A player can simply explore, recruit companions, fight, complete quests, discover dungeons, learn abilities, find equipment, and follow a dramatic story. The deeper simulation exists to make those actions consequential, not to force spreadsheet play.

Core player loop:
Explore -> Observe -> Discover -> Understand -> Create -> Change World -> Encounter Consequences -> Explore.

Valid play identities include, but are not limited to: heroic adventurer, dungeoneer, monster hunter, farmer, alchemist, smith, engineer, merchant, caravan operator, sailor, arena fighter, settlement founder, ruler, courtier, spy, scholar, historian, naturalist, mage, portal specialist, chronomancer, dungeon architect, monster breeder, and creator/publisher.

No one identity should be mandatory. The campaign can call upon several, but player specialization and party composition should create multiple legitimate paths through problems.

### Onboarding and play rhythm
The first session should demonstrate the whole identity in miniature: encounter a local problem, explore a nearby place, learn something, craft or build a practical solution, recruit or help a person, and observe a consequence. Introduce deeper views when the player has a question to answer, rather than opening an enormous graph at character creation.

A short outing should alternate discovery, tension, decision, reward and return. Safe hubs support crafting, relationships, preparation and reflection. A long journey changes available knowledge and tools, not just enemy health values. Travel, gathering and repeated fights cannot be used to inflate the 60–80-hour target.

Proposed server presets share the same rules architecture: **Adventure** emphasizes curated pacing and manageable needs; **Living World** makes ongoing societal and survival pressure more prominent; **Creator** grants broader authoring authority. Presets configure simulation rules; they are not separate games or a replacement for integrated creation. Specialized careers remain viable in each, subject to disclosed restrictions.

## 6. World scale, topology, traversal, and usable building space

**Requirement:** combine JRPG density with enough continuous wilderness to farm, hunt, gather, engineer, found settlements, build dungeons, and undertake meaningful ship/airship journeys. A small decorative overworld is insufficient; a vast empty walking map is also insufficient.

Use one canonical geography with three presentations: **local detail** for immediate interactions; **regional wilderness** for continuous exploration and settlement catchments; **strategic travel** for long roads, seas, trade and wars. Buildings, rivers, weather fronts and vehicles must refer to the same world facts across views. A zoomed-out icon is not a second independent town or ship.

Earlier suggestions of a 100–200 km² “continent” mixed fictional geography and playable area. **Retire that figure as a requirement.** Select scale through travel-time, content-density and construction-capacity tests before converting it into kilometers. Likewise, 1–5 km² per region remains an unvalidated experiment, not a committed asset budget.

| Quantity | Proposed test envelope, not fixed release promise |
|---|---|
| Campaign geography | 5–8 differentiated regions; topology may be islands, river valleys, inland seas, mountains or multiple landmasses. |
| Settlements and dungeons | Explore 4–7 major cities, 10–20 smaller settlements and 15–25 major dungeons as content-budget hypotheses. Minor sites add variety, not mandatory completion checklists. |
| Routine home-base trip | Approximately 1–3 minutes to a common local activity; establish through feel tests. |
| Unfamiliar village/wilderness journey | Approximately 5–15 minutes of active play, with decisions and discoveries rather than uninterrupted walking. |
| Long sailing/airship travel | Several minutes of meaningful navigation on a new route, with an earned repeat-route convenience option. |
| Construction room | Support at least a farm, workshops, housing and a dungeon project near a home region without erasing all nearby adventure sites; test simultaneous co-op claims. |

The world must support walk/run, climbing, jumping or ledge traversal, swimming, ropes and equipment, mounts, carts, boats/ships, later airships, and magical access as installed capabilities. Foot travel maximizes access; mounts improve speed; cargo transport creates logistics; ships require water and suitable landings; airships need readable physical constraints. Power upgrades should create choices, not make earlier geography meaningless.

Strategic travel advances the same game clock and simulates supply, weather, route risk and events. Players may stop, investigate or materialize a local site where geography permits. Repeated known travel can be condensed while preserving costs and consequences; never require manual repetition solely to sustain the world-size illusion. Different player groups may occupy different regions concurrently.

**Buildable wilderness is productive space, not empty padding.** It contains habitat, resource flows, defensible positions, hydrology, archaeology and transport opportunities. Claim permissions, land use and local density prevent eight nearby settlements from consuming every quest approach. Roads and gates remain meaningful; terraforming can change them.

**Topology decision:** a finite planet cannot also be an infinite plane. The platform must support finite/wrapping worlds and practically unbounded region networks as explicit world-generator profiles. The default profile remains open. Global climate/civilization history can operate on a bounded coarse domain while new frontier domains connect through recorded boundary conditions. Lazy generation must not rewrite visited rivers, roads, historical claims or map evidence when a neighbor is first explored.

## 7. World genesis, eon-scale evolution, and historical causality

The seed produces a causal world, not just a terrain surface. Generation proceeds through cosmology/planet rules, geology and hydrology, climate, life and ecological niches, sapient populations, cultures and languages, institutions and settlements, historical individuals, conflict, artifacts, ruins, and the player-arrival era.

**Eons are explicitly supported as a time range.** Geological and biological prehistory may span millions of years through coarse stochastic/analytic stages; civilization history may span centuries to tens of thousands of years at a finer level; live action runs at interactive resolution. These are package-defined timescale bands, not a requirement to execute every year or creature as a tick. Do not reduce the requested eon-scale ambition to a fixed 5,000-year civilization simulation.

History leaves evidence: strata, buried construction, changed rivers, deforestation, roads, graves, abandoned farms, language divergence, surviving books, legal claims, lineages, scars, hoards and altered habitats. A history entry without any meaningful physical, ecological or social consequence is insufficient for a major event.

Sapience, body plan, culture, polity, faction and hostility are separate. Humans, orcs, minotaurs, medusas, giants, dragons, constructs, spirits and package-defined beings may rule, trade, teach, form families where appropriate to their biology, found institutions or destroy them. Do not equate species with moral alignment, civilization or attack-on-sight behavior. A medusa court needs infrastructure and customs compatible with its abilities; giant architecture must reflect scale; a minotaur maritime culture need not resemble a generic maze cult.

Example generated lineage: a giant kingdom builds mountain reservoirs; a succession war damages them; orc households settle the surviving terraces; a dragon takes the citadel; merchants pay tribute; a hero later kills the dragon; irrigation and territorial claims become disputed. The surviving walls, roads, rulers, farms, artifacts and beliefs must derive from those events. Historical actors persist with identities and provenance, not interchangeable encounter labels.

Prehistory becomes live history at player arrival. Player-founded towns, inventions, victories, crimes, treaties, families and imported temporal objects enter the same event model. Retired adventurers can become mentors, rulers, descendants’ ancestors or legends. The world may continue after an individual campaign or character ends.

Generation uses stable keyed random streams by domain, location/entity, epoch, rule and purpose. That prevents unrelated random draws from shifting results; it does **not** prevent a genuinely changed ecological rule from changing subsequent wars. Causally meaningful changes must propagate. A generation manifest pins all world-affecting rules, configurations and decisions.

## 8. Multi-resolution simulation and clock policy

Local actors use detailed movement, combat, inventory, jobs, relationships and environment state. Regions use demographic cohorts, production flows, trade, habitat populations, disease and politics. Polities use institutions, migrations, armies and treaties. Eon stages use climate/geology/evolution abstractions. These are distinct resolutions of one world, not independent random simulations that happen to share names.

### Promotion, aggregation and conservation
Every resolution transition has an explicit mapping contract. Preserve named individuals, important items, ownership, obligations, population totals, resource balances, injuries and critical history. Aggregate anonymous cohorts where their exact identities are not yet needed; materialize details deterministically inside the permitted cohort envelope. Once a person or artifact is established and observed, do not re-roll it on return.

Demotion summarizes routine activity without erasing significant facts. Account for consumption, mortality, migration and production exactly once. A town must not produce food at regional level and again through visible workers. A distant army and its local soldiers share one authoritative representation lease. Promotion must reconcile casualties, stockpiles and time before local interaction begins.

Exact local and coarse simulations need not yield identical microscopic outcomes. **Required guarantee:** repeatability under a pinned scheduling/resolution policy, plus conservation and declared approximation bounds. Stronger observer-independent outcomes are a research/engineering target for selected domains, not assumed. Player loading a region may not create profitable resource duplication or retroactively invalidate established history.

### Separate clocks
- **Host tick:** ordered simulation and command execution; distinct from rendered frames.
- **World/calendar time:** seasons, growth, travel, appointments and political processes.
- **Historical epoch time:** accelerated generation/catch-up stages using explicit rules.
- **Branch-local time:** a timeline’s historical head; cross-branch travel is a transaction.
- **Narrative pacing state:** chapter readiness, threats and optional deadlines; not an invisible second calendar.

A solo pause stops authoritative simulation. Proposed default for private hosted worlds: pause when empty; dedicated owners can opt into always-on progression with disclosed catch-up limits and protections. One player sleeping, inspecting a graph or taking a long voyage must not advance everyone else by days without consent. Group time acceleration requires a server policy and coordination.

Long absence, long travel, or century-scale advancement uses budgeted deterministic catch-up with a preview of consequential outcomes when appropriate. Avoid silently advancing a whole multiplayer world because one traveler visits its future. The relation between combat duration, day length, crop cycles and campaign seasons requires an early playtest decision, not an arbitrary realism constant.

## 9. Universal World Ontology

Avoid separate bespoke engines for farming, crafting, politics, quests, monsters, and war. Build a relatively small set of expressive primitives that can compose into those activities.

Candidate primitives: Entity, Property, Component, Material, Location, Relationship, Kinship, Knowledge, Belief, Ownership, Need, Goal, Role/Job, Institution, Resource, Action, Transformation, Effect, Status, Rule, Event, Obligation, Claim, Reputation, Culture, Language, Faction, Population, Habitat, Recipe/Process, Ability, Portal, Timeline, and Provenance.

The same Actor framework should serve player characters, companions, NPCs, sapient monsters, animals, bosses, historical heroes, rulers, and—where practical—large creatures. Differences are component composition and cognition, not separate hard-coded object classes.

The same Item/Material framework should represent mundane tools, food, weapons, relics, crafted components, trade goods, artifacts, magical objects, and historically important possessions.

### Boundaries rather than one giant object graph
Shared vocabulary does not mean every module can mutate every field. Each domain owns its state and exports typed queries, commands and consequences. Ecology owns populations; economy owns ledgers; society owns roles and claims; narrative owns interpretation and quest contracts; the common actor/material/knowledge identities link them.

Definitions are immutable, namespaced, versioned templates; **instances** carry mutable world state; **observations** are scoped knowledge about instances; **events** are canonical transitions; **views** are UI/AI projections. A legendary sword’s definition, unique instance, market valuation, rumored origin and rendered sprite are not the same record.

Examples of cross-domain contracts: HarvestCompleted exposes quantities and ownership; ShipmentLost cites cargo and cause; JobContractAccepted records parties and terms; TreatyChanged records authority and affected regions. Consumers react through validated commands at known execution stages. A generic event bus does not replace explicit semantics or prevent cycles by itself.

## 10. Characters, bodies, appearance, traits, and lifecycle

Character customization is a first-class requirement: appearance, body/species options, attributes, traits, backgrounds, affinities, equipment, skill builds and powers. The actor model also serves companions, citizens, animals, sapient monsters, historical figures and enemies through different components. Do not hard-code fixed protagonists as the only fully supported actors.

Appearance combines body plan, silhouette, face/hair or analogous features, palette, clothing, equipment and animation attachments. Cosmetic choices should not accidentally impose undisclosed stat penalties. Where anatomy changes gameplay—size, reach, aquatic movement, extra limbs—the creator shows the meaningful consequences. Asset compatibility is validated; arbitrary body types cannot be promised full animation support without suitable authored rigs/sprites.

The internal model can include strength, agility, endurance, intellect, perception, will and presence; HP and other resources; size/mass; senses; locomotion; resistances; accuracy/evasion; recovery; fatigue; injuries; social context; affiliations; memories; latent traits and derived coefficients. These names are original-design examples, not a mandated D&D stat array or an assertion about Chrono Trigger’s source.

**Tabletop breadth** means combat, exploration, social action, checks/opposed actions, preparation, equipment, conditions, rest/recovery, companions and creative problem solving all have consistent mechanics. It does not mean copying a particular Dungeons & Dragons edition, classes, spells or text. The ruleset must define calculation order, rounding, stacking, immunities, resource costs and explanatory outputs.

Default UI shows a compact character sheet and important conditions. Domain expertise and introspection may reveal progressively deeper numbers, mechanics and causal traces (§11). Security secrets, other players’ private information and unresolved random outcomes are not character stats to expose.

### Mortality, recovery and succession
Support downed/incapacitated states separately from death. Recovery, treatment, permanent injuries, resurrection and corpse/item retrieval are ruleset choices. Casual, adventure and harsh-survival presets may vary penalties without changing the persistence model. Death must have a comprehensible consequence and cannot strand the only means of completing the campaign without an intended failure/alternate continuation.

Worlds can outlive player characters. Support retirement and continuation through an apprentice, companion, descendant, reincarnation or a new adventurer where the rules permit. Inheritance, household assets, relationships and historical reputation are explicit transfers, not a blanket copy of all previous power. Co-op character ownership, reconnect control and offline vulnerability are server policies. Immortal beings and biological lifespan differences must coexist with long-duration history.

The rules catalogue must cover exploration/social checks, opposed tests and resistance or saving-test equivalents; stealth, perception and detection; light/vision and cover; movement modes and carrying/load; conditions, sustained effects and interruption; healing/rest and resource recovery; languages, tools, rituals, summons and transformations where the relevant packages provide them. Randomness/dice models, numeric ranges and stacking behavior remain original ruleset decisions. Social actions must respect agency and believable incentives rather than make every NPC controllable by one high stat.

## 11. Introspection and vast interdisciplinary progression graphs

Introspection is both a player-facing power and an architectural requirement for explainable rules. A novice can see a condition; an expert can learn its value; a specialist can inspect the contributing effects; a power player can inspect the applicable formula/graph and a trace of the last resolution. The displayed explanation must come from the calculation actually used, not an approximate tooltip maintained separately.

Keep **observation**, **inference**, **confirmed knowledge** and **hidden truth** distinct. An estimate should not masquerade as a precise fact. Expertise is domain-specific: smiths inspect material/process defects; naturalists inspect physiology and behavior; historians trace provenance; tacticians analyze action timing; mages inspect effect topology; chronomancers inspect branch history. Analysis can require tools, time, experiments, language knowledge or relationships. Basic readability, accessibility settings and essential feedback must never be gated behind grinding an analysis skill.

Players can maintain journals, bestiaries, maps, research notebooks and shareable findings. Knowledge trading creates professions, but server-authorized views prevent a downloaded content package from automatically revealing live enemy intentions, secret histories or another player’s private facts. Learned recipes or verified principles can unlock creation primitives. Admin debugging is separate from earned player insight.

### Distinct progression concepts
| Concept | Meaning | Example relationship |
|---|---|---|
| Attribute | General capability | Perception supports scouting and analysis. |
| Skill | Practiced competence | Metallurgy improves material evaluation and processing. |
| Ability | An executable action | A movement technique, a spell, negotiation or a craft operation. |
| Trait | Persistent or conditional rule modification | Night vision, affinity, flaw, aptitude or body property. |
| Knowledge | A fact, method or theory the actor knows | A lost alloy process or the location of a buried gate. |
| Combat job/archetype | A loadout or training tradition | A role emphasizing protection, healing or control. |
| Profession / social office | Work and authority in society | Smith, farmer, historian, captain, magistrate or court physician. |

The skill/crafting structure is a **typed graph with AND/OR and multi-input dependencies**, not necessarily a tree. Recipes may require several inputs, tools, skills and environmental conditions together. Cycles that represent practice/recycling are legitimate; circular prerequisites with no entry route are defects. Distinguish known, attainable, equipped and mastered nodes. Switching a combat job must not erase civic identity, memories or all general knowledge.

Expected domain crossings include Botany–Farming–Herbalism–Medicine–Alchemy–Chemistry–Metallurgy–Smithing–Engineering; History–Archaeology–Languages–Politics; Astronomy–Navigation–Calendars–Agriculture–Temporal Magic; Anatomy–Medicine–Monster Hunting–Spirit Arts. Many routes can teach overlapping concepts: apprenticeship, scholarship, practice, reverse engineering, observation, travel, barter or an alternate era.

Vastness is measured through viable build families and distinct transformations, not inflated node counts. Validate reachability, prerequisites, incompatible tags, cost loops and package references at compile/install time and again against generated worlds. UI must support search, domain filters, local neighborhoods, pinned goals, missing-requirement explanations and recommended next experiments. Do not render a million-node graph at once. Combat loadouts, action costs, specialization, equipment and opportunity costs keep a knowledgeable character from using every power simultaneously. Respec policy is an open ruleset decision; experimentation must not impose gratuitous punishment.

## 12. Ability, Magic, and Combination System

Abilities should be data-driven compositions rather than a fixed list of hard-coded spells. A generalized ability grammar should support source/element, form, targeting, delivery, triggers, costs, duration, effects, conditions, and modifiers.

Example dimensions: physical/fire/ice/lightning/poison/spirit/time/gravity/etc.; strike/projectile/beam/field/aura/summon/movement/counter; self/single/line/cone/radius/chain; homing/piercing/ricochet/split/persistent/life-steal/repeat/status/terrain interaction.

Combined abilities inspired by the general idea of cooperative techniques should emerge from requirements across multiple actors, skills, positions, affinities, timing, or equipment. The system should support two-person, three-person, and potentially larger coordinated actions without reproducing any existing game's named techniques or exact rules.

Abilities can have noncombat semantics. Ice can freeze water, force can move objects, fire can alter materials, growth can manipulate plants, portals can link spaces, and temporal abilities can operate on history. Combat capability and exploration capability should overlap heavily.

### Effect contract and safe composition
Every effect declares inputs, target selectors, spatial extent, costs, timing, affected properties, stacking/priority, duration, reversal or persistence behavior, and readable feedback. The host resolves validation, resource reservation, execution, consequences and release/cancellation in a documented order. Effects use a bounded expression language or sandbox, not arbitrary code injected through an item description.

Cooperative techniques require participant consent/readiness, range, eligible capabilities and a transactional cost reservation. Disconnects, interrupts, death or movement cannot charge one actor twice or let another receive a free completed technique. Environmental and construction effects use the same permissions and material rules as ordinary tools.

Open-ended magic does not mean unrestricted power. Enforce resource/cooldown/loadout limits, scope budgets, counters and forbidden recursive combinations. Splitting a projectile must not recursively split forever; summoning cannot escape population budgets; portal creation must cite a valid address and authority. Packages may change these policies but must publish compatibility and balance expectations.

## 13. Items, Materials, Crafting, and Manufacturing

Crafting richness should meet or exceed Terraria in breadth while being grounded in a deeper transformation model.

Avoid enumerating every item as an unrelated recipe. Represent production as materials + components + tools + energy + environment + knowledge + skill + process -> output.

Processes may include gathering, mining, cutting, grinding, smelting, alloying, forging, casting, weaving, sewing, fermenting, distilling, cooking, enchanting, assembling, growing, breeding, preserving, carving, printing, and magical transformation.

Item properties should emerge substantially from material, geometry, construction technique, component quality, craftsmanship, enchantment, wear, repair, maker identity, and historical use. An artifact can accumulate provenance and social value independently of combat stats.

Crafting graphs should feed exploration and vice versa: new resources enable new tools; tools enable new terrain access; new places reveal lost knowledge; knowledge unlocks processes; processes alter farming, trade, combat, settlement development, and infrastructure.

Player-facing crafting interfaces should be fictional and legible (forge, laboratory, kitchen, workshop, ritual space), while power users may unlock deeper parameterized views through introspection/creator capabilities.

### Production, matter and ownership
Resources come from deposits, ecology, harvest, salvage, trade or explicitly declared supernatural sources. Tools wear, goods spoil where appropriate, products can be repaired/recycled, and processes consume time, labor and energy. Conservation is enforced within a ruleset; magic can create matter only through an explicit priced rule, not an accidental recipe cycle.

Crafting orders reserve inputs and output capacity; workers fetch materials, use a suitable station, perform the process, and deliver outputs. Interruptions preserve accountable work-in-progress. Quality is derived from declared factors, with inspectable uncertainty; a visually identical item can carry different process history. Blueprints describe a plan, not free copies of the required goods.

Automation is ordinary machinery, worker contracts or magical devices. Its production still consumes inputs and participates in upkeep, ecology and economy. Support logistics priorities, shared storage permissions, batch work and delegated supply chains so late-game complexity need not mean repetitive clicking. Trade and recruitment provide alternative ways to acquire outputs for players who do not want to master every craft.

## 14. Survival, Weather, Seasons, Ecology, and Celestial Systems

Survival exists as a meaningful world interaction rather than a chore meter collection. Relevant factors can include hunger, water, fatigue, temperature, wetness, shelter, disease, food quality, terrain, injury, and equipment. The default UI should collapse complexity into readable conditions.

Weather is simulated and spatial. Fronts move across regions; storms can be visible on the strategic map; weather affects travel, crops, rivers, fires, ships, monsters, visibility, magic, disease, and warfare.

Celestial state may include star/planet properties, axial tilt, seasons, moons, phases, tides, eclipses, meteor showers, comets, and long-period cycles. Fantasy packages may attach magical significance to these states. Celestial conditions can therefore influence spell potency, migration, agriculture, prophecy, navigation, or historical crises.

Ecology uses populations, habitats, trophic relations, migration, reproduction, predation, disease, and adaptation at appropriate resolution. Players can produce indirect consequences: remove predators -> prey increase -> crops damaged -> food prices change -> migration/politics respond.

Biological and cultural evolution should occur over long timescales. Isolated populations can diverge physiologically, culturally, linguistically, magically, technologically, and socially.

### Living ecosystems and bounded environmental simulation
Creatures need habitat niches, food/energy sources, reproduction/mortality rules, territory, migration and responses to pressure. Plant/insect/herbivore/predator relationships are networks, not one fixed food pyramid. Sapient beings also have culture, goals and institutions (§16/§22). A “monster encounter” can be hunting, territorial defense, a social meeting, a raid or a historical threat.

Localized weather and celestial rules should produce observable patterns, not arbitrary attribute multipliers. The package specifies how moons, seasons, eclipses, tides or magical conjunctions affect particular processes. Physical plausibility is a design choice; an eclipse need not always cause a climate disaster. Forecasts and astronomical knowledge provide counterplay.

Represent water, smoke, heat, fire, flooding, light and potentially other fluids/fields to the fidelity needed for farming, building, dungeon hazards and combat. No full computational fluid dynamics promise is required. Specify conserved/approximated quantities, update resolution and stable thresholds. Player observation cannot create infinite water or extinguish a fire merely by unloading a region.

Prehistory may include adaptation, speciation, extinctions and cultural divergence. Live ecology needs bounded responses and recovery mechanisms. Test predator removal, invasive crops, disease and population collapse across many seeds. Interesting consequences are preferred to every server predictably starving before its first campaign ends.

## 15. Farming, Husbandry, Breeding, and Domestic Life

Farming is part of the world economy and ecology, not an isolated minigame. Plant definitions can include species, soil preferences, moisture, climate, seasons, growth stages, disease, genetics, yield, nutrition, alchemical properties, and market uses.

Animal/monster husbandry can include taming, breeding, training, selective traits, disease, labor roles, transport, food production, magical uses, and ecological risks.

Domestic play includes housing, workshops, storage, cooking, relationships, family/household state, hired workers, gardens, livestock, and local community ties. A player should be able to spend long periods living productively in one region without the game treating that as failure to engage with the 'real' game.

Support fishing/foraging, cooking/preservation, orchards, crop rotations or soil amendments where useful, irrigation, pollination, breeding, seasonal planting windows and trade in seeds. Plants and animals carry lineages, environmental tolerances and discoverable properties. Genetics can be abstracted but must be stable, understandable through expertise and connected to actual outcomes.

Livestock and trained monsters have needs and welfare, housing, task suitability and risks; recruitment/taming is not a free conversion of every boss into a farm machine. Domestication, training and breeding are distinct processes. Farming changes habitat, water demand, settlement supply and political leverage.

Nutrition and survival should create preparation choices and a useful cooking profession without requiring constant meter babysitting. Hired labor, irrigation and production orders allow farms to function while adventurers travel. Households, festivals, hospitality, friendship, optional romance and family life remain parts of a recognizable community rather than only production bonuses.

## 16. NPCs, recruitment, companions, jobs, and social affinities

NPCs are persistent people or beings, not collections of shop/quest buttons. Each may have a body and lifecycle, household, residence, inventory/property, work, skills, needs, memories, knowledge, preferences, relationships, obligations, culture, institutions and personal goals. Generated notable people receive additional narrative and presentation budgets; everyday residents still have coherent identities.

### Social model
**Filial and kinship ties:** parent/child, siblings, adopted family, spouse/partner, extended family, ancestors, dependents, inheritance, household membership and family duties. These are not interchangeable with faction membership.

**Tribal, clan and cultural ties:** shared descent narratives, upbringing, language, customs, homeland, ritual and historical alliances or grievances. Culture is not identical to body/species. Migration, adoption, conversion, intermarriage where supported, education and individual choices can alter affiliations.

**Political and ideological ties:** ruler, patron, institution, citizenship, party/faction, law, office, claims, loyalty, dissent, religious affiliation and preferred governance.

**Socioeconomic and professional ties:** class/status, wealth, debt, employment, trade interests, guilds, credentials, property, material security and mutual dependence.

**Personal affinities and aversions:** trust, affection, respect, fear, resentment, rivalry, attraction, gratitude, ambition and values. Store dimensions with provenance and context rather than one approval number. An agent may admire the player, fear the player’s patron and refuse a job that separates them from a dependent relative. Do not make membership alone prescribe behavior; history, institutions and personal experiences explain tendencies.

### Recruitment as a negotiated world transition
The same process supports party members, settlers, hired specialists, apprentices, retainers, soldiers, teachers, merchants, allies and suitable creatures. It evaluates interest, trust, pay/support, safety, status, housing, equipment, mobility, family obligations and institutional permission. Successful recruitment records terms, authority, residence/travel plans, job and economic changes. Replacing a smith in one village may solve the player’s problem while creating another village’s shortage.

Recruits can refuse, negotiate, resign, become injured, change loyalty or request help. Relationships are not bypassed simply by assigning a work slot. Some roles are voluntary employment; others are civic offices, military orders or companion agreements; they need different authority contracts. Player management must show why a task is accepted or blocked.

### Jobs and daily autonomy
Separate occupation, actual work orders, worksite, employer, qualifications, combat job and social office. Agents schedule work, travel, rest, meals, care, social contact, rituals and emergencies. Work reservation prevents two actors taking the same item or assigning one actor to two places simultaneously. Skill-based matching, priorities, pay, capacity and personality affect allocation. Management can be direct, delegated to a foreman/steward, or goal-based; autonomy continues outside a menu.

### Companions and party identity
Target a manageable memorable cast within each generated campaign; 6–10 major companions is a prototype hypothesis, not a fixed slot count. Give them distinctive aspirations, contradictions, relationships, recurring scenes, expressive presentation and consequences. A companion’s narrative role must agree with their generated kinship and history. They may also govern a workshop, teach or run a settlement when not adventuring.

Party control, leadership, shared inventory, companion ownership and simultaneous co-op parties require explicit policies. No NPC should appear in two parties or two workshops at once unless a declared magical rule creates a distinct entity. Conflicting recruitment attempts are resolved by the host and the NPC’s contract, not a client race.

## 17. Dialogue, Knowledge, Belief, and Memory

Dialogue should operate on the world model, not merely static branching trees. Conditions may examine what each participant knows, believes, wants, fears, owns, remembers, and is permitted to reveal.

Dialogue can transmit knowledge, alter relationships, establish obligations, negotiate trade, recruit, teach skills, trigger conflict, reveal quests, create rumors, lie, persuade, threaten, bribe, or modify institutional state.

NPC knowledge must be scoped. An NPC should not know facts simply because the server does. Rumors and misinformation are valid knowledge objects with provenance and confidence.

Optional AI dialogue packages may render natural language from structured state, but generated lines may not silently create canonical facts. New facts must pass typed validation/canonicalization before entering the world ledger.

Dialogue is an action protocol plus presentation. A line may express a claim, make an offer, communicate an observation or establish an obligation; only validated actions create transfers, treaties, jobs or quests. Persuasion uses the speaker’s actual leverage and the listener’s motives, with fictionally legible outcomes. Repeating a request cannot bypass refusal through random spam.

Knowledge records source, recipient, time/branch, certainty, permission and whether the statement is true, mistaken, deceptive or unknown to the speaker. Memory summarization preserves committed facts and significant emotional/relational consequences. Rumors travel through contact, institutions and media rather than omniscient broadcast.

Authored dialogue fragments and constrained grammars must support a complete no-LLM experience. Localization uses stable semantic keys, grammatical metadata and text expansion support. Optional AI rendering receives only authorized facts and must not reveal a secret simply because the server stores it. A journal can show conflicting testimony without falsely certifying it as truth.

## 18. Quests, generated casts, and the 60–80-hour campaign

A quest is a **world-state contract with narrative interpretation**: an issuer or motive, relevant participants/places, desired outcomes, allowed or prohibited consequences, evidence, rewards, deadlines where disclosed, failure states and alternative continuations. It is not limited to a kill counter. The system must recognize a supported repair, relocation, negotiation, craft, investigation, political settlement or temporal solution as well as combat.

Procedural campaign synthesis uses generated history and present tensions, then fits **authored dramatic grammars** to real entities. Writers supply scene structures, emotional beats, pacing patterns, dialogue pieces, puzzle families and performance direction. The generator supplies concrete histories, geography, people, conflicts, connections and branch choices. “Fully procedural” does not prohibit hand-created building blocks; it prohibits relying on one fixed disguised campaign as the whole product.

A pipeline selects candidate conflicts and casts, binds valid roles, generates a chapter dependency graph, fits geography/travel, allocates encounters and recovery periods, validates solvability, then evaluates coherence and emotional variety. Canonical facts already established may not be silently changed to rescue a weak plot. The synthesizer can schedule new plausible actions or select different actors; it cannot retroactively claim an observed dead ruler was alive all along.

### Dramatic architecture
A campaign needs an opening personal problem, expanding stakes, recurring relationships, discoveries that reframe history, changes in capability/geography, meaningful choices, a climax, an ending and aftermath. Not every seed should use the same “ancient evil and five artifacts” template with renamed nouns. Companions require visible goals, tension, development and payoff; villains need motives and resources grounded in the world. Include quiet/social episodes, wonder, humor and local stakes alongside major war or cosmological threats.

The time budget in §3 is a planning envelope. Maintain a production ledger for scenes, dungeons, bosses, travel, optional arcs and systems learning, correcting for overlap. Require human playtests across seeds; neither narrative graph size nor an LLM score proves a satisfying 60-hour story.

### Open play without incoherent urgency
PROPOSAL: flagship Adventure pacing advances key irreversible narrative threats at disclosed milestones or during explicitly timed operations, while ordinary ecology, trade and daily life continue. An optional Living World preset allows broader autonomous crises and fail-forward outcomes. These are visible rules; do not announce that a city dies tomorrow and then freeze that deadline for a year without explanation.

Players may assassinate a ruler, recruit an antagonist, destroy a gate, leave for farming, or change history. The story must rebind eligible roles, recognize alternate outcomes, create a successor conflict or offer a meaningful failed/changed ending. It need not invent a perfect bespoke cinematic for every possible action. Publish the supported response grammar; do not hide arbitrary invulnerability behind claims of total freedom.

Critical information and progression capabilities need resilient acquisition routes: another witness, archive, teacher, expedition, artifact or compatible craft. Redundancy must remain plausible and not spawn a duplicate legendary item simply to satisfy a quest flag. Generated optional failure is acceptable; silent permanent softlocks in the flagship campaign are not.

### Multiplayer narrative state
Maintain world/branch campaign facts, party decisions and each character’s knowledge separately. A late joiner gets a recap, accessible entry role and current objectives, not a reset of the world. Shared irreversible decisions follow a host-configured consent policy. Branch-specific quests display their timeline; taking an item from one branch does not satisfy a different branch’s contract unless explicitly allowed.

## 19. Dungeons and Dungeoneering

Dungeons must be full adventure spaces, not procedural combat corridors. They can contain inhabitants, factions, ecology, puzzles, traps, locks, verticality, secrets, environmental hazards, lore, resources, treasure, quests, bosses, alternate routes, construction history, and persistent changes.

Generation begins with purpose and history: who built this place, why, how it changed, who occupied it later, what damaged it, what is still sealed, and what lives there now. Spatial generation follows the semantic history.

Dungeoneering is itself a skill domain: navigation, cartography, traps, locks, climbing, illumination, archaeology, geology, monster knowledge, logistics, secret detection, and survival.

Players can also build dungeons. Dungeon construction should use the same primitives as generated dungeons: rooms, mechanisms, traps, doors, encounters, themes, access rules, ecology, rewards, and procedural grammars. Advanced creators may package dungeon-generation rules for other worlds.

### Construction and operation
Dungeon building includes excavation, multilevel rooms, corridors, supports, doors, locks, light, drainage, pressure plates, switches, triggers, traps, hazards, secret spaces, storage, lore, inhabitants and access routes. Use the same spatial/material/logic primitives as ruins and towns. A dungeon may be a home, mine, crypt, prison, laboratory, challenge course, monster habitat or contested fortress, not only a player-authored combat arena.

Separate a **blueprint** from an **occupied operational site**. In normal play, construction costs resources and labor; inhabitants require recruitment, breeding, summoning or migration; treasure requires a funding source; traps have ammunition/energy and maintenance. Creative/admin authority may bypass costs only in a disclosed rules profile. A player-built reward room cannot print infinite wealth into an economy server.

Creator tests analyze entrance/exit reachability, softlocks, trap reset behavior, escape routes, alternative solutions, resource budgets and creature pathing. A dungeon grammar includes purpose, room roles, connective rules, ecological needs, historical layers, encounter contracts and presentation constraints. It is exportable without copying the original world’s private identities or unique treasure instances.

Meaningful terraforming and tools can bypass designed routes. Recognize successful systemic solutions rather than placing invisible “quest walls.” Where some material, ward, title or ecological consequence limits modification, communicate the rule and possible counterplay. Temporal versions must share architectural lineage, with routes changing for recorded reasons (§24).

## 20. Town, fortress, and settlement management

**Requirement:** settlement play must approach the depth of the adventure simulation, not be a cosmetic town-building screen. Players can found, construct, inhabit, organize, supply, defend, govern and eventually abandon or transform camps, farms, villages, towns, ports, fortresses, underground communities and dungeon settlements. A settlement is a living site and institutions, not a prefab count.

### Physical construction and infrastructure
Support planning and direct placement, blueprints, terrain modification and excavation, multilevel structures, material suitability, accessible paths and stairs, storage, workshops, housing, defenses, water and sanitation abstractions, power/mechanical or magical services, farms, animal enclosures and roads. Structural support/collapse, flooding, smoke and fire should exist at declared useful fidelity. Cosmetic detail should not overwhelm collision, roof visibility or worker navigation.

A construction plan becomes accountable work: reserve land/permissions, estimate materials, reserve inputs, transport them, assign qualified labor, perform stages, validate access, and mark the structure usable. Partly completed buildings can obstruct, shelter or fail according to explicit rules. Save/load must preserve work-in-progress rather than refund and duplicate materials.

### Labor, logistics and production
Players can set priorities, stockpile filters, minimum/maximum stocks, workshop orders, transport routes, shifts, qualified roles and district responsibilities. Hired agents and autonomous residents choose or receive work within contracts. Tool scarcity, unsafe routes, missing materials, insufficient pay, illness or conflicting loyalties explain blocked tasks.

Support practical delegation: a steward can maintain food reserves; a foreman can staff a workshop; a quartermaster can prepare an expedition. Management views expose bottlenecks and causal traces, not only totals. Nearby visible labor and distant aggregate output must reconcile (§8). A player who leaves on a dungeon expedition should be able to rely on a functioning settlement without minute-by-minute micromanagement.

### People, institutions and governance
Households need food, shelter, safety, care, social life and appropriate services. Settlement institutions include markets, schools, libraries, temples, guilds, hospitals, taverns, courts, arenas and administrative offices. They create jobs, information flows, social mobility, obligations and identity, not just percentage buffs.

Migration responds to opportunity, safety, kinship, institutions, prejudice/tolerance, resources and reputation. Visitors, merchants, refugees, pilgrims and diplomats are distinct from permanent residents. Recruitment can move households or divide them with consequences. Authority to assign work, impose taxes, command troops or alter land is derived from ownership, consent, contracts or political office; ordinary party leadership is not absolute rule over every resident.

Factions, class interests and families can cooperate, bargain, strike, compete, secede or rebel. Include security, law, disaster response, siege supply, recovery and succession. Affinities and aversions (§16) must visibly affect these decisions.

### Adventure and management are views of one place
The player may walk through their workshop as a JRPG character, then use an in-world planning/ledger view to organize it. Switching view never creates a different economy or different citizens. Detail follows permission and expertise; beginner controls offer sensible delegation, and power players can inspect operations. Co-op roles can divide building, defense, supply and diplomacy without requiring everyone to open the management UI.

Settlements evolve after the campaign. They can prosper, be conquered, be abandoned, decay, become ruins, attract new inhabitants, or be revisited in another era. A later adventurer should find evidence of the earlier player’s choices. This continuity is a core acceptance scenario, not merely postgame flavor.

## 21. Economy, Trade, Mercantilism, and Logistics

Goods have production chains and movement. Prices respond to production, consumption, scarcity, danger, distance, transport capacity, policy, war, weather, reputation, quality, and information.

Players may trade as individuals, operate shops/workshops, run caravans, finance expeditions, establish trade routes, transport goods by ship, speculate on shortages, manage warehouses, create monopolies, or participate in institutions.

Economic play must feed other systems: clearing a mine changes ore supply; roads reduce transport risk; monsters interrupt trade; war increases demand; crop failure changes prices and migration; a new crafting technique changes labor and production; portals can radically alter logistics.

### Accounting and systemic constraints
Represent inventories, ownership, production, consumption, transport, money or barter, debts and contracts explicitly. Price formation uses a bounded ruleset reflecting scarcity, quality, information and local market access; prices cannot simply recurse into unstable global perfect-market calculations every frame. World-generation and live economies need the same accounting meanings even when update cadence differs.

Merchants need cargo capacity, capital or credit, routes, information, risks and customers. Shops and caravans can be player-owned or delegated. Institutions may tax, subsidize, embargo, insure or monopolize activities if the package supports them. Trade law and relationship effects connect mercantilism to courts and society. Loss, spoilage, upkeep and consumption are legitimate sinks; an exploit caused by duplicate accounting is not a sink-design problem.

Portal and time travel challenge scarcity. Imports therefore carry branch/world provenance and follow server rules for transfers, duties, quarantine, unique goods and ownership. Cross-server economies are not implicitly trusted in V1. Progression-critical goods need more than one attainable supply route or an explicit alternate campaign solution.

## 22. Factions, Court Intrigue, Politics, Law, and War

Political simulation arises from characters, institutions, claims, resources, cultures, relationships, and interests.

Individuals may possess titles, claims, loyalties, secrets, obligations, patrons, enemies, heirs, family ties, ideological commitments, and ambitions. Court play can involve persuasion, diplomacy, favors, marriage, patronage, bribery, blackmail, spying, legal action, succession, assassination, rebellion, and alliance.

Secret information is simply knowledge with restricted distribution and provenance, allowing one common knowledge model to support intrigue.

War should not become a completely separate strategy game. Soldiers remain actors; weapons remain items; armies aggregate actors into command structures with commanders, units, formations, supplies, morale, objectives, and territorial context. Resolution changes fidelity by scale.

A player can participate as individual fighter, squad leader, commander, ruler, spy, supplier, healer, engineer, or diplomat. Strategic war events can materialize into detailed JRPG battlefields and boss encounters when the player is present.

Treat political organization as data: clans, councils, monarchies, city leagues, theocracies, tributary networks and original institutions can emerge from compatible rules. Culture, faith, language and technology can travel independently of bloodline or species. Dynasties track claims and inheritance; knowledge institutions preserve or lose methods; religions and traditions affect goals and social cohesion without being merely combat bonuses.

War requires mobilization, recruitment, equipment, food, transport, intelligence, morale, command, terrain, casualty accounting, occupation and a termination process. Strategic formations may represent cohorts; materializing a battle transfers authority to local actors without duplicating troops or spoils. A hero’s local action can influence command or morale through an explicit consequence, not automatically decide every continental war.

Court intrigue uses evidence and beliefs as well as true facts. A forged claim can be believed; exposing it is an information/legitimacy action. Diplomacy, espionage, patronage, marriage or alliance, persuasion, blackmail and succession are supported fantasy gameplay roles with understandable leverage and outcomes. Legal and political consequences remain in-world rule systems, distinct from server moderation and administrator powers.

## 23. Coliseums, Tournaments, Competition, and Reputation

Combat systems should naturally support arenas and competitions: duels, party battles, survival formats, monster contests, magical competitions, mounted events, nonlethal bouts, and tournaments.

These can create rankings, fame, rivalries, sponsorship, gambling economies where legal, recruitment opportunities, rare techniques, political spectacle, and specialized builds. Players can eventually construct and operate arenas using the settlement/creation system.

Arena organizers can define entrant eligibility, team size, equipment/ability restrictions, victory conditions, nonlethal protection, stakes, scheduling and prize funding. Rankings and fame derive from accountable matches; self-farming staged fights cannot silently mint public prestige or money. Tournament events can recruit rivals, reveal techniques, affect patronage and become generated campaign beats.

Player-run coliseums are ordinary constructed sites with staff and logistics. In-world fantasy wagers, where included, use fictional currency and configurable content rules; real-money gambling is not part of the product requirement. Server moderation and age/content settings are separate decisions.

## 24. Portals, time travel, and alternate histories

**Requirement:** spatial/planar portals, time travel, and travel between alternate historical timelines participate in quests, puzzles, crafting, items, skills, settlement history and exploration. They are not just cutscene destinations or ideas deferred automatically to a distant sequel.

A world address identifies WorldID, BranchID, PlaneID, RegionID, coordinates/elevation and a **historical state or epoch reference**. A live branch has its own current tick/head. A time coordinate alone does not identify a reconstructible state; the host needs a retained checkpoint, event interval or a valid deterministic reconstruction contract.

### Three travel operations
Spatial/planar transit changes place or plane while preserving the relevant time policy. Temporal transit moves to a historical or prospective state of a lineage. Alternate-history transit moves between materialized branches that share a past and then diverge. Planes may have different calendars, physics, ecology or magic, declared through packages and transit rules.

### Proposed flagship causal model: preserve, then branch on intervention
The earlier “hybrid” suggestion was underspecified. **PROPOSAL:** preserve existing branches and create a child branch when a traveler materially changes a historical state. The untouched source history remains available; the changed branch evolves from the divergence. Visiting its later era shows the consequences of the intervention. This makes the requirement concrete while protecting another player’s ongoing world from retroactive deletion.

A mutable-single-history or fixed-self-consistent model can be a distinct server cosmology package, but implementing all models is not a prerequisite to the first release. Selecting the flagship policy remains an ADR. Native branch-aware addressing and persistence are requirements regardless of the default.

Example: an aqueduct is ruined in branch A. The party visits an earlier supported epoch, creates branch B, learns the lost process and repairs the aqueduct. Advancing B produces a functioning irrigation network and different settlement outcomes. Returning to A does not magically overwrite A; the party can bring knowledge/materials back, repair A in the present, or choose to continue in B. Quest text must state which outcome matters.

### Historical fidelity and storage
Record consequential historical events, retained world/region checkpoints and persistent identities. Coarse ancient history need not contain every footstep. A supported visit reconstructs declared detail consistently with committed facts, then makes it canonical for that branch. Do not promise pixel-exact travel to every instant of an eon without storing or reconstructing the necessary state.

Branches share immutable ancestry and store changes separately where practical. Branch materialization, future catch-up and local resolution transitions are budgeted jobs with canonical commit points. Branch count, active heads, retention and storage have server limits. Required quest branches must be retained; deleting one requires safe handling of inhabitants, active quests, property and travelers. No arbitrary branch merge is assumed: combining two contradictory histories is a designed operation with conflict semantics, not an ordinary save merge.

### Multiplayer and travel safety
One traveler cannot rewind all connected players. Groups can remain in different eras/branches; permissions govern creation, acceleration and abandonment. Long future jumps create/advance an authorized destination branch rather than consuming everyone’s live calendar. Reconnection, host failure and rollback must restore each traveler to exactly one valid state.

Transit is atomic: validate destination and authority; reserve costs and capacity; checkpoint departure; transfer traveler/inventory/control; acknowledge arrival; release reservations. Missing packages, impossible arrival terrain, inaccessible return paths or partial failures require a safe refusal or recovery point. Ordinary portals cannot execute arbitrary code or trust remote servers.

### Identity, duplication and economy
Separate a being/item’s ancestry identity from its branch-specific incarnation and its globally unique live instance. Encountering an alternate version is allowed; accidental duplicate execution or duplicate inventory ownership is not. Import policies specify whether historical copies are tradeable, count as equivalent unique quest objects, incur costs, or are restricted. Time travel must not enable an unintended infinite-gold or duplicate-legendary loop.

Knowledge crossing eras is a supported reward. Extinct seeds, ancient manufacturing, future materials, divergent teachers, altered genealogy and alternate characters can extend the skill/crafting graph. A bootstrap paradox or self-originating artifact may exist only if a cosmology explicitly permits it and the provenance model records that rule.

### Temporal/planar dungeon and campaign design
A site can be an intact palace, a war-damaged ruin, a fungal cavern or a thriving alternate capital, with physical continuity from shared history. Puzzles may use past access, later collapse, growth/aging, celestial gates, extinct ecology or alternate political access. Validate solvability across time, not only within each map. Signal the correct branch, era, causal stakes and return route.

Late mastery can create/configure portals with destination, conditions, costs, owner, permissions and stability. Progression may expand from local tools to world travel, planes, eras and causal branches, but the earlier hour-by-hour unlock schedule is not fixed. All temporal examples in this document are original illustrative designs, not copies of named quests or objects from reference games.

## 25. Combat, parties, and encounter semantics

Combat must preserve JRPG readability, dramatic animation, tactical choices and character/build identity while sharing the world’s actor, material, ability, environment and relationship rules. Visible encounters, meaningful targeting/positioning, statuses, preparation, cooperative techniques, environmental effects and original boss behaviors are required design goals.

**OPEN: timing and control model.** The conversation references both command/ATB-style games and action-oriented games. That does not decide between ATB, discrete turns, real-time action or a hybrid. PROPOSAL: compare a field-based command/timeline prototype with a simpler action prototype using the same actor/effect definitions. Choose based on solo/co-op clarity, build depth, encounter staging and input accessibility before content production. Do not build four complete combat engines to preserve indecision.

The rules pipeline must define legality and resource reservation; initiative/readiness; target selection, range, sight and cover; hit/avoidance or equivalent resolution; damage and defenses; statuses, interrupt/stagger and reactions; death/recovery; loot and witnesses; and an explainable combat record. Use fixed, versioned rounding/ordering conventions. Exact formulas remain original package rules.

Exploration and combat should transition without contradictory terrain, teleported resources or a different version of the same creature. Enemies may negotiate, flee, defend territory or surrender when appropriate. Boss patterns use the same effect framework with authored encounter constraints; special exceptions must be visible and inspectable rather than arbitrary immunity to all systemic solutions.

### Co-op time and ownership
A command menu cannot freeze the whole world for every player. If the chosen model pauses an encounter, specify its membership, admission boundary, local environmental progression and interaction with outside actors. Prevent pause-based invulnerability or cost-free production. An alternative continuous combat clock may offer queued commands and generous decision windows. This is an early implementation gate.

Player ownership of companions, simultaneous commands, readiness for combo techniques, disconnect behavior, loot allocation and difficulty scaling must be explicit. Do not scale all enemies invisibly so that growth never matters. World-level war uses aggregate cohorts until a battle is materialized; shared resource and casualty accounting ties arena fights, skirmishes and large battles to the same world (§22).

## 26. Hosting, persistence, identity, and replay

V1 runs offline/solo through a local authority, player-hosted co-op, or a dedicated server. **1–8 players is a planning proposal**, not a measured capacity. Solo and multiplayer use the same validated commands and simulation semantics; a local in-process transport is allowed. A client renders authorized state and requests actions; it never directly writes canonical inventory, geography, history or rewards.

A world can be copied/backed up and moved between local and dedicated hosting. Separate logical WorldID, campaign ID, branch IDs and server endpoint. Moving the save is not a new fictional universe; intentionally forking it is recorded. Default characters are world-bound; cross-server character/item imports are optional validated operations, not an implicit global economy.

### Identity and reproducibility levels
| Level | Required ingredients | Guarantee |
|---|---|---|
| Seeded genesis | Seed; pinned engine/simulation ABI; package content hashes; generator/rules configuration; deterministic schedule policy | Reproduce the generated baseline for that profile. |
| AI-assisted genesis | All of the above plus accepted generation-decision artifacts/content snapshot | Reproduce accepted content without calling an AI provider again. |
| Live branch replay | Starting checkpoint; ordered player/admin inputs; committed external/AI decisions; relevant scheduling policy and versioned rules | Reconstruct the declared authoritative state, subject to the supported replay horizon. |
| World copy/fork | Snapshot plus required packages and manifest, with fork metadata if branched | Continue from the copied state; not a promise of identical subsequent play. |

Package version strings alone are insufficient; pin bytes/hashes, dependencies and effective per-server settings. Cache/version information that affects simulation must be part of identity. Cosmetic client preferences need not alter world identity unless they influence gameplay. Keep fingerprints comparable without leaking private server credentials or story spoilers.

### Save model and recoverability
Use periodic checkpoints plus structured consequential history and sufficient replay/journal data; full event sourcing of every animation is not required. Persist actors, aggregates, terrain edits, constructions/work orders, items, economy, quests, knowledge, permissions, branches, package state and accepted AI outputs. Save atomically, version schemas, and support backup, corruption detection and migration dry runs. Pruning storage may reduce replay detail but must not destroy canonical history required by time travel or an active quest.

Transactions cover crafting, trading, recruitment, construction reservations, loot, portal transfers and timeline operations. Idempotent command identifiers prevent reconnect retries from duplicating effects. Network loss does not authorize clients to invent outcomes. Join-in-progress receives a snapshot and ordered continuation appropriate to its visibility scope.

Server owners configure pause/empty-server behavior, sleep/time acceleration, death penalties, difficulty, creation permissions, PvP, survival pressure, content packages and AI budgets. Critical gameplay settings are disclosed and hashed. Ordinary solo play requires no account or cloud inference under the proposed offline-first product policy. Dedicated hosting needs documented save export/import, admin logs and a predictable upgrade process.

Future MMO development benefits from these boundaries but may still require major changes to partitioning, replication, ownership transfers and operational controls. We do not promise a rewrite-free path to one globally mutable multiverse.

## 27. Sanctioned packages and per-server customization

**Requirement:** content, rules, world generators, skills, crafts, creatures, cultures, societies, narrative, history, UI, art, audio, creator tools and AI integrations have supported expansion surfaces. Official developers must use those surfaces for ordinary content. The base game is a curated first-party package set, not a privileged monolith with mods bolted onto it.

The kernel owns necessary trusted mechanisms: deterministic execution/scheduling, identities, serialization, transactions, package loading, capability checks, networking, spatial/time addressing and presentation host interfaces. Game semantics live above it where feasible. “Every aspect extensible” does not mean community code may replace trust enforcement, break memory safety, bypass permissions or guarantee new engine capabilities forever without a runtime update.

### Package contract
A manifest declares namespaced identity/version, content hashes, authorship/license, compatible runtime/API versions, dependencies, optional capabilities, configurations and defaults, provided definitions/services, assets, migration hooks, execution budgets and tests/evals. Distinguish data-only definitions, sandboxed simulation logic, generation extensions, client presentation, creator tools and host-brokered AI adapters. Categories may overlap; authority remains explicit.

Avoid fixed enums for species, elements, damage types, slots, professions or planes. Use typed registries and discoverable capabilities. Validate that new anatomy has required presentation/navigation support and that a new effect has resolvers, explanations and allowed interactions. Declarative extensibility is not a substitute for specifying what a new concept does.

A server’s configuration overlay is separate from the immutable published package. Overrides are schema-validated, inspectable, hashed and exportable as a curated server profile. Creators can distribute a package, a configuration preset or a compatible collection without secretly modifying upstream bytes. Optional integrations depend on capabilities and contracts, not fragile string matching of another author’s private fields.

### Compatibility and deterministic execution
Resolve dependencies before world start. Specify command/effect stages, ordering and conflict policy; never depend on nondeterministic file enumeration or callback registration order. Two modifiers affecting the same property need declared composition, priority or an explicit conflict. Test cycles and runaway cascades. “Subscribe to an event” does not guarantee compatibility.

### Installation and lifecycle
Genesis-only changes require a new/forked world. Forward-only changes enter at a recorded installation time. Existing-world enrichment requires a migration/materialization plan that does not contradict observed history. Cosmetic updates can be separate. An installed crop can arrive through trade; a new empire cannot silently have occupied a visited town for centuries.

Before upgrade/remove: analyze affected instances, saved definitions, quests, branches and dependencies; create backup; run migration in a copy; validate; commit or roll back. Removed package content needs a safe placeholder/quarantine or a supported conversion, not null references. Removing a core dependency may be refused. Server authors must be able to pin old working versions.

### Joining and distribution
The server advertises required hashes, compatibility, sizes and requested capabilities. A client obtains packages from configured trusted sources, verifies integrity and licensing metadata, obtains appropriate consent, then joins. A cryptographic signature proves provenance, not harmlessness. Assets need decode/resource limits too. No routine instruction to download arbitrary native DLLs from a chat server.

Custom packages may invalidate flagship balance, campaign-duration or narrative-quality guarantees. Label profiles as first-party tested, community-tested or experimental with actual evidence. Sanctioned means a supported installation model, not endorsement of every package’s quality or safety.

## 28. Creation as Gameplay and Creator Tooling

There should be no hard conceptual boundary between play, making, and sanctioned modding.

Ordinary players create through fictional interfaces: forge, farm, laboratory, workshop, ritual space, construction tools, conversation, settlement institutions, breeding, cartography, portal devices.

Advanced creators may inspect and manipulate structured definitions and graphs for abilities, items, NPCs, quests, dialogue, dungeons, generation rules, cultures, biomes, historical events, and UI.

World designers gain access to broader rule composition and generation grammars. Package developers work against a sanctioned sandboxed API. Ordinary content creation must not require direct kernel modification; that is reserved for engine development.

In-game creations should be exportable where appropriate: a spell, item schema, crop lineage, dungeon grammar, settlement template, creature behavior, or quest framework can become a shareable package definition.

Creator permissions are server/world scoped. Hosts can control exploration, construction, terrain modification, item/ability publishing, rule editing, timeline operations, admin powers, and package installation.

### One authoring representation, different authority
The game, visual tools and package SDK produce the same normalized definitions and plans. The player’s in-world action still checks learned capability, resources, labor, ownership and permissions. A world designer may have broader editing authority; an admin can waive costs deliberately. Do not mistake equivalent data representations for equivalent permissions.

Creation progresses from materials and tools through items, abilities, mechanisms, buildings, institutions, habitats and portals. Workbenches can expose richer controls as expertise develops. Provide previews, cost/impact estimates, safe local testing, reversible edits where feasible, and explicit warnings for world-affecting changes.

Export distinguishes a reusable blueprint/template from a unique live instance, its inventory and its history. Redact private NPC/player data, substitute portable references, preserve attribution/licenses and declare dependencies. A blueprint shared from a rich server does not bring free currency or secret story state to another. Community publishing can later add discovery and commerce without changing this representation.

## 29. Optional AI/LLM integration, evaluation, and canonicalization

**Requirement:** server-configurable, replaceable AI providers can drive proposed parameters and evaluate procedural generation, with optional narrative/NPC/creator assistance. The game must remain complete and playable with no LLM. “Astra” here names the intended downstream analysis/implementation workflow; it is not a hard-coded runtime model dependency or a claim about any model’s guaranteed capability.

Server profiles specify provider/model adapters, local or remote execution, roles, prompts/templates, allowed data, output schemas, budgets, timeouts, fallback policies, caching, consent/disclosure and logging. Different entities or workloads can use different policies. Credentials stay in a trusted host broker, never in distributed packages or world exports.

### Roles and boundaries
AI may propose world-generation parameter sets; compare candidate histories/cultures/settlements/dungeons; critique quests, visual composition and character arcs; suggest NPC plans; render authorized dialogue; summarize history; help author structured packages; propose adversarial tests; and assist later moderation. Each is a capability with limited authority. Real-time core combat and essential actions must not block on an external request.

The pipeline is **generate/propose → deterministic schema and semantic validation → hard constraints/tests → optional quality critique → authorized selection → canonical commit**. An LLM score can inform a choice, not prove solvability, originality, economic balance, safety or emotional quality. Automated simulation tests and human review remain necessary. An AI’s explanation of a bug must link to a reproducible scenario before being accepted as established behavior.

### Reproducibility correction
Deterministic candidates plus a deterministic selector do **not** make the result reproducible when the selector’s scores come from a nondeterministic model. Pin accepted score/choice artifacts and generated content, with hashes and provenance. Provider/model names, prompts, low temperature or a provider seed alone are not this product’s replay guarantee. Replaying a canonical world reads the recorded decision, not a new model response.

AI-off genesis may produce a different baseline from AI-assisted genesis. An existing world containing already accepted AI-authored content must remain playable after the provider is disabled; unresolved optional requests fall back to deterministic authored behavior. These are distinct guarantees and must be tested separately.

### Truth, testimony and planning
World truth, character belief and generated wording are separate. A proposed secret treaty must be a legal world transition or a valid generation-stage fact, not a line of dialogue that silently rewrites history. A lying NPC may assert a false treaty, but the knowledge system records that as testimony, not a true treaty. AI planners submit legal actions; simulation rules and NPC knowledge determine available choices.

Treat player dialogue, books, package descriptions and retrieved lore as untrusted model input. They cannot override system authority, expose other players’ secrets, request arbitrary files, or invoke privileged server tools. Validate outputs by schema, references, knowledge scope, costs and effect permissions. Keep the authority boundary outside the model prompt.

### Evals and creator workflows
Packages may ship example worlds, expected invariants, test scenarios, regression seeds, prompt/evaluator versions and failure cases. Build deterministic balancing bots and property tests for economic/ability loops; use AI to suggest additional cases rather than act as the only judge. Budgets and degradation behavior must be observable to the host.

A creator can express an intent in language, receive a structured proposal, inspect/edit it, test it in a sandbox and publish through the same normal package pipeline. AI assistance never removes provenance/license review, permission checks or compatibility requirements. Generated art/audio, when used at all, must pass the same style, rights and quality gates as other assets; they are not mandatory for procedural play.

## 30. Procedural generation, static analysis, and quality control

Generation is structured synthesis over authored primitives, simulated history and explicit constraints—not random substitution of nouns. Every major generated site, character, artifact and quest retains enough provenance to explain its existence. Every accepted world includes a feasible campaign and useful space for independent lives and construction.

### Generation passes
1. Validate the package/configuration profile and select topology/cosmology.
2. Generate coarse geography, climate, ecology and historical evolution.
3. Select a playable era/region without rewriting committed history.
4. Bind campaign tensions, cast and dramatic grammars to existing world facts.
5. Materialize settlements, wilderness, routes and historical dungeon layers.
6. Solve progression/reachability and allocate resources, teachers, clues and alternatives.
7. Validate social/economic/ecological viability and causal consistency.
8. Evaluate encounter/traversal variety, pacing, art composition and narrative presentation.
9. Commit the manifest, accepted decisions, genesis state and quality report.

The passes may iterate through bounded retries with stable attempt identifiers. Report a failed configuration clearly instead of looping indefinitely. A curated grammar fallback may be used if disclosed and compatible with the seed’s deterministic rules; it must still generate the world rather than silently substitute a fixed campaign. Discovering a region later cannot retroactively change established geography or solve earlier contradictions by re-rolling facts.

### Static architecture and content analysis
The requested commercial, statically analyzed approach means compiling definitions/rules before play wherever practical: resolve symbols and dependencies; type-check effect/quest contracts; check skill/recipe reachability; reject impossible AND prerequisites; analyze declared read/write capabilities; detect dependency cycles; validate migration signatures; check localization/asset coverage; and bound recursion, instruction use and event fan-out.

Some properties are dynamic or too complex to prove statically. Use scenario simulation and property-based testing for economic feedback, ecology, agency and multi-step magic. Static validation cannot guarantee compelling stories or the absence of all emergent exploits. The GDD does not claim a verified reconstruction of Chrono Trigger’s implementation.

### Content quality and failure handling
Use machine checks for objective invariants: connectivity, necessary keys, accessible teachers, viable early supplies, legal spawn/landing points, recoverable quests, grammar bindings and consistent timelines. Use human rubrics and optional model critics for characterization, novelty, dramatic rhythm, visual readability, atmosphere and thematic coherence. Compare to recorded reference observations, not vague “JRPG quality” scores alone.

Release quality is demonstrated across a representative seed/configuration matrix and sampled full campaigns. It is not guaranteed for every arbitrary third-party package combination. First-party supported profiles have explicit test coverage; experimental profiles disclose their limits. Track repeated room/quest motifs and semantically equivalent items so combinatorial quantity is not confused with meaningful variety.

## 31. Visual direction, character customization, and presentation budget

The required visual identity is expressive, readable 16-bit-inspired sprite fantasy, with Chrono Trigger and Secret of Mana as central artistic references and Final Fantasy IV/VI informing ensemble adventure and world presentation. Produce an original setting, characters, architecture, interface, effects and composition language. Do not copy sprites, portraits, map arrangements, distinctive character silhouettes or recognizable asset sets.

Local scenes should prioritize clear silhouettes, restrained readable pixel density, expressive animation, rich environment tiles, meaningful elevation, strong focal points and dramatic but legible spell staging. A 2D or controlled 2.5D renderer is acceptable. Camera, base resolution, tile size, sprite proportions, animation directions, portrait usage, lighting and scaling rules remain art-bible decisions, not silently fixed by a reference game.

**Procedural does not mean stylistically uncurated.** Artists create coherent modular architecture, biome palettes, props, character/body templates, equipment layers, effect primitives, portrait components and animation vocabularies. Generation assembles them under composition and readability constraints. New species/cultures must either provide compatible art or use clearly declared fallbacks. Continuous arbitrary shapes cannot promise the same bespoke polish as supported body families.

Character customization needs a tested matrix: body family × clothing/equipment attachment × movement/combat pose × interaction animation. Limit unsupported intersections visibly rather than shipping broken arms, unreadable weapons or clipping. Cultural variation should affect architecture, dress, crafts and institutions coherently, without making each generated civilization require an entirely new art library.

Towns and fortresses need roof cutaways, indoor visibility, multilevel navigation, build previews, material feedback and clear ownership overlays. Dungeons need reliable hazard/secret cues without erasing discovery. Strategic maps show real weather, routes, armies and landmarks at a readable abstraction, not every simulated person.

The production art ledger tracks **authored families and coverage**, not the number of possible generated worlds. Unique bosses, companion moments and campaign climaxes require explicit animation/staging budgets. AI-generated assets are optional production inputs, not the default solution to an unbounded art requirement.

### Interface, accessibility and information design
Design both controller-friendly and mouse/keyboard-friendly interfaces, with remapping, adjustable text/UI scale, subtitles and non-color-only state cues. Provide reduced motion/flashing, readable target selection, comprehensible terrain height/occlusion and alternatives to precision timing where the chosen combat permits. Accessibility settings are never earned through character progression.

The principal views are adventure HUD, inventory/equipment, abilities/loadouts, filtered skill/crafting graph, journal/bestiary/research, settlement orders, construction planning, social/kinship relations, strategic travel and a timeline/portal atlas. All display the same authorized state. Keep novice defaults compact, expose optional expert detail, and explain costs or blocked actions before commitment. Irreversible construction, political and temporal actions need clear consequences and the applicable permission/consent flow.

## 32. Audio and Music

Music should have strong thematic identity, memorable location/character motifs, dramatic boss and chapter scoring, and adaptive layering where useful. Procedural worlds do not imply generic procedural music. Official content should ship with authored composition and curated motif grammars.

Packages may add music, ambience, instruments, biome soundscapes, battle layers, culture-specific musical vocabularies, or procedural/adaptive rules through sanctioned interfaces.

Map semantic states to authored music/ambience: culture, biome, settlement, personal motif, threat, combat phase, travel and era. A settlement’s motif may survive into a ruin or altered timeline, with curated variation to convey continuity. The audio system must respond to real state without abruptly changing tracks whenever a minor variable changes.

Packages supply licensed stems, themes, sound effects and mapping rules with metadata for duration, transitions, intensity and accessibility. Define concurrency/mixing budgets and deterministic event cues while allowing non-authoritative rendering variation. Captions, volume controls and alternative cues keep important hazards accessible without audio. Fully voiced generated dialogue is not a baseline requirement.

## 33. Social/community, distribution, and later MMORPG foundation

Server configurations are curated compositions of rules, packages, seed/history, permissions, play style and AI policy. These identities provide the basis for discovery, shared seeds, world exports/forks, package collections, creator attribution, documentation and community support. A server’s profile must distinguish world-gen configuration from live administrative changes and from cosmetic client choices.

V1 needs portable world saves, package installation/verification, direct/private hosted play, basic administration and sharing of eligible creations. Server directories, social accounts, subscription/hosting services, package ratings, broad catalog search and marketplace mechanisms may be later releases. Keep interfaces ready without making ordinary solo play depend on a global account or registry service.

Future distribution should support dependency resolution, cached/offline packages, content maturity settings, abuse reporting, takedown/quarantine workflows, compatibility evidence and creator attribution. A published creation carries license and provenance, not ownership of the originating world’s private people or data. Monetization, revenue sharing and marketplace governance require separate business/legal decisions; they are not settled here.

**MMORPG is a later ambition, not a V1 synonym for a persistent server.** Larger populations require authority partitioning, spatial/branch sharding, account services, durable operations, economy trust, moderation and adversarial security. Rules packages may be reused, but existing save/data schemas and mutable-timeline semantics may need substantial redesign. Inter-server portals and character/item transfer require an explicit trust and transaction protocol; they are not automatically enabled by sharing a WorldAddress type.

## 34. Security, privacy, package sandboxing, and trust

The authoritative host validates actions and limits what each client or package can observe. Player introspection reveals authorized game mechanics and knowledge, not server secrets, credentials, unexplored-state dumps or unrestricted access to other players. Admin/debug views have distinct roles and auditable access.

Normal community packages execute through constrained data and/or a sandbox with explicit capabilities. Default-deny arbitrary filesystem/network access, process execution, native memory, nondeterministic wall-clock access and secret-state queries. A trusted host broker may call an AI provider under owner policy; the package cannot make arbitrary HTTP requests on its own.

Budget memory, CPU/instructions, recursion, event fan-out, actor creation, assets, texture dimensions, decompression, storage and network payloads. A signed package is still untrusted input. Validate dependencies, archive paths, decoded media, schema versions and migration behavior. Establish a supported API version policy, revocation/quarantine path and diagnostic logs. Native engine extensions, if ever offered, are a separate high-trust tier outside ordinary server auto-installation.

Data scopes apply to knowledge, quests, claims, inventories, chat, telemetry and AI requests. Server owners disclose external AI use and retention; defaults should minimize personal data sent to providers. User-created text and package documentation are untrusted model inputs. A model cannot authorize privilege escalation by saying an action is safe.

Multiplayer safety includes ownership/build permissions, personal and communal storage policies, consent for disruptive timeline changes, PvP settings, kick/ban controls and recovery from griefing. In-game laws or monarchs do not substitute for real server administration. Backups/rollback must handle item and timeline duplication consistently.

## 35. Interconnection without uncontrolled coupling

Maintain a **Design Resonance Matrix** linking each mechanic to the domains it reads, changes and makes perceptible. Evaluate meaningful player decisions, resource/knowledge flow, historical consequence, counterplay, clarity, performance and testability. Do not rank an idea as good merely because it touches ten subsystems. Some clean local mechanics are valuable; forced integration can create noise or cascading failures.

Each cross-domain link needs an owner, a typed contract, a timescale, a bounded response and an explanation path. Prefer shared state meanings and explicit events over duplicated inventories, hidden mini-economies or global callbacks that mutate everything. Cycles are allowed when they describe real feedback, but their gain, delay and saturation must be designed.

| Example action | Immediate effect | Wider consequence | Player-readable evidence |
|---|---|---|---|
| Restore a river crossing | Shorter viable cargo route | Lower losses, new trade and migration | Caravans return; prices and resident dialogue change. |
| Remove an apex predator | Reduced direct danger | Prey growth, crop pressure, altered hunting | Tracks, damaged fields, new requests and analysis traces. |
| Recruit a master smith | New labor/teaching capability | Workshop output and local political obligation shift | Contracts, orders and family responses. |
| Learn an extinct crop in the past | New knowledge/seed lineage | Farming, alchemy, trade and ecological risk | Growth behavior, recipes, quarantine or market demand. |
| Build a dungeon arena | New constructed site and jobs | Training, spectacle, rivals and regional fame | Visitors, funded prizes, school recruitment and histories. |
| Expose a succession forgery | Beliefs and legitimacy change | Alliances, law and potential conflict | Testimony, court decisions and faction reactions. |

The critical loop is not merely “systems cause systems.” It is **a player can act, perceive a consequence, understand more, and use that knowledge creatively**. The complete scenarios in Appendix B test that loop end to end.

## 36. Non-goals, safeguards, and scope integrity

V1 is not a literal SNES program, a commercial fan sequel, a ROM compatibility layer, or a project whose first milestone is reconstructing another game. No copying of original code, game data, characters, worlds, dialogue, music or protected expression is required. Architectural inspiration is independent design research, not an asserted source lineage.

V1 is not a global MMO, a universal simulation of every molecule, a promise of infinitely many fully active timelines, or a guarantee that any arbitrary package combination produces a polished campaign. Finite compute/storage and explicit fidelity budgets are part of the world contract.

The project must not collapse into a shallow crafting RPG merely because that is easier, nor into an opaque simulation UI that loses the classic-JRPG experience. Dwarf Fortress-scale depth across adventure and management, Terraria-class creation/exploration, temporal/planar progression, social affiliations, procedural history, optional AI and first-class extensibility all remain requirements. Proposed delivery stages do not authorize deleting them.

The narrative horizon is a quality/experience target, not a requirement to make all activities take 80 hours, a limit on player agency, or a promise that an arbitrary generated story automatically has masterful writing. Characterization, scenes, music, composition and encounter design require authored expertise and evaluation.

A minimal trusted kernel is a goal, not permission to weaken security or make every low-level engine invariant runtime-editable. Optional AI is not an excuse for missing non-AI dialogue, generation or gameplay. Extensibility is a supported contract, not a claim that future engine updates will never be needed.

### Originality and commercial review
The U.S. Copyright Act distinguishes protected expression from ideas, systems and methods; that distinction is not a blanket clearance to reproduce a game’s recognizable expression. [R11] Maintain independently authored source/assets and a license/provenance register for third-party material, including community and AI-assisted content. Avoid relying on claims that an art style is “nebulous” as the product’s legal strategy. Qualified review of the specific implementation, branding and distribution terms is a release gate. This document is a product design specification, not a legal clearance opinion.

## 37. Architectural specification and conceptual contracts

This section is a technology-agnostic architecture specification, not a completed implementation design. Language, engine, networking transport, database and sandbox selection remain decisions for the next review. No existing repository has been inspected or changed for this revision.

### Logical boundaries
| Boundary | Owns | Exposes |
|---|---|---|
| Trusted runtime/kernel | IDs, deterministic execution policy, transactional commit, serialization, capabilities and package loading | Stable host API, schedules, storage/replication interfaces and diagnostics. |
| World/history | Topology, time/branches, generated baselines, historical facts and materialization | Addresses, travel validation, causal queries and checkpoints. |
| Actor/action rules | Bodies, attributes, skills, effects, inventories and combat actions | Legal actions, consequence events and calculation traces. |
| Environment/ecology | Climate, fields, habitats, populations and resources | Local conditions, harvest/pressure facts and forecasts. |
| Society/economy | Relationships, jobs, institutions, ownership, claims, work and ledgers | Contracts, needs, trade, authority and consequence events. |
| Narrative/knowledge | Beliefs, observations, dialogue acts, quests, cast binding and campaign pacing | Authorized journal/dialogue views and state-transition objectives. |
| Creation/packages | Definitions, plans, grammars, integrations, migrations and tests | Visual tools, SDK, validation and export/install workflows. |
| Client presentation | Input, sprite/world rendering, audio, UI and local interpolation | Player commands and authorized views; no canonical writes. |
| Optional AI broker | Credentials, requests, permissions, budgets and accepted-decision artifacts | Typed proposals/evals, validation results and deterministic fallback. |

A common ontology links these boundaries; it does not require one monolithic entity-component database for all graph queries. Use appropriate indexed structures for spatial state, relationships, history and definitions. Decide how immutable content, mutable instances and event/provenance data coexist before choosing a storage product.

### Required semantic contracts
**Command:** caller/actor, unique command ID, authority scope, target IDs, expected version where relevant, requested action, simulation timestamp/order and parameters. Validation is separate from execution. Repeated delivery has one effect.

**Rule/effect:** namespaced definition, version, input/target types, predicates, costs, read/write capabilities, execution phase, bounded computation, output events and explanation descriptors. The rules runtime provides the real evaluation trace used by introspection.

**World event:** unique ID, branch/tick, event type/version, participants, location, cause references, validated state delta and knowledge/publicity policy. Historical prose is a view over selected events, not their sole storage.

**Work order:** issuer/authority, site, process/blueprint, reservations, skills/tools, assigned agents, schedule, progress, outputs and failure/interrupt policy. Creation and NPC labor share this contract.

**Quest contract:** world/branch scope, role bindings, desired predicates, evidence, deadline/pacing policy, prohibited outcomes, reward transaction, alternative/failure continuations and presentation metadata.

**Generation decision:** input/configuration hashes, candidate IDs, attempt index, evaluator/version, scores/reasons if used, selected output hash, authorization and canonical commit reference. AI decisions and deterministic selection remain distinguishable.

### Execution rules
Pin scheduling order, RNG policy, arithmetic/rounding, serialization and permitted parallelism. Workers may compute candidates concurrently, but canonical commits follow stable ordering. Multithreading is not itself prohibited; nondeterministic authoritative outcomes are. Client prediction is reconciled to server truth and cannot invent inventory or quest rewards.

Maintain sparse indexes and budgeted queries. Long history, branch catch-up or graph analysis must not block an interactive tick indefinitely. Requests can be staged as budgeted server jobs while a player continues permitted activity.

Expected repository surfaces include runtime, authoritative host, presentation client, first-party packages, schemas, creator tools, generator/compiler, simulation tests, seed fixtures, migration tools, documentation and build/release automation. Exact folder names and technologies are not mandated by this GDD.

## 38. Development program, milestones, and release evidence

The complete vision is the destination. The development sequence is a way to test its foundations early, not a smaller replacement product. No calendar or staffing estimate is established here. A production estimate must follow measured prototypes and an authored-content inventory.

| Gate | Demonstration | Required exit evidence |
|---|---|---|
| G0 — Executable contracts | Original package-driven action/effect model, persistent IDs, seed manifest, host boundary and rule trace | Deterministic replay fixture; static validation; no copied reference code. |
| G1 — Risk-first technical slice | Two real clients plus local solo, sandboxed package install, save migration, one temporal fork and offline AI fallback | Crash/retry tests; branch/identity correctness; pinned decisions replay without provider. |
| G2 — Playable interdisciplinary loop | One original region with town, wilderness, dungeon, crafting, survival, recruit and a social consequence | An enjoyable short adventure demonstrating Appendix B’s linked outcome rather than separate demos. |
| G3 — Adventure/fortress continuity | Build/operate a settlement, delegate labor, leave on adventure, return to accountable changes | Production and population reconciliation; NPC autonomy; no unload-based duplication. |
| G4 — Historical world synthesis | Coarse eon history, civilizations, notable people, artifacts, ecological state and derived ruins | Repeatable seeds; material historical evidence; valid frontier boundaries and generated quest routes. |
| G5 — Deep progression and temporal play | Large cross-domain graphs, multiple professions, cooperative abilities, portals/eras/alternate branch dungeon | Reachability/balance checks; original art coverage; noncombat and temporal solutions. |
| G6 — Campaign and content production | Diverse campaign grammars, companion arcs, cultures/biomes, dungeons, scores, bonus paths | Representative end-to-end 60–80-hour playtests; measured variety and scene/encounter quality. |
| G7 — Commercial hosted release | Supported first-party profiles, dedicated hosting, creators/expansions, onboarding and accessibility | Recovery, security, migration, load, stability, package and generation acceptance gates. |

Historical evolution, branch semantics, multiplayer and extension compatibility must be tested before locking the architecture; the earlier roadmap placed several of these too late. Rich content can expand after the contracts work, but the first networked and temporal tests cannot wait until the world has been built around incompatible assumptions.

### Nonfunctional targets to validate
A proposed initial experience target is 60 fps client presentation on an explicitly selected reference machine and a fixed host tick suitable for the chosen combat model. A 20 Hz host is a candidate, not a committed value. Determine CPU/memory/save/network budgets through profiles with 1, 4 and 8 independently exploring players, multiple active settlements and branches, and increasing counts of detailed actors and aggregate populations.

Record generation latency, p95 tick/command response, memory per active region/branch, materialization delay, replay cost, save growth, migration duration and degraded-network behavior. Include worst-case creator graphs and optional AI timeouts. “Runs on an ordinary PC” is not an acceptance criterion without a named hardware/configuration profile.

World-history and crafting depth need domain benchmarks as well as raw counts: history-to-ruin causality; construction/labor sophistication; diverse viable builds; social motive differentiation; ecological resilience; and player comprehensibility. Stage representative tests before expanding the number of species, recipes or kingdoms.

### Production and commercial inputs still needed
Estimate authoring families for environments, body/animation/equipment coverage, NPC/companion arcs, puzzle grammars, boss patterns, music and localization. Identify engineering, design, pixel art/animation, audio, narrative, QA and security responsibilities. AI tooling may assist each discipline but is not a substitute for assigning accountability or testing content quality. V2 social/MMO funding and scope remain separately gated.

## 39. Risk register and design tradeoffs

| Risk | Why it matters | Required mitigation / evidence |
|---|---|---|
| Breadth outpaces quality | Many systems can exist as shallow menus. | Integrated acceptance scenarios; domain depth benchmarks; preserve flagship play quality. |
| Story versus player freedom | Unrestricted destruction can invalidate pacing and witnesses. | Explicit quest contracts, alternate routes, fail-forward stories and disclosed deadline policy. |
| Procedural repetition | Large permutations can be mechanically equivalent. | Semantic diversity metrics plus human playtests and authored composition budgets. |
| Combinatorial rules | Skills, packages, crafting and time can create dominant or infinite loops. | Static checks, bounded effects, adversarial simulation, regression fixtures and counters. |
| Temporal storage/causality | Arbitrary rewinds and many branches can explode state or contradict co-op. | Retained epoch contract, immutable ancestry, branch limits, safe transit and identity accounting. |
| Resolution inconsistency | Local and aggregate simulation can duplicate resources or people. | Ownership leases, conservation tests, declared approximation policies and stable facts. |
| Infinite frontier versus global history | Lazy neighbors can contradict earlier geography/politics. | Topology profiles, deterministic boundary contracts and no rewriting observed history. |
| AI nondeterminism and authority | A provider can change output, leak facts or invent unsupported actions. | Logged accepted artifacts, scoped data, schema/semantic validation and offline fallbacks. |
| Package incompatibility | Extension points alone do not define ordering or safe removal. | Typed contracts, dependency/override policy, migration dry runs and supported profiles. |
| Player overwhelm | Deep graphs and management can bury the adventure. | Layered interfaces, delegation, contextual guidance, journals and optional analysis depth. |
| Art combinatorics | Custom bodies and equipment multiply animation work. | Explicit supported families, coverage matrix, asset validation and original style bible. |
| Economic/ecological collapse | Feedback can destroy campaign viability or reward exploits. | Bounded response times, sinks/sources, recovery rules and seed-wide stress tests. |
| Commercial/IP risk | “Same style” is not universal permission to reuse expression. | Original production, provenance/license tracking and qualified review before release. |
| Hosting/MMO overpromises | Small-server architecture does not solve global operations. | V1 reliability first; separate V2 scale/trust decisions and estimates. |

Tradeoffs must be recorded, not smoothed over with claims of unlimited emergence. A simplification is acceptable when it preserves the stated experience and its consequences; deleting a required domain or permanently hiding requested mechanics is not an implicit implementation choice.

## 40. Acceptance and verification specification

These are **tests to implement**, not results claimed as already passing. Each requirement in Appendix A is design-covered; implementation evidence is pending. Retain reproducible fixtures, exact manifests and failures as package/runtime regressions.

| Test ID | Scenario | Passing evidence |
|---|---|---|
| T01 | Repeat deterministic genesis and replay under pinned configuration | Equal authoritative fingerprints at declared checkpoints; cosmetic differences excluded. |
| T02 | Repeat AI-assisted generation from accepted artifacts with provider unavailable | Same committed content; no new inference required; AI-off fresh genesis labeled separately. |
| T03 | Run solo and two remote clients through the same rule scenario | Equivalent legal outcomes; no client-owned canonical writes or duplicated effects. |
| T04 | Retry/crash during craft, trade, construction or loot | Exactly-once resulting ownership and resource accounting after recovery. |
| T05 | Promote/demote a settlement, population and army repeatedly | No duplicated actors, goods, casualties or production; established facts preserved. |
| T06 | Generate eon history and inspect a later ruin | Physical/social evidence cites recorded lineage, events and plausible timescale. |
| T07 | Generate sapient nonhuman societies | Species, culture and hostility vary independently; compatible institutions and architecture. |
| T08 | Remove predators or introduce a crop | Understandable ecological/economic consequences without guaranteed runaway collapse. |
| T09 | Inspect one item/action at several knowledge levels | Accurate scoped values, formula and real trace; no privileged data leak. |
| T10 | Explore skill/crafting graph and install a cross-domain extension | Valid AND/OR reachability, useful alternate paths, manageable UI and no unintended free-resource cycle. |
| T11 | Compose an ability and a multi-actor technique | Legal costs/targets, bounded recursion, clear counterplay and correct interrupt/disconnect handling. |
| T12 | Craft an item through materials/process/quality | Properties and provenance derive from actual inputs; salvage/repair conserve declared resources. |
| T13 | Operate a farm while the player adventures elsewhere | Accountable growth, labor, weather, upkeep and output with delegated management. |
| T14 | Recruit an NPC with competing family/class/political obligations | Reasoned acceptance/refusal; real relocation/work/relationship consequences and no duplicate assignment. |
| T15 | Build and operate a multilevel fortress | Access, storage, labor, services, defenses, institutions and recovery function in one world. |
| T16 | Build and publish a playable dungeon grammar | Valid routes, hazards and inhabitant/resource rules; export contains no illicit live loot/private state. |
| T17 | Solve a generated quest through several disciplines | Recognized meaningful alternate outcomes, earned rewards and coherent later dialogue. |
| T18 | Kill/recruit/lose a critical narrative participant | Supported alternative, successor or explicit failure ending; no silent campaign softlock. |
| T19 | Speak using rumors or AI dialogue under scoped knowledge | Claims are distinct from truth; unauthorized facts do not leak or become canonical. |
| T20 | Traverse a long route at multiple scales | Same geography, vehicle, cargo, clock and weather; repeated travel remains tolerable. |
| T21 | Travel to past, intervene, and visit a changed later branch | Correct lineage, material consequences and source-world protection under chosen policy. |
| T22 | Two parties travel across eras; disconnect during transit | Each traveler exists once, branch permissions hold, reconnect reaches a valid location. |
| T23 | Attempt historical item cloning, rapid aging or temporal trade exploit | Published provenance/transfer rules enforce intended scarcity and costs. |
| T24 | Resolve a political/court/war scenario through personal action | Beliefs, claims, logistics, morale and consequences remain accountable across resolutions. |
| T25 | Run a player-built coliseum tournament | Valid entrants, funded prizes, ranking evidence and nonlethal/safety rules where selected. |
| T26 | Install, configure, upgrade and remove packages | Deterministic ordering, content hashes, dependency validation, reversible migration and safe failure. |
| T27 | Submit malicious or excessively expensive package/AI inputs | Capability/resource limits hold; no arbitrary OS/network/secret access. |
| T28 | Complete representative generated campaigns | Human evidence for 60–80-hour density, characters, pacing, variety, polish and accessible alternatives. |
| T29 | Continue after ending or character retirement | Settlement/history continuity, inheritance/continuation and useful optional/sandbox activity. |
| T30 | Join late, pause/sleep, split groups and resume a persistent server | Disclosed time policy, valid recap/objectives, no involuntary world rewind or hidden calendar jump. |
| T31 | Stress rendering, UI, assets and hosting profiles | Measured budgets and clear unsupported limits; readable original visual/audio presentation. |
| T32 | Review commercial release material and packages | Original assets/code or documented licenses; reference-game expression excluded; attribution retained. |

Definition of done for a major mechanic includes a player-facing use, an expert/introspection view where appropriate, a package extension surface, persistence/migration coverage, multiplayer authority, a cross-domain consequence and an executable regression. A feature checklist alone is not evidence of depth.

## 41. Astra handoff: implementation GDD and first build decisions

The next pass should treat this document as the complete **product requirements baseline**, not as a final engine choice or permission to omit difficult systems. Produce an implementation GDD/TDD that traces every requirement to architecture, data, APIs, UI, tests, delivery stage and unresolved risks. Do not replace the distinctive vision with a generic RPG starter.

### Required decisions, in order
**ADR-01 — Core action/clock/combat contract.** Select a flagship combat/control model; settle pause, sleep, travel, co-op encounter membership and calendar relationships. Prototype at least one end-to-end alternative comparison before committing assets.

**ADR-02 — World topology, history and resolution.** Choose finite/wrapping versus unbounded-region default, macro boundary rules, unit scales, eon/civilization/live resolution, conservation contracts and supported historical reconstruction horizons.

**ADR-03 — Temporal causality and identity.** Decide the flagship branch policy, traveler/alternate identity, artifact transfer, future visits, branch retention, permissions and quest semantics. Demonstrate two-party safety before large content production.

**ADR-04 — Runtime, storage and determinism.** Evaluate engine/language/runtime candidates against original sprite presentation, headless hosting, portable package execution, replay, indexed graphs, branching state and migration. Specify arithmetic, ordering, RNG and parallel commit policy. Do not select a technology solely because the team already knows it.

**ADR-05 — Package API and sandbox.** Define stable registries, effect/query interfaces, typed capabilities, configuration overlay rules, dependency/override order, limits, authoring/export, upgrade/removal and integrity model.

**ADR-06 — NPC autonomy and management.** Specify jobs, work reservations, households, social vectors, authority, decision explanations, migration/recruitment and adventure/fortress continuity. Choose which details are aggregate without erasing required motives.

**ADR-07 — Generation and narrative contracts.** Define grammar language, static validators, constraint solver strategy, cast/quest binding, plot repair, semantic diversity metrics and human quality assessment. Set achievable first-party profile boundaries.

**ADR-08 — AI broker and reproducibility.** Specify typed roles, accepted-decision storage, privacy, budgets, fallback, prompt-injection boundaries and which outputs require human/package-author review.

**ADR-09 — Presentation and production.** Set sprite/tile/camera/elevation standards, body/gear animation coverage, narrative/music authoring inventories, input/accessibility and reference machines. Estimate work only after these assumptions are explicit.

**ADR-10 — Release and community contract.** Decide supported hosts/platforms, package compatibility window, offline/account policy, save transfer, operational support, creator distribution and what V2 genuinely requires.

### Expected next deliverables
Produce domain/data diagrams and schema examples; command/effect/event specifications; package manifests and extension examples; persistence and migration plan; deterministic replay specification; generation/pacing budgets; prototype repository layout; first-party content inventory; security model; measurable acceptance plan; dependency-ordered milestones; and an estimate with staffing assumptions. Map each deliverable to Appendix A IDs and §40 tests.

The first build should prove an original playable interdisciplinary loop with real persistence, at least two clients, one package extension, an introspection trace, a recruited worker and a temporal branch fixture. Do not spend the opening phase only on a universal engine or only on attractive combat screens. Preserve the long-horizon vision while gathering evidence on its hardest contracts.

## Appendix A. Requirement and feedback traceability

This register maps the full design discussion to this revision. “Captured” means specified in the GDD, **not implemented or verified**. Sections identify the controlling design; test IDs refer to §40. Rows A1–A7 summarize owner requirements. A8 contains derived safeguards added during review. No Word review comments or tracked changes were present in the supplied DOCX; conversational comments and corrections are included here.

### A1 — Product identity and experience

| ID | Requirement / feedback captured | Sections | Tests |
|---|---|---|---|
| RQ-001 | Original commercial game, not a ROM modification, copied codebase or research-first source reconstruction. | 1, 2, 36, 37, 41 | T32 |
| RQ-002 | Continue the architectural spirit of classic JRPGs through independent data-driven, statically analyzable systems. | 1, 9, 30, 37 | T10, T26, T32 |
| RQ-003 | Graphics/presentation draw on Chrono Trigger and Secret of Mana, with Final Fantasy IV/VI elements, without copying protected expression. | 4, 31, 32, 36 | T31, T32 |
| RQ-004 | Simulation depth targets Dwarf Fortress scale or beyond in both individual adventure and fortress/settlement play. | 1, 7, 8, 16, 20, 38 | T06, T14, T15, T29 |
| RQ-005 | Crafting, exploration and world manipulation target Terraria richness or beyond. | 6, 11, 13, 19, 28, 38 | T10, T12, T16, T20 |
| RQ-006 | A generated world supports a polished 60–80-hour adventure with bonus chapters, areas and paths. | 3, 18, 30, 38 | T28 |
| RQ-007 | Approximately twice the baseline horizon is a stretch opportunity; the world continues after the campaign. | 3, 10, 18, 33 | T28, T29 |
| RQ-008 | Keep the whole ambitious vision; stage implementation without silently replacing it with a shallow prototype. | 2, 3, 36, 38, 41 | T28, T29 |
| RQ-009 | Study the named references: Crystal Project, Sea of Stars, Octopath Traveler 0, Elliot, Terraria, Stardew and descendants. | 4 | T28, T31 |

### A2 — Procedural geography and history

| ID | Requirement / feedback captured | Sections | Tests |
|---|---|---|---|
| RQ-010 | Worlds, history, locations and campaigns are procedurally generated from coherent rules, not one fixed campaign. | 7, 18, 30 | T01, T06, T28 |
| RQ-011 | Persistent seeds support replaying the same baseline world and creating divergent play histories. | 7, 24, 26 | T01, T21, T29 |
| RQ-012 | Provide practically unbounded spatial/frontier and combinatorial continuation beyond the polished region. | 3, 6, 8, 30 | T20, T29 |
| RQ-013 | Balance large-world travel with JRPG density, ample survival/crafting space and usable building land. | 3, 5, 6 | T20, T28 |
| RQ-014 | Ships, airships and other travel should feel consequential rather than abrupt teleportation. | 6, 21, 26 | T20, T30 |
| RQ-015 | Support eon-scale prehistory/evolution, long civilization histories and live historical continuation. | 7, 8 | T06, T29 |
| RQ-016 | Simulate celestial conditions, seasons, weather and environmental consequences. | 7, 14, 15, 32 | T08, T13, T20 |
| RQ-017 | Simulate monster ecosystems, habitats, resource relationships, migration and adaptation. | 7, 14, 15 | T07, T08, T13 |
| RQ-018 | Species, cultures, empires and cities rise, change, fall and leave ruins or descendants. | 7, 16, 20, 22 | T06, T07, T29 |
| RQ-019 | Dragons, medusas, minotaurs, giants, orcs and new beings can be historical/social actors, not merely enemy types. | 7, 9, 14, 16, 22 | T06, T07, T14 |
| RQ-020 | Historical acts leave physical/social evidence, artifacts, provenance, lineages, dungeons and quests. | 7, 17, 18, 19, 24 | T06, T17, T21 |

### A3 — Actor depth, introspection and progression

| ID | Requirement / feedback captured | Sections | Tests |
|---|---|---|---|
| RQ-021 | Characters are customizable in appearance, stats, traits, equipment, skills and powers. | 10, 11, 12, 13, 31 | T09, T10, T11, T31 |
| RQ-022 | Provide tabletop/D&D-like breadth through original combat, exploration, social, condition and resource mechanics. | 10, 11, 12, 17, 19, 25 | T09, T11, T17 |
| RQ-023 | Default UI abstracts deep mechanics while power players can learn actual traits, algorithms, formulas and values. | 10, 11, 25, 31 | T09 |
| RQ-024 | Introspection itself is a trainable/discoverable in-world capability. | 5, 11, 17, 28 | T09, T10 |
| RQ-025 | Knowledge, observation and analysis can unlock new creation primitives and shared discoveries. | 11, 12, 17, 28 | T09, T10, T17 |
| RQ-026 | Skills and crafting progression are vast, interconnected and interdisciplinary, not isolated linear trees. | 11, 12, 13, 35 | T10, T11, T12 |
| RQ-027 | Support overlapping learning routes through practice, training, apprenticeship, study, discovery and historical travel. | 11, 16, 24 | T10, T14, T21 |
| RQ-028 | Open-ended magic and powers compose from reusable effects with combat and noncombat applications. | 12, 19, 24, 25 | T11, T17, T21 |
| RQ-029 | Support cooperative/combination techniques, builds, equipment synergies and tactical party roles. | 11, 12, 16, 25 | T10, T11 |
| RQ-030 | Jobs include combat identities, professions, social offices and actual work in the world. | 11, 16, 20, 22 | T10, T14, T15 |

### A4 — Creation, survival, domestic play and exploration

| ID | Requirement / feedback captured | Sections | Tests |
|---|---|---|---|
| RQ-031 | Making is integrated into player gameplay rather than only a detached editor. | 5, 13, 19, 20, 28 | T12, T15, T16 |
| RQ-032 | Crafting uses materials, tools, knowledge, skill, environment and transformations. | 11, 13, 14 | T10, T12 |
| RQ-033 | Player-created items, abilities, mechanisms and discoveries extend the same systems used by developers. | 12, 13, 27, 28 | T11, T12, T26 |
| RQ-034 | Survival, shelter, food, water, fatigue, weather and preparation have meaningful but manageable roles. | 8, 10, 14, 15 | T13, T20, T28 |
| RQ-035 | Farming, fishing, foraging, cooking, husbandry, breeding and domestic life connect to ecology/economy. | 14, 15, 20, 21 | T08, T13 |
| RQ-036 | Dungeoneering supports traps, secrets, locks, navigation, light, verticality, archaeology and logistics. | 6, 11, 19 | T16, T17, T20 |
| RQ-037 | Players construct towns, fortresses, workshops, infrastructure and institutions. | 13, 16, 20, 28 | T14, T15, T29 |
| RQ-038 | Players construct and operate dungeons using real world rules and can share dungeon grammars. | 19, 27, 28 | T16, T26 |
| RQ-039 | Crafting/exploration capability unlocks further places, materials, ideas and build choices. | 6, 11, 12, 13, 19, 24 | T10, T12, T20, T21 |

### A5 — People, society, careers and narrative

| ID | Requirement / feedback captured | Sections | Tests |
|---|---|---|---|
| RQ-040 | NPC recruitment changes residence, party, work, society and relationships rather than only a menu. | 16, 20, 21 | T14, T15 |
| RQ-041 | NPCs have tribal/clan affinities and aversions with culture and history. | 7, 16, 22 | T07, T14, T24 |
| RQ-042 | NPCs have filial/kinship, household, ancestry and dependent-family relationships. | 10, 16, 20, 22 | T14, T29 |
| RQ-043 | NPCs have political, ideological, factional, cultural and personal loyalties or aversions. | 16, 17, 22 | T14, T19, T24 |
| RQ-044 | NPCs have socioeconomic/class, professional and material interests, not a single approval value. | 16, 20, 21, 22 | T14, T15, T24 |
| RQ-045 | Recruitable companions, memorable major NPCs and villains require coherent personality and narrative arcs. | 16, 17, 18, 31, 32 | T18, T19, T28 |
| RQ-046 | Dialogue, beliefs, rumors, secrets and knowledge connect to actual world state and social outcomes. | 11, 17, 18, 22, 29 | T17, T19, T24 |
| RQ-047 | Quests can be resolved through supported alternative disciplines and react to changed world states. | 12, 17, 18, 19, 24 | T17, T18, T21 |
| RQ-048 | Heroic adventuring is a complete legitimate play path, without mandatory mastery of all other careers. | 3, 5, 18, 25 | T17, T28 |
| RQ-049 | Trading/mercantilism, shops, caravans, logistics and production are meaningful careers. | 6, 13, 20, 21 | T12, T20, T24 |
| RQ-050 | Coliseums, tournaments, competition, fame and player-built arenas participate in the world. | 20, 23, 25 | T25 |
| RQ-051 | Court intrigue, diplomacy, politics, claims, institutions and succession are supported. | 16, 17, 20, 22 | T14, T19, T24 |
| RQ-052 | Battles and wars connect individual actions, armies, supply, morale and territorial outcomes. | 8, 20, 21, 22, 25 | T05, T24 |

### A6 — Time, planes and alternate worlds

| ID | Requirement / feedback captured | Sections | Tests |
|---|---|---|---|
| RQ-053 | Spatial portals, other planes and distant world locations are native traversal capabilities. | 6, 24, 27, 28 | T20, T22 |
| RQ-054 | Time travel to supported historical/future states participates in actual gameplay. | 7, 8, 24, 26 | T21, T22 |
| RQ-055 | Travel to alternate histories with diverged societies, places, people and artifacts is supported. | 7, 16, 24, 26 | T21, T23 |
| RQ-056 | Quests and puzzles can require temporal/planar progression, not just use it as optional lore. | 18, 19, 24, 30 | T17, T21 |
| RQ-057 | Item, material, skill and craft graphs can depend on lost eras, extinct life and alternate knowledge. | 11, 13, 14, 24 | T10, T12, T21, T23 |
| RQ-058 | Advanced creation can manipulate portals and supported temporal connections under server rules. | 24, 27, 28 | T21, T22, T26 |

### A7 — Hosting, expansions, AI and future platform

| ID | Requirement / feedback captured | Sections | Tests |
|---|---|---|---|
| RQ-059 | Solo/local host, player-hosted co-op and dedicated persistent servers precede MMORPG scale. | 3, 26, 33, 38 | T03, T30 |
| RQ-060 | Players can continue, replay, export or fork persistent seeded worlds. | 10, 24, 26, 33 | T01, T21, T29 |
| RQ-061 | Every major game/content domain has sanctioned first-class expansion surfaces; ordinary mod hacks are unnecessary. | 9, 27, 28, 34, 37 | T26, T27 |
| RQ-062 | Official/base content and community expansions use the same supported package architecture. | 27, 28, 37 | T26, T32 |
| RQ-063 | Packages, rules and plugin settings are customizable per server. | 26, 27, 29, 33 | T01, T26, T30 |
| RQ-064 | World-gen rules, history, narrative grammars, art, music, UI and tools are package extension domains. | 18, 27, 28, 30, 31, 32 | T26, T31 |
| RQ-065 | In-game creations can become portable shared blueprints/definitions/packages where eligible. | 19, 27, 28, 33 | T16, T26, T32 |
| RQ-066 | LLMs/AI are optional and pluggable per server, including parameter proposal and procedural evaluations. | 26, 27, 29, 30 | T02, T19, T27 |
| RQ-067 | Contemporary AI assists implementation/creation/evaluation while the independent game remains playable without it. | 1, 29, 38, 41 | T02, T19 |
| RQ-068 | The architecture underpins later community, social discovery, distribution and MMORPG layers. | 26, 27, 33, 38, 41 | T26, T30 |
| RQ-069 | Deliver a full GDD/specification and an explicit handoff for Astra implementation analysis. | 2, 37, 38, 40, 41 | T01, T32 |

### A8 — Derived safeguards needed to fulfill the vision

| ID | Requirement / feedback captured | Sections | Tests |
|---|---|---|---|
| RQ-070 | Accepted AI decisions/content are recorded and replayed; seed alone is not an AI determinism claim. | 2, 26, 29 | T01, T02 |
| RQ-071 | NPC knowledge and introspection are scoped independently from server truth and administrator access. | 11, 17, 29, 34 | T09, T19, T27 |
| RQ-072 | Timeline changes protect co-op state, use explicit branch policy and transaction-safe travel. | 8, 24, 26 | T21, T22, T23 |
| RQ-073 | Adaptive simulation conserves identities/resources and avoids double counting visible and distant activity. | 8, 13, 20, 22, 37 | T04, T05, T13 |
| RQ-074 | Graph and package composition have typed dependencies, bounded effects, ordering and test coverage. | 11, 12, 27, 30, 34, 37 | T10, T11, T26, T27 |
| RQ-075 | Save/version compatibility, package upgrade/removal, backups and recovery are explicit lifecycle contracts. | 24, 26, 27, 34 | T04, T22, T26 |
| RQ-076 | Narrative, world and host clocks have disclosed pause/deadline policies rather than contradictory urgency. | 3, 8, 18, 25, 26 | T18, T28, T30 |
| RQ-077 | Quality claims are measured targets; unsupported seed/configuration combinations are labeled rather than guaranteed. | 3, 27, 30, 38, 40 | T28, T31 |
| RQ-078 | Access, disability accommodations and essential feedback are not locked behind character progression. | 11, 25, 31, 32 | T09, T31 |
| RQ-079 | World geometry, combat mode, headcount, hardware and technical stack remain explicit decisions, not owner-approved guesses. | 2, 6, 25, 26, 38, 41 | T20, T28, T31 |


## Appendix B. Cross-system acceptance stories

These are original illustrative scenarios and test fixtures, not a mandated setting or fixed campaign. They demonstrate depth through connected outcomes rather than a disconnected feature checklist.

### B1 — The broken reservoir
An ancient giant polity built terraces above a river. Its fall left a fractured reservoir; orc farming households now occupy the valley; a dragon controls the surviving citadel. Weather and damaged irrigation reduce yields, raise food costs and intensify a succession dispute. The generated town, ruins, families, artifacts and dragon history agree.

The player can fight the dragon, negotiate water access, recruit an engineer, acquire materials through trade, repair a mechanism, relocate crops, or discover a supported temporal route to learn the lost process. Each path uses existing rules and has different costs. Repair affects actual irrigation, harvest, work and market outcomes. A knowledgeable character can inspect the chain, while a casual player hears the miller’s relief. Tests: T06, T07, T08, T12, T14, T17, T21, T24.

### B2 — The smith who will not simply move
A master smith has a dependent parent, a guild obligation, distrust of the player’s patron and a desire to teach. Recruitment fails despite good personal affection. The player can provide suitable housing/care, negotiate a teaching contract, establish a local branch workshop or resolve a political obligation. A forged command can create a belief-based complication but cannot directly rewrite the NPC’s loyalty.

Acceptance changes employment, travel, household circumstances and production. The former village experiences a skills gap; an apprentice may replace the smith. The player can inspect known motives without reading undisclosed secrets. One friend adventures while another manages orders; no one duplicates the smith across locations. Tests: T09, T14, T15, T19, T24, T30.

### B3 — The living dungeon school
A player excavates a multilevel ruin into a training dungeon, builds drainage and mechanisms, recruits staff, brings compatible creatures, and funds treasure/prizes. Materials, work orders, habitat needs, permissions and maintenance are real. Visitors create trade and institutional reputation; an accident can trigger treatment, repair and political consequences.

The creator exports a generation grammar without exporting unique live items, private NPC histories or free rewards. Another server installs it under a different difficulty configuration and supplies its own inhabitants/resources. The grammar adapts through capabilities rather than hard-coded upstream IDs. Tests: T04, T05, T13, T15, T16, T25, T26, T27.

### B4 — The star calendar and the crop
A predictable celestial cycle affects a package-defined magical pollinator and thus a rare crop. Astronomical study reveals the schedule; farming research identifies tolerances; engineering enables irrigation; alchemy consumes the harvest; merchants distribute it. A court’s monopoly creates political stakes. The causal rule is explicit, not a random “eclipse means famine” event.

The player may establish a school, share the knowledge, trade in seeds or retrieve an extinct related lineage from another era. Ecological introduction can help or harm local species, with discoverable signs and countermeasures. Farming remains enjoyable when the player declines court politics. Tests: T08, T09, T10, T13, T17, T23, T24.

### B5 — Two parties, one history
Two friends remain in the present while another party visits a supported past epoch and intervenes. Under the proposed branch policy, the intervention creates a new child history. The original friends keep their buildings, calendar and active quests. The travelers can visit the child future, see a changed dungeon/city, and return with authorized knowledge or goods.

Crash the host after departure reservation and before arrival acknowledgment. On recovery every traveler and inventory has exactly one live location. Then attempt to clone a unique crown across visits, remove a required package and delete an occupied branch. Each operation follows the published transfer/migration/retention policy. Tests: T04, T21, T22, T23, T26, T30.

### B6 — One profile, two AI policies
Generate a test world with optional narrative evaluation. Store the accepted decisions and content snapshot. Disable the provider, replay from the recorded baseline, and continue with authored deterministic dialogue fallback. The world remains coherent. Generate a fresh world using the same seed but an AI-off genesis profile; label that fingerprint separately rather than pretending its content must match.

Install a community ecology package with an owner override. Validate and hash the effective configuration, test a migration in a copy, and attempt a malicious prompt hidden in a lore book. It cannot grant permissions or reveal server secrets. Tests: T01, T02, T19, T26, T27.

### B7 — The settlement becomes archaeology
After completing the epic, a player retires and the host deliberately advances a branch across generations. The player’s village changes through institutions, descendants, production, war or abandonment. A later character arrives to find evidence: a workshop’s tools, a civic law, an altered river, graves, a family claim, an arena legend or a dungeon built under the old walls.

The site is not merely re-rolled with a “ruined” skin. Historical records, surviving actors/artifacts and physical layout match the important intervening events. The returning player can adventure there, rebuild it, contest an inheritance or explore its earlier branch. Tests: T05, T06, T15, T17, T21, T29.

## Appendix C. Illustrative world and package contracts

The following examples are **conceptual**, not executable configuration schemas. Names are original placeholders. The implementation GDD must specify actual types, validation rules and serialization.

### C1 — Shareable world identity
```text
WorldManifest
  world_id: persistent logical ID
  seed: ASHEN-SUN-0193
  runtime_abi: pinned simulation/runtime revision
  package_lock: exact package hashes + dependency resolution
  effective_rules_hash: package defaults + server overrides
  topology_profile: declared finite/wrapping/frontier policy
  resolution_policy: pinned scheduling/materialization rules
  genesis_decisions_hash: accepted external choices, or empty
  genesis_snapshot_hash: canonical initial world
  campaign_profile: supported adventure/quality configuration
  active_branches: branch IDs + retained checkpoints
```

Operational endpoint, administrator credentials and AI provider secrets do not belong in a public share string. Private knowledge is not made public by sharing a manifest. Client cosmetic choices are outside authoritative identity unless they change gameplay.

### C2 — Expansion manifest
```text
PackageManifest
  id / version / content_hash / license / authors
  runtime_api_range
  requires: typed capabilities and pinned dependencies
  provides: definitions, systems, grammars, assets, UI tools
  configuration_schema: allowed server overrides
  execution_capabilities: explicit query/action permissions
  execution_limits: instructions, memory, fan-out, storage
  lifecycle: genesis, forward, migration, removal contracts
  integrations: optional capability-based adapters
  tests: regression seeds + invariants + scenario cases
  evals: versioned quality rubrics / optional AI roles
```

An official seafaring package and a community seafaring package should use the same surface. Neither can bypass host authority or rewrite observed history by installation alone.

### C3 — An inspectable rule result
```text
RuleResult
  action_id / rule_id / definition_version
  authorized_observer_scope
  input_facts: resolved actor, item, target and environment
  steps: applied modifiers in actual resolution order
  rounding_policy / resource_debits / emitted_effects
  outcome: authoritative result
  knowledge_projection: surface, estimated, learned or exact
```

The raw trace can be retained locally or summarized under a declared replay policy. A player view filters it by knowledge and permissions; it never invents a contradictory formula for the same result.

### C4 — Historical/temporal provenance
```text
TemporalProvenance
  ancestry_entity_id
  branch_incarnation_id
  live_instance_id
  origin_branch / origin_epoch / creation_event
  ownership_events / transformations / transfer_events
  causal_parent_refs
  unique_equivalence_policy
```

This separates intentionally meeting an alternate-history version from accidentally duplicating one live object. Quest and market logic refer to explicit equivalence rules rather than guessing from an item name.

## Appendix D. Review findings and change log

**Baseline reviewed:** persistent_seed_jrpg_gdd_v0_1.docx and its Markdown counterpart, plus the complete visible design conversation and latest feedback. The supplied DOCX contained no embedded review comments or tracked insertions/deletions. This revision consolidates conversational comments, including later corrections that supersede earlier assistant suggestions.

| Area | Earlier gap or ambiguity | Revision |
|---|---|---|
| Source-code ancestry | Discussion sometimes treated disassembly as a ready commercial foundation. | Explicit independent implementation; no source audit or ROM compatibility dependency claimed. |
| Requirement authority | User requirements and assistant suggestions were mixed. | Requirement/target/proposal/ADR labels and a requirement-to-section/test register. |
| Hidden mechanics | Earlier framing could permanently hide deep rules from players. | Earned access to true formulas, values, provenance and actual calculation traces. |
| World scale | Fictional kilometer figures mixed continent scale and local playable area. | Unvalidated figures retired as requirements; travel/density/build-capacity tests lead sizing. |
| Temporal extent | Thousands of civilizational years could understate “eons.” | Separate geological/biological eons, civilizational history and live clocks. |
| Infinite geography | A finite simulated planet and endless terrain were treated interchangeably. | Explicit topology profiles, frontier boundaries and no rewriting observed geography. |
| Fortress depth | Town building was described broadly without enough management semantics. | Construction stages, labor, logistics, stockpiles, institutions, autonomy, authority and failure/recovery. |
| NPC affinities/jobs | Categories were listed but behavioral and employment consequences were thin. | Distinct filial, clan, political, class and personal ties; negotiated recruitment and real job reservations. |
| Multiplayer time | Solo-style menus, sleep and long travel could affect everyone inconsistently. | Separate clocks and explicit pause/acceleration/encounter policies. |
| Time travel | “Hybrid causality” left identity, co-op rewrites and storage unspecified. | Proposed preserve-and-branch model, alternative ADR, retained epochs and transactional transit. |
| AI determinism | A deterministic selector was assumed to neutralize nondeterministic evaluation. | Accepted decisions/content are stored; AI-assisted and AI-off baselines are distinguished. |
| Package safety | Event hooks/signatures were implicitly treated as sufficient compatibility or safety. | Ordering/conflict contracts, capability limits, migration/removal and integrity-versus-trust distinction. |
| Interdisciplinarity | More subsystem connections were treated as automatically better. | Meaningful consequences, bounded feedback and typed domain ownership rather than maximum coupling. |
| Procedural polish | A 60–80-hour high-quality story was sometimes phrased as automatically guaranteed. | Human-tested release targets, grammar/content budgets and supported configuration boundaries. |
| Roadmap | Networking, history and temporal risks appeared too late. | Risk-first network/package/branch tests precede large production. |
| MMO follow-on | A later MMO was presented too close to a scaling-only task. | Explicitly acknowledges future partitioning, trust and operations redesign. |
| Comparative study | Prior conversation implied broader hands-on/current verification than documented. | Official-feature citations separated from our interpretations and future study tasks. |

No owner-requested domain is intentionally removed. The original short high-level sections are expanded into operational behavior, constraints and testable contracts. The unresolved decisions are exposed in §41 rather than silently resolved by the author.

## Appendix E. Sources, verification boundaries, and originality

External reference descriptions were checked against the following primary developer/publisher or official legal sources on **8 September 2026**. Citations support reference-game feature statements or the limited legal distinction, not our proposed architecture’s feasibility. No prices, review scores, current player counts or release-state claims are used as design evidence. No reference screenshots, game assets or source code are embedded in this GDD.

**[R01] Andrew Willman — Crystal Project, official Steam description.** [Official source](https://store.steampowered.com/app/1637730/Crystal_Project/). Scope: Nonlinear exploration, classes/subclasses, traversal and visible combat information.

**[R02] Sabotage Studio — Sea of Stars, official game site.** [Official source](https://seaofstarsgame.co/). Scope: Field combat, traversal and dynamic presentation features; not a source-code analysis.

**[R03] Square Enix — Octopath Traveler 0, official System page.** [Official source](https://www.jp.square-enix.com/octopathtraveler0/system/). Scope: Character creation, jobs, town facilities/recruitment, transferable abilities and party combat; Japanese feature text.

**[R04] Square Enix — The Adventures of Elliot: The Millennium Tales, official site.** [Official source](https://www.square-enix.com/the-adventures-of-elliot-the-millennium-tales/en-us/). Scope: Action, field exploration, opening paths and historical discovery; not a hands-on assessment.

**[R05] Re-Logic — Terraria, official Steam description.** [Official source](https://store.steampowered.com/app/105600/Terraria/). Scope: Generated worlds, exploration, excavation, equipment/machinery and building.

**[R06] ConcernedApe — Stardew Valley, official About page.** [Official source](https://www.stardewvalley.net/about/). Scope: Farming, professions, community schedules, seasons, crafting and relationships.

**[R07] NPC Studio — Fields of Mistria, official site.** [Official source](https://www.fieldsofmistria.com/). Scope: Advertised community/farming/magic/ruin-exploration design; not a verification of every feature’s release state.

**[R08] Marvelous / XSEED — Rune Factory 4 Special, official Steam description.** [Official source](https://store.steampowered.com/app/1580240/Rune_Factory_4_Special/). Scope: Farming, monster raising, crafts, social life and dungeon adventure.

**[R09] Square Enix — Dragon Quest Builders 2, official Steam description.** [Official source](https://store.steampowered.com/app/1072420/DRAGON_QUEST_BUILDERS_2/). Scope: Construction, exploration/materials, villagers/crops and cooperative building.

**[R10] Bay 12 Games — Dwarf Fortress Features.** [Official source](https://www.bay12games.com/dwarves/features.html). Scope: Official description of persistent history, adventure/outpost play, construction, people, materials and weather; does not prove our feasibility.

**[R11] U.S. Copyright Office — Title 17, Chapter 1, especially §§102–103.** [Official source](https://www.copyright.gov/title17/92chap1.html). Scope: Distinction between expression and ideas/systems and treatment of derivative works; not clearance of this project.

The primary design source is the owner’s conversation reproduced in this project and the supplied v0.1 document. Exact original-SNES source layouts, hidden coefficients, module names, and the completeness of any fan disassembly were not independently verified for this revision and are not implementation assumptions. Reference games not covered by a source above are explicitly comparative study targets, not claims of completed research.

All scenario names and proposed contracts are illustrative original designs. The next implementation pass must preserve attribution and licenses for any external code/assets it chooses to introduce.

## Appendix F. Glossary and scope reminders

**Adventure envelope:** the geography, cast, systems exposure and narrative material elevated to the flagship campaign’s quality/duration target; not the whole universe.

**Authoritative:** the host’s validated state is the game’s source of truth, even when host and client run on the same machine.

**Branch:** a materialized alternate history with shared ancestry and independently tracked subsequent state.

**Canonical:** accepted structured state or fact; distinct from what a character believes or a dialogue renderer says.

**Capability:** a typed power an actor/package may exercise, with rules and permissions; not a promise of unrestricted engine access.

**Creation:** in-world making and authoring through the same systems used by official content; sometimes presented through a workbench or planning UI.

**Definition / instance:** a versioned reusable content/rule template versus one mutable object/person/site in a world.

**Epistemic state:** what a character knows, believes, estimates or has evidence for; presented in-game as knowledge, research and memory rather than jargon.

**Introspection:** earned analysis of real mechanics, parameters, provenance and consequences; separate from basic accessibility and privileged debugging.

**Materialization:** conversion of retained coarse/historical state into detailed playable state under explicit consistency rules.

**Package:** a versioned sanctioned extension with declared content, behavior, dependencies, configuration, permissions, lifecycle and tests.

**Persistent seed world:** a reproducible generated baseline plus an evolving stored history. The seed is not a replacement for a save.

**Procedural:** world-specific content emerges from rules, authored components and recorded decisions. It need not mean every pixel or line is invented from nothing at runtime.

**Simulation depth:** meaningful state, rules and cross-domain consequences that produce player decisions—not merely a large number of hidden variables.

**Supported profile:** a tested package/configuration combination. Custom profiles may be experimental without ceasing to be sanctioned installations.

**World / server / campaign:** respectively a persistent fictional state and lineage, its hosting environment, and a bounded adventure arc inside it.
