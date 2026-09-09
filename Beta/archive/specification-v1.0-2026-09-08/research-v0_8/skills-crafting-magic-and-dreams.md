# Skills, crafting, magic and dream traversal

## Scope and evidence

Beta's professions, combat jobs, learned introspection, researched magic and planar abilities should be different uses of a shared capability model. Their common structure is an actor with knowledge and tools performing an action, consuming or reserving resources, producing consequences and learning from the result. They need distinct play experiences and expressive performances; a common implementation should not make cooking, acting and spell research feel interchangeable.

The sources below include developer interviews, official manuals, maintainer documentation and explicitly separated Reddit observations. The proposed contracts and experiments are design judgments for Beta. None establishes that a universal skill engine, dream simulation or the complete catalog already works. Dates matter: older examples demonstrate a decision or failure at that time, not necessarily the current balance of the cited game.

## 1. Composition needs a controlled language

In a 2019 interview, Noita developer Petri Purho described a C++ engine combining simulation with Lua and other libraries. He also described a failed experiment in collapsible combat buildings: the impressive simulation often left little useful play space. His central design concern was whether players understood and enjoyed the consequences, not merely whether simulation ran quickly.[^1] This supports Beta's systemic ambition while directly challenging the assumption that more simulated interactions automatically improve play.

Noita's official release notes also record changing a duplication spell to avoid recursion-related crashes and limiting a particular persistent casting mechanism.[^2] A spell-composition system therefore needs explicit execution bounds even when its appeal is creative combination. Limits should be expressed in terms players can understand—capacity, focus, resources or instability—while validators catch malformed definitions before those definitions enter a world.

**Beta application.** Define a spell as a typed graph of targeting, delivery, transformations, costs, conditions, duration and termination. Schools express methods and learning traditions; elemental or other facets express compatible properties. A fire enchantment, an illusion, a lingering mist and a dream projection need different supported semantics. A new combination can emerge from implemented primitives, but inventing an unsupported primitive requires an extension and corresponding tests. The player-facing research interface proposes compositions within that vocabulary rather than sending arbitrary prose to the authority.

Graph validation must consider maximum targets, recursive triggers, spawned effects, repeated resource conversion, cross-realm references and suspension. Changing a spell after creation produces a versioned definition; a saved wand cannot silently change because a newer package reuses its identifier. An NPC with the same applicable knowledge can build or use the same legal composition. The plan should include an adversarial spell that triggers another spell and tries to retrigger its parent; rejecting or bounding that graph is a real proof of safe composition.

## 2. Schools, jobs and professions have different responsibilities

D&D's published basic spell rules distinguish access through class lists, preparation, casting requirements, longer rituals and concentration. The spell's declared effects delimit what it does, with broader adjudication left to a human referee.[^3] A computer game cannot inherit that last responsibility without defining an executable rule or explicitly optional adjudication surface.

Beta should retain the useful separation: a class/job grants a learning route, identity and specialization; a skill describes competence; knowledge records learned content; a capability permits a concrete action; an active obligation or effect governs ongoing cost. These should not be collapsed into one level number. A learned profession remains useful when a character changes combat role, subject to explicit equipment, practice and specialization rules. Original Squire, Mage and Scout definitions remain migration obligations, not names to replace with a more fashionable system.

The required catalog includes fishing, hunting, cooking, knitting, writing, acting and acrobatics. For each, author at least a basic action, an advanced use, a learning route, a consequential outcome, native performances and a meaningful quest or puzzle application. Fishing can combine location knowledge, bait, weather and technique; hunting can combine tracking, animal behavior, equipment and an ethical/political context. Knitting can produce insulation or repair a textile. Writing can preserve knowledge, create correspondence or support a disputed record. Acting can convey a performance or attempt an impersonation. Acrobatics can open a route without becoming an automatic combat advantage everywhere.

Interdisciplinary use is the main opportunity. A hunter obtains a material a smith needs; a scholar identifies a lost treatment; a textile specialist prepares compatible insulation; a mage applies a ward; an actor gets the party into a gathering where the maker's testimony matters. Each contribution should have a valid simpler alternative when the adventure path is not meant to require that profession. The union of professions must expand agency instead of forcing one character to master every tree.

