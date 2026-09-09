# Systemic worlds: evidence and contracts for Beta OpenFantasy

## Assessment

The selected architecture in [v0.6](../architecture-synthesis-v0_6.md) is appropriate: one portable C# authority, shared character capabilities, persistent individuals, bounded planning, and separate simulation and presentation. Dwarf Fortress supplies the strongest evidence for history becoming an ongoing society. Caves of Qud supplies concrete examples of composable capabilities and coordinated procedural meaning. Cataclysm: Dark Days Ahead supplies inspectable character and content interfaces. RimWorld supplies a useful criticism: simulation has to become understandable experience.

These precedents support the direction, not a claim that Beta's combined scope is already proven. In particular, no examined reference establishes complete, uniformly detailed, continuously autonomous NPC lives across an arbitrarily large world, combined with independent multiplayer Wait encounters and elaborate animated presentation. “Player-equivalent” should mean the same applicable state, rules, opportunities, and consequences. “Anything is possible” should mean open outcomes within a declared action grammar, with extension points for future verbs. Neither phrase is an implementable promise of unlimited computation or every imaginable behavior.

## Dwarf Fortress: continuity across resolution changes

**Documented evidence.** Bay 12's December 2013 logs describe three population resolutions: local play, abstract building/dungeon populations, and abstract site populations. Population duplication required a new tracking system. Historical individuals were more expensive than anonymous populations. The same logs describe transferring tribute negotiations between abstract sites, buildings, and actual rooms; October entries describe preserving the same damaged buildings through unloading and reloading. These are historical implementation reports, not claims about unresolved current bugs.[^1]

**Architectural implication.** Beta's requirement that every individual retain full depth is stronger than that historical population model. It may share immutable definitions and compact uneventful records, but cannot replace an established person with a generic population count. Promotion into local play must materialize the person's existing job, belongings, commitments, route, injuries, and knowledge. A camera cannot manufacture their biography or repeat a completed work interval.

**Documented evidence.** In his 2023 interview, Tarn Adams describes world generation establishing sites, diplomatic relationships, and historical figures; some live mechanics continue differently or remain incomplete. Villain plans use structures that survive world generation into play. To restrain necromancers, the developers added opposing alliances and considered further institutional responses. Adams also describes conservative “might contain items” flags that skip expensive checks without requiring perfectly maintained negative information.[^2]

**Architectural implication.** Prefer real countervailing systems over forcing a dark lord to lose because a plot needs an ending. Supplies, recruitment, vulnerable transport, succession, resistance, intelligence, and coalition formation should constrain expansion. A world can still fall. Starting profiles and crisis settings should communicate those possibilities, while surviving people retain playable alternatives such as service, escape, captivity, resistance, trade, or migration. A story director may select what to reveal and which eligible opportunities to offer; it may not falsify armies, victories, or resources.

**Documented evidence.** Bay 12's 2026 release notes describe throttling repeatedly failed paths, leaving zombies uninterested in surroundings until disturbed, retaining rarely encountered friends/grudges, preventing contradictory diplomatic missions, and disabling a family need where isolated residents lacked ways to satisfy it. The March development log separately describes procedural magical populations and laboratory work in development. That work is not evidence of a completed general-purpose magic system available for Beta to inherit.[^3]

**Architectural implication.** Needs require feasible responses. A familiar cannot merely lose hunger points forever while all feeding routes are unavailable. An isolated artisan needs a legible blocked state, substitution, request for help, or changed goal. Blocked plans should subscribe to relevant changes and use bounded retries, not continuously search the same impossible route. Instrument stalled obligations and their reasons before adding thousands of people.

## Caves of Qud: meaningful generation and reusable capabilities

**Documented evidence.** Grinblat and Bucklew's 2017 paper distinguishes history as a causal process from historical accounts encountered in the present. Its mythic biography generator chooses events and rationalizes them against historical entity properties, using state transitions and replacement grammar. Coherence comes from recurring properties and constrained relationships, without simulating every underlying cause.[^4]

**Architectural implication.** Use this technique for myths, inscriptions, oral traditions, biased accounts, and sufficiently constrained prehistory. Separate an account's assertions from canonical world facts. Once a living NPC, artifact owner, species transformation, or divine pact is committed, later text generation must respect that fact. Uncertain ancient explanations can coexist with authoritative consequences: a buried weapon has one provenance and material state even when three cultures disagree about who blessed it.

**Documented evidence.** Qud's 2019 GDC session describes connecting village histories, cultures, architectural styles, storytelling, NPCs, and quests through multiple levels of generation. The session abstract and indexed slide excerpts establish that coverage; the full recording and complete slide deck were not available for detailed inspection here.[^5] In a 2022 interview, the developers describe using an authored backbone inside procedural surroundings. They explain that adding a Brain component to furniture yielded animated, conversational tables, and explicitly caution that concrete illustrative presentation would make Qud's extreme breadth more expensive and constrained.[^6]

**Architectural implication.** Generate a settlement through shared constraints: water, food, local materials, routes, beliefs, institutions, occupations, needs, and relationships should feed architecture, recipes, clothing, stories, and quests. Avoid independent randomizers that create a sea cult, desert boatyard, and fish economy with no connective explanation. Preserve original OpenFantasy's authored cast and places as living anchors inside that framework. Their identities survive; their future need not remain frozen.

**Documented interface.** Bucklew's object-modding documentation exposes XML blueprints with explicit merge versus replacement behavior.[^7] His published mutation example provides actual Flaming Hands source: the ability registers for an NPC offensive-action query, tests range/visibility/cooldown, dispatches temperature and damage events, prevents incompatible equipment, and supports mutation removal. The same historical sample contains direct rendering and sleep calls inside ability execution.[^8]

**Architectural implication.** Adopt reusable capabilities that describe AI affordances, costs, anatomy constraints, effects, and reversibility. Reject presentation waits and renderer references inside the authority. A researchable fire enchantment, creature breath, burning oil, and divine flame may reuse effect primitives while preserving distinct casting rules and provenance. Removal must unwind only effects owned by that instance; it must not reset unrelated later changes.

The documented C# mod mechanism compiled source into privileged assemblies through reflection and supported overriding types. This is a historical extension surface, not proof of current API stability or a safe multiplayer package boundary.[^9] Beta should use versioned definitions and typed effect operations for ordinary content. Executable extensions require explicit compatibility, trust, deterministic behavior, and save-migration contracts. Do not expose unrestricted core mutation simply because a reference game permits it.

## Bounded comparisons: Cataclysm and RimWorld

**Inspectable code.** Cataclysm's public `npc.h` derives NPCs from `Character` and exposes skills, factions, inventory ownership, serialization, mounts, and activities. This demonstrates a shared character foundation, not complete equality of every NPC and player behavior.[^10] Its faction documentation explicitly supports nonreciprocal relationships.[^11] Its magic interface composes target rules, casting costs, component requirements, fields, chained effects, and enchantments; underlying effect operations remain implemented in code.[^12]

**Architectural implication.** Preserve separate personal, institutional, religious, and elemental relations. A guild defending a merchant does not imply the reverse, and a worker's loyalty need not match their nation's diplomacy. Use one reusable requirement/process model for food, alchemy, spells, vehicle parts, and maintenance. Validate substitutions and resource accounting before multiplying recipes. NPC planners need bounded, explicit action affordances rather than reasoning over every possible recipe or spell composition during each decision.

**Documented design argument.** In *The Simulation Dream*, RimWorld creator Tynan Sylvester argues that complexity hidden from the player's mental model contributes little experienced value; readable behavior lets players interpret and coauthor stories.[^13] This is design reasoning, not an audited RimWorld scheduler or permission to invent Beta's promised causality.

**Architectural implication.** Every major world consequence needs a perceivable path: an empty market stall, a delayed letter, a repaired cart, altered greetings, a memorial, or a changed route. Developer omniscient inspection and character-limited knowledge views must remain distinct. Virtual playtesters can inspect consistency and attempt goals; human playtests must establish whether the world feels alive, readable, and enjoyable.

## Adventurelets, encounter decks, and interacting grand narratives

**Documented evidence.** Failbetter's 2012 design diary describes storylets gated by mutable qualities, with choices explicitly changing those qualities; its deck guidance explains how availability and deck size shape repetition.[^14] Emily Short's *Storylets Play Together* describes episodes feeding larger arcs while resources, relationships, and threats connect otherwise separate stories.[^15]

**Architectural implication.** Combine contextual spontaneous encounters, authored adventurelets, and eligible encounter decks over the same world. A deck draws opportunities, not alternate facts. Eligibility must bind existing actors, locations, knowledge, and resources; reserve scarce participants; revalidate before execution; and record cooldown, consumption, cancellation, and RNG state. Invalid draws should return an explicit reason and choose another eligible opportunity or none. Never revive a dead quest giver or spawn a replacement shipment to honor a card.

Grand narratives need separate persistent instances, including active, dormant, transformed, and resolved states. A succession conflict, magical discovery, religious schism, ecological crisis, and dark-lord rise may overlap through shared people and institutions. Their outcomes affect one another through committed actions, not a hidden global plot counter. Random seeds establish opportunities and initial conditions, not guaranteed endings. Test deck dilution after adding content, starvation of low-weight arcs, conflicting participant reservations, and the cancellation of an adventurelet whose premise another NPC has already resolved.