## 3. Deep production graphs and approachable operation

Atelier Ryza 3's official manual describes linked ingredient loops, selected traits and an automatic material-selection option. Its automation does not fill every special transformation loop.[^4] The useful pattern is a simple reliable operation with opt-in control over a richer recipe. Beta need not imitate that game's interface or ingredient mechanics to use the same separation.

Cataclysm-DDA's proficiency documentation distinguishes general skills from specific learned proficiencies, prerequisites and penalties when a craft lacks them.[^5] Its practice-recipe documentation offers explicit practice activities with no ordinary finished product.[^6] Those are actionable models for apprenticeships and learning by doing, but their existence does not establish that any given penalty or training duration is enjoyable.

**Beta application.** The default craft command selects a known valid recipe and clearly shows important cost, duration and output. Advanced control can expose permitted substitutions, order, heat, purity, potency, stability or structural tradeoffs as the character learns them. Automation must use declared inventory preferences and reservations: it should not consume a unique heirloom merely because it is technically valid metal. A preview names what is known, estimated or unknown and can explain why an unavailable option is blocked.

Mineral composition should travel through refining, smelting and alloying as lot provenance. Production stages account for consumed mass or declared game quantities, fuel/energy, waste and recoverable material. A lower-quality input may be suitable for a wall or tool but inappropriate for a specialized weapon. Quality needs dimensions—durability, sharpness, warmth, potency, taste or stability—rather than a single rarity label that erases all material choices.

Herbal tea is a useful small proof: harvest an identified plant in season; preserve or use it fresh; choose water and preparation; spend real heat and time; serve an actual prepared item; apply its supported effects and personal preference response. A hot spring similarly has actual water/heat, location, capacity, exposure and access. Both can support conversation or recovery without resetting needs for free. Test interruption, sharing the last ingredient and serving an unknown preparation to an NPC with a relevant dislike or condition.

## 4. Recycling and combination exploits are production design

Factorio's quality announcement explains why its proposed recycler returned a limited fraction of ingredients: productivity bonuses could otherwise produce a net-positive loop.[^7] A later development account identifies another difficulty: when an item has alternative recipes, deciding which ingredients recycling should return becomes ambiguous.[^8] These are concrete precedents for checking whole process graphs rather than reviewing each recipe alone.

Beta should associate a produced lot with its actual process and inputs where downstream recovery depends on them. A package that changes a recipe cannot retrospectively change an existing item's origin. Salvage, repair, enchantment extraction, purification and temporal transport need the same conservation review. Clearly priced supernatural creation can break ordinary material conservation as an intentional rule, but it must declare what is spent and why repeated use is bounded.

Analyze strongly connected groups of processes for unpriced gain in valuable quantities, energy, money, knowledge rewards and rare claims. Not every cycle is a bug: a sustainable farm or recycling trade can be intended. The question is whether its inputs, time, capacity, risk and losses match the stated economy. Validate with actual repeated transactions, not only symbolic inspection, because rounding and partial stacks can create value even when the nominal formula appears safe.

## 5. Earned introspection without accidental omniscience

Reddit discussions around Noita include players who enjoy experimentation and players who still struggle to understand wand construction after substantial play. Qud discussions similarly debate the opportunity cost of investing in crafting skills and the value of alternative teaching routes.[^9] These are useful contrasts, not representative surveys or measurements of either game's learning curve.

The strongest Beta response is not to reveal every number immediately or to hide every rule. Separate **rule literacy**, **observed inputs** and **available control**. A character can know how alloy hardness is estimated while lacking reliable knowledge of an untested ore. A diplomat can understand succession law without knowing a secret claimant. An expert can recognize an illusion's tell without being given every true hidden object behind it. UI precision must depend on evidence, not only skill level.

At novice level, show immediate actions, known danger, meaningful cost and legible feedback. At growing expertise, explain relevant properties and tradeoffs. At mastery, offer optional formulae, comparisons, process editing, recorded evidence and informed forecasts. Players can return to concise views at any level. Accessibility, controls and basic comprehension are not character abilities to purchase. In testing, compare what novices and experts can infer from identical scenes, and also compare two scenes that differ only in a secret the character has not discovered: their permitted projections must remain indistinguishable.

Reddit reports about Cataclysm proficiency learning include confusion about available training and frustration with time spent before discovering a practice route.[^10] This suggests explicit training affordances, mentors and readable prerequisites. It does not justify copying current community formulas without checking source. Reward purposeful practice and varied application; avoid making repetitive low-value crafting the dominant route to every profession.

## 6. Dreams are subjective spaces with real consequences

D&D Beyond's published Dream entry separates a sleeping target from a messenger in a trance and allows conversation and shaped dream imagery. Its published effect also distinguishes an optional harmful outcome from ordinary communication.[^11] This is a useful body/presence distinction. It does not describe a complete autonomous multiplayer dream engine, and the live page's specific metadata should not be treated as Beta's rules.

A Reddit discussion proposes using dreams as small roleplayed adventures, while also noticing the problem of other players sitting out and the possibility of powerful unintended political or harmful uses.[^12] The thread contains homebrew suggestions and disagreements. Its value is the set of design questions: who participates, what the dreamer believes, who controls imagery, what persists, and how a dream scene affects everyone else's time.

**Beta application.** A dream session derives its imagery from the dreamer's acquired memories, beliefs, desires and supported imagination. Remembering a lie does not turn it into a historical fact. Divine revelation or divination, if supported, is a separate capability with its own source and rules. Visitors acquire attributed impressions and testimony; corroboration determines what becomes justified knowledge.

The physical body retains one actor identity and resources. A dream avatar references it through an explicit tether with a permitted action set; it cannot perform a second simultaneous day's labor. Wake, injury, death, disconnection, encounter admission and closure resolve the tether once. Dream loot stays symbolic unless a declared conversion law transfers an eligible effect with real costs. Dreams can alter memories, affinities or learning only through supported effects and exposure rules. An imagined week should not automatically supply a week's rest, research or aging.

Author readable dream motifs from personality and recent experience while preserving enough stability for meaningful investigation. Avoid random incoherence as a shortcut to variety. A shy artisan might communicate through a recurring workshop, but a scene's symbolism should remain open to interpretation rather than becoming a guaranteed diagnostic label. Allow private, quiet dreams; not every night's sleep should trigger a mandatory adventure.

## 7. Planes, travel and systemic interaction

Minecraft's official Nether introduction presents a distinct hostile destination reached and left through portals.[^13] For Beta, the important model is a realm with different environmental and traversal rules, not merely an underground floor. The core must separately address physical elevation, a plane, a dream session and a historical branch.

A plane's rules declare permitted matter, visibility, movement, time mapping, entry and return. An ethereal-like overlap requires a spatial correspondence plus explicit observation and interaction rules. A different frontier plane can have its own terrain, habitats and cultures. A historical destination reconstructs a point in a chronology; visiting another plane does not automatically create another past. Combining these systems must not duplicate actors, erase possessions or produce an unrestricted shortcut around every puzzle.

Because time travel is a cornerstone, some mysteries will intentionally be solved by witnessing another time. That should be an earned solution, with identity, location, evidence and intervention consequences. Evidence from a divergent branch cannot automatically prove the same event occurred in the present branch. Dream testimony, divine claims and historical observations require different provenance in the journal.

## 8. Reddit synthesis and proposed experiments

The consulted threads are nonrandom samples selected to surface design risks. Search-indexed excerpts were used where direct Reddit retrieval failed; those cases are identified in the source notes. Votes are not population estimates. One Factorio calculation thread explicitly corrected its own initial upcycling math after comments challenged it.[^14] This is a reason to reproduce important mechanics, not a reason to reject useful player insight.