## Proposed contracts for the executable specification

These recommendations refine v0.6; they are not claims of implemented behavior.

| Contract | Required behavior |
|---|---|
| SW-01 — Character parity | Player and NPC controllers submit the same applicable commands against the same character state. Differences require an explicit capability, knowledge, legal, anatomical, or control-permission reason. Every established individual remains recoverable. |
| SW-02 — Scheduled cognition | Persist current goals, commitments, action phase, decision deadline, and relevant evidence versions. Use bounded candidate generation, search expansion limits, stable tie-breaking, interruption rules, and an explanation when no feasible plan exists. |
| SW-03 — Resolution transfer | One owner advances each actor/job/vehicle interval. Transfer a checkpoint containing last committed time, pending events, reservations, custody, route progress, effect clocks, and RNG state. Boundary crossing never resets or rerolls them. |
| SW-04 — Facts and knowledge | Store authoritative facts separately from observations, beliefs, rumors, and authored accounts. Evidence has source, audience, time, uncertainty, and disclosure rules. Planners cannot query inaccessible faction secrets through convenience APIs. |
| SW-05 — Consequence accounting | Joint labor, travel, combat, research, and worship use actual participants and reserved resources. Death, departure, or capture interrupts affected commitments once and emits durable continuations. No narrative-owned duplicate inventory. |
| SW-06 — Effect composition | Version each researched spell definition; bound graph depth, expansion, affected area, spawned entities, and recursive triggers. Declare stacking, targeting, custody, dispel, source attribution, and tactical/world clock behavior. |
| SW-07 — History and crisis | Distinguish generated prehistory from committed live events. Preserve lineage and deity rule versions. Crisis actors use feasible recruitment/logistics and can succeed or fail without player involvement. Narrative views never rewrite resolved outcomes. |
| SW-08 — Durable diagnosis | Checkpoints pin rules/packages and include pending work and command receipts. Retain causal references needed by relationships, claims, quests, and explanations; compact routine traces only under a declared retention rule. Export reproducible failing seeds and redacted diagnostic bundles. |

Simulation detail needs an explicit equivalence policy. For supported routine jobs, advancing ten minutes in one batch must match advancing the same interval in smaller batches, including interruption boundaries. For abstractions such as distant campaigns that intentionally differ from tactical resolution, document the approximation and prohibit camera movement from selecting a more favorable resolver after relevant outcomes become committed. Actor depth survives either policy; invisible tactical omniscience does not follow from it.

Performance limits should use deterministic work units for authority decisions. If a machine cannot finish the scheduled work on time, reduce presentation rate or slow the simulation according to host policy. Silently dropping different NPC decisions according to local wall-clock time would make hardware alter history. Worker threads can calculate candidates or load data; stable validation and commit order still govern consequences.

## Retained experiments and adversarial validation

The following experiments belong in the real core, importer, tools, and Godot adapter. Early small fixtures remain regression assets as production scale grows. Run supported workloads on Windows, macOS, and Linux, with one through four players in a hosted world; retain mobile as a later platform-validation phase.

| Experiment | Workload and evidence required |
|---|---|
| SW-P1 — Population ladder | Sweep 100, 1,000, 10,000, and 100,000 persistent individuals, varying active local populations independently. These are stress points, not launch promises. Measure bytes per actor/history edge, p50/p95/p99 scheduling cost, allocation, GC pauses, save size, and backlog. |
| SW-P2 — Planning storm | Block a major bridge while multiple factions reroute trade and refugees seek shelter. Measure path searches, unsuccessful retries, event fanout, obligation latency, and starvation. Verify that bounded work recovers without omniscient rerouting. |
| SW-P3 — Resolution invariance | Run the same seeded jobs with frequent, infrequent, and adversarial load/unload patterns. Compare conserved resources, stable identities, memories, effect expiry, and declared exact outcomes; classify every intentional approximation. |
| SW-P4 — Long history | Advance centuries of lineage/institution history and years of live society at explicit epoch resolutions. Measure generation time and growth of births, deaths, claims, research, and records. Ensure useful causal history remains inspectable after compaction. |
| SW-P5 — Coupled encounter | Hold Wait combat beside a burning workshop and a moving vehicle while distant labor and political orders progress. Verify the adopted admission/calendar contract, effect ownership, and absence of outside free contributions. |
| SW-P6 — Recovery | Kill the process around each custody, research, crisis, and checkpoint boundary; retry commands and change package order. Recover the last durable state without duplicated cargo, invented knowledge, or unsupported schema fallback. |