| Scenario | Required evidence |
|---|---|
| SK-01 — Same skill, different role | Player and capable NPC use a profession action with equivalent prerequisites, costs, world outcome and permitted knowledge; control/acting differs. |
| SK-02 — Three levels of understanding | Novice, growing and expert actors complete a craft and a puzzle; advanced insight helps while basic controls and essential warnings remain available. |
| SK-03 — Conserved production cycle | Repeat craft, split, repair, salvage, enchant, extract and recycle across restart; detect unintended value creation and retain actual input provenance. |
| SK-04 — Research graph abuse | Recursive effects, cross-realm targets and excessive fan-out receive a bounded result or explainable rejection without corrupting a world. |
| SK-05 — Ordinary craft, real story | Tea, a knitted repair or a written record changes an actual NPC interaction or quest state with native performance and no invented consequence. |
| SK-06 — Interrupted dream | Wake or endanger the body during a cooperative dream; resolve resources, memory, control and return exactly once under the selected timing policy. |
| SK-07 — False dream evidence | The dreamer believes a false accusation; imagery conveys that belief but neither UI nor a quest silently marks the accused guilty. |
| SK-08 — Simple controls after mastery | An expert returns to the concise view and delegates a lawful job without losing functionality or being forced into repeated formula editing. |
| SK-09 — Cross-time solution | A temporal investigation produces correctly attributed evidence and a valid solution or changed outcome; another branch's facts do not overwrite the target incident. |

These scenarios are specified, not executed. Human testing should examine comprehension, discovery, autonomy and the frequency of repetitive setup. Native artists and narrative authors should review the same replayed outcomes as the engine team. A skill is complete only when its action, consequence and expression work together across its supported catalog.

## Sources

[^1]: Joel Couture interviewing Petri Purho, [Road to the IGF: Nolla Games' Noita](https://www.gamedeveloper.com/game-platforms/road-to-the-igf-nolla-games-i-noita-i-), 13 March 2019. Primary developer testimony; no complete engine source audit.
[^2]: Nolla Games, [official release notes](https://noitagame.com/release_notes/), cumulative historical notes accessed 8 September 2026; recursion and always-cast entries inspected.
[^3]: Wizards of the Coast, [2024 Basic Rules: Spells](https://www.dndbeyond.com/sources/dnd/br-2024/spells/), live official rules accessed 8 September 2026.
[^4]: Koei Tecmo/Gust, [Atelier Ryza 3 official manual: Synthesis Flow](https://www.koeitecmoamerica.com/manual/71cYx2DZ/en/5200.html), undated manual, accessed 8 September 2026.
[^5]: CleverRaven contributors, [Proficiencies](https://docs.cataclysmdda.org/JSON/PROFICIENCY.html), live maintainer documentation, accessed 8 September 2026.
[^6]: CleverRaven contributors, [Practice Recipes](https://docs.cataclysmdda.org/JSON/PRACTICE_RECIPES.html), live maintainer documentation, accessed 8 September 2026.
[^7]: Wube, [Friday Facts #375 — Quality](https://www.factorio.com/blog/post/fff-375), 8 September 2023. Historical development explanation.
[^8]: Wube, [Friday Facts #387 — Swimming in lava](https://www.factorio.com/blog/post/fff-387), 24 November 2023.
[^9]: Reddit, [How do you guys creating crazy wands?](https://www.reddit.com/r/noita/comments/1dqsbc4), 28 June 2024; [Investing in cooking I get, but Tinkering feels OP sometimes](https://www.reddit.com/r/cavesofqud/comments/1e3fx3u), 14 July 2024. Indexed post/comment excerpts consulted; direct fetch failed. Anecdotal player experiences, not verified game formulas.
[^10]: Reddit, [Questions on proficiency learning](https://www.reddit.com/r/cataclysmdda/comments/1i7bf7s), January 2025; indexed excerpt consulted after direct fetch failed. Historical anecdote.
[^11]: Wizards of the Coast, [Dream](https://www.dndbeyond.com/spells/2619156-dream), live official spell body accessed 8 September 2026; community comments below it are not official rules.
[^12]: Reddit, [Don't sleep on the Dream spell](https://www.reddit.com/r/dndnext/comments/iclysr/dont_sleep_on_the_dream_spell/), August 2020; directly retrieved post and comments. Proposed tabletop interpretations, not implementation evidence.
[^13]: Mojang, [Visit the Nether!](https://www.minecraft.net/en-us/article/visit-nether-), historical official introduction, accessed 8 September 2026.
[^14]: Reddit, [I did the math on Quality recycling loops so you don't have to](https://www.reddit.com/r/factorio/comments/1gh03ps/i_did_the_math_on_quality_recycling_loops_so_you/), November 2024; directly retrieved, including author corrections. Calculations were not adopted or independently verified.