Adversarial scenarios should include: two jobs reserving the last reagent; an ally dying during a combined action; a mount unloaded while starving or carrying a passenger; a rumor disproved before a caravan departs; a covert ally revealed through an unrelated UI; enchantments recursively triggering one another; a treaty and war order competing in one commit; a world crisis resolved by NPCs before the player arrives; and a lineage rule update attempting to reroll a known person's affinity. Each must produce a bounded, explainable result or a clear validation rejection.

A connected slice should follow one family, artisan, courier, animal companion, and faction through a damaged crossing, herb shortage, repair/research choice, journey, Wait encounter, and autonomous crisis development. Revisit after distance, time, and restart. Critic agents inspect the causal record and attempt exploits. Virtual playtesters pursue several careers using ordinary commands. Artist agents demonstrate supported personality, care, work, injury, and weather performances; humans judge readability and attachment. Production expands this tested fabric rather than assembling unrelated feature demonstrations.

## Sources and evidence boundaries

Sources accessed 8 September 2026. Developer interviews are primary testimony, not source-code audits. Historical snippets establish patterns at their stated dates. Cataclysm links reference the inspected live documentation/source branch, not a pinned release or measured benchmark. No proprietary full codebase or decompiled binary was obtained.

[^1]: Tarn Adams/Bay 12 Games, [2013 development log](https://www.bay12games.com/dwarves/dev_2013.html), especially 13 October and 8, 15, 23 December 2013.
[^2]: John Harris interviewing Tarn Adams, [How Tarn Adams upgraded and optimized Dwarf Fortress for its official Steam release](https://www.gamedeveloper.com/programming/how-tarn-adams-upgraded-and-optimized-dwarf-fortress-for-its-official-steam-release), 14 March 2023.
[^3]: Bay 12 Games, [development log and release notes](https://www.bay12games.com/dwarves/), 4/31 March, 13 May, and 5 August 2026; pending magic work distinguished from releases.
[^4]: Jason Grinblat and C. Brian Bucklew, [Subverting Historical Cause & Effect: Generation of Mythic Biographies in Caves of Qud](https://pcgworkshop.com/archive/grinblat2017subverting.pdf), FDG 2017, DOI 10.1145/3102071.3110574.
[^5]: Brian Bucklew and Jason Grinblat, [End-to-End Procedural Generation in Caves of Qud](https://www.gdcvault.com/play/1025914/Math-for-Game-Developers-End), GDC 2019; abstract and indexed slide excerpts only.
[^6]: John Harris interviewing Brian Bucklew and Jason Grinblat, [Tapping into the potential of procedural generation in Caves of Qud](https://www.gamedeveloper.com/design/tapping-into-the-potential-of-procedural-generation-in-caves-of-qud), 10 March 2022.
[^7]: C. Brian Bucklew, [Object Modding](https://freehold.atlassian.net/wiki/spaces/CQP/pages/23691295/Object+Modding), 26 June 2016.
[^8]: C. Brian Bucklew, [Creating new mutations](https://freehold.atlassian.net/wiki/spaces/CQP/pages/25690149/Creating+new+mutations), 14 July 2016; developer-published source example.
[^9]: C. Brian Bucklew, [.cs Scripting Mods](https://freehold.atlassian.net/wiki/spaces/CQP/pages/25690146/.cs+Scripting+Mods), 2 July 2016.
[^10]: CleverRaven contributors, [Cataclysm-DDA npc.h](https://github.com/CleverRaven/Cataclysm-DDA/blob/master/src/npc.h), public source, inspected `Character` inheritance and NPC interfaces.
[^11]: CleverRaven contributors, [NPC factions](https://docs.cataclysmdda.org/JSON/FACTIONS.html), live maintainer documentation.
[^12]: CleverRaven contributors, [Magic, Spells, and Enchantments](https://docs.cataclysmdda.org/JSON/MAGIC.html), live maintainer documentation.
[^13]: Tynan Sylvester, [The Simulation Dream](https://tynansylvester.com/2013/06/the-simulation-dream/), June 2013; the author's design argument, not independently verified historical claims about other games.
[^14]: Failbetter Games, [StoryNexus Developer Diary #2](https://www.failbettergames.com/news/storynexus-developer-diary-2-fewer-spreadsheets-less-swearing), 5 August 2012; [Narrative Snippets: StoryNexus Tricks](https://www.failbettergames.com/news/narrative-snippets-storynexus-tricks), 9 September 2012. Historical design evidence, not a platform recommendation.
[^15]: Emily Short, [Storylets Play Together](https://emshort.blog/2019/12/03/storylets-play-together/), 3 December 2019; direct author discussion of *Bee* and storylet composition.
