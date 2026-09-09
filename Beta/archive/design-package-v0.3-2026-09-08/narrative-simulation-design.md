# Narrative, people and quests in one simulated world

8 September 2026. Owner direction: deepen RogueFiction's narrative, questing and NPC lessons within Beta's more ambitious society, daily-life and building systems. The structures below are proposals, not implemented features. All original OpenFantasy content remains subject to the [complete migration requirement](/Users/alucinor/Workspace/Projects/OpenFantasy/Beta/content-migration/README.md).

The player promise is that a person has a life before asking for help, the problem exists in the world, and the solution changes that life. The quest log helps the player understand these relationships. It does not maintain a competing version of reality.

## What RogueFiction actually contributes

| Inspected evidence | Valuable principle | What Beta must add |
|---|---|---|
| [StoryNode, Choice and Effect](/Users/alucinor/Workspace/Projects/RogueFiction/src/story.rs:101) | Authored prose can expose conditional choices with explicit effects | Typed, versioned predicates over specific entities, relationships, resources and knowledge; effects committed through the simulation |
| [Run flags and oath identities](/Users/alucinor/Workspace/Projects/RogueFiction/src/story.rs:8) | Choices persist and can affect later scenes and mechanics | Durable promises with parties, subject, terms, scope, status and evidence; no universal 64-bit flag list |
| [NPC descriptions and Corvus's conditional return](/Users/alucinor/Workspace/Projects/RogueFiction/src/content.rs:768) | Specific character voices and a later return make earlier kindness tangible | People who travel, remember, work, form households and respond to events; distinguish authored appointments from autonomous daily activity |
| [Merrow quest and Corvus return test](/Users/alucinor/Workspace/Projects/RogueFiction/tests/playthrough.rs:390) | A recoverable object and an earlier choice connect distant scenes | Stable object identity, custody, acquisition evidence, alternative solutions and duplicate-safe rewards |
| [Noisy prying](/Users/alucinor/Workspace/Projects/RogueFiction/src/game/world.rs:941) | A useful action has consequences visible to nearby agents | Sound events, perception, source uncertainty, lawful access, property damage and witnesses using shared rules |
| [Monster hunting memory](/Users/alucinor/Workspace/Projects/RogueFiction/src/game/world.rs:62) | Pursuit continues from remembered information rather than omniscience | Separate personal knowledge, observation time, confidence and communication channels for people and creatures |
| [State-based descriptions](/Users/alucinor/Workspace/Projects/RogueFiction/src/v2/app2.rs:1023) | Description derives from nearby state; ambient variety avoids consuming authoritative RNG | Grounded descriptions, visual/audio cues and accessible text equivalents with the same disclosed information |
| [Exploration-verb tests](/Users/alucinor/Workspace/Projects/RogueFiction/tests/v2_flow.rs:139) | Looking, sensing, searching and resting have deliberate information/time costs | Explicit action-duration rules in multiplayer; reading a journal must not accidentally advance or pause the shared world |
| [SavedLevel](/Users/alucinor/Workspace/Projects/RogueFiction/src/game/world.rs:156) | Return visits preserve altered objects and places | Actual disk durability, continuing distant simulation and branch-specific identity |

RogueFiction does **not** demonstrate an autonomous household economy, general social planner, durable database, or arbitrary generated quest solver. Its NPC agendas are mostly authored dialogue and placement, and its visit persistence freezes levels in memory. Its active listen/smell actions consume turns; “ambient description does not touch RNG” does not mean all sensing is free. Beta should deepen these real strengths rather than attributing an already complete society engine to them.

## Shared state, different views

Use a small set of related runtime records, owned by the deterministic simulation:

- **Person:** identity, life state, household, affiliations, skills, possessions, current work/activity and bounded personal goals. Start with a few legible needs; do not require a psychology simulator before an NPC can act.
- **Relationship:** directed trust, affection, grievance and obligation with reasons and participants. These are distinct dimensions; paying a debt need not produce friendship, and friendship need not erase a grievance.
- **Knowledge:** a claim, subject, source/witness, observation time and certainty. World truth and a person's belief are separate. A rumor may be outdated or mistaken, but its origin must be explainable.
- **Work order:** intended output/service, required materials/tool/site/skill, reservations, progress and beneficiaries. Repairing a mill is work in the world, whether performed by a player or an NPC.
- **Commitment:** the actual promise: issuer, acceptors, beneficiaries, bound targets, success/failure/cancellation terms, deadline policy and compensation. The quest UI presents this record.
- **Story instance:** a versioned authored scene or pattern, concrete role bindings, history dependencies, current stage and relevant knowledge. It can add expressive beats to a real commitment without duplicating its inventory or completion state.
- **Memory:** a bounded retained interpretation of significant events, linked to factual evidence. Summaries can compress presentation, but must not delete unresolved debts, kinship, ownership or quest prerequisites.

A change to the world emits typed consequential events after a transaction. Interested systems update a bounded set of affected records. Avoid scanning every quest against every NPC each tick. A deterministic scheduler can work on coarse activity intervals in distant settlements, while preserving named people, commitments and material totals during activation changes.

Let meaningful history appear in objects and places: a repaired family tool, a blocked older doorway or a ward reused in a newer building can become inspectable evidence. The [history and materials brief](/Users/alucinor/Workspace/Projects/OpenFantasy/Beta/art-style-calibration/history-and-materials.md) distinguishes cosmetic atmosphere, recorded evidence and functional condition. A generated description must not turn decorative scratches into invented historical facts.

This proposal combines the research lessons deliberately: Dwarf Fortress's linked consequences and material specificity; Stardew's comprehensible daily life, distinctive voices and authored relationship moments; Terraria's meaningful terrain alteration and tool/material progression; RogueFiction's sensory discovery and memorable callbacks. These are Beta design inferences from the [reference study](/Users/alucinor/Workspace/Projects/OpenFantasy/Beta/reference-architecture-research-2026-09-08.md), not claims that the four games share an internal quest architecture.

## Persistent relationship networks and long story arcs

The owner explicitly requires deep persistent stories and evolving affinities both between the player and NPCs and among NPCs themselves. Relationships are part of the full society simulation. The player is not the hub of every connection or the sole cause of social change.

Represent relationships as directed, contextual records between stable people: affection, trust, respect, fear, resentment, loyalty and obligations as appropriate to the character and content. Affinities for activities, values, communities and places influence choices without being identical to interpersonal friendship. Reciprocal feelings can differ. Kinship, household membership and institutional roles are explicit ties, not deductions from one numeric friendship score.

Significant interactions carry causes and memories. Shared work, care, rivalry, promises kept or broken, discoveries, gifts, conflict and absence can affect different dimensions. Gifts are one possible interaction, not a universal affection engine. An NPC may initiate a visit, help a friend, avoid a rival, seek reconciliation or change an alliance through legal actions and available time/resources. Distant simulation must preserve these named relationships and unfinished commitments rather than regenerate them on return.

An enduring story arc is a persisted versioned instance with bound participants, phases, prerequisites, important memories, unresolved tensions, promises, witnessed outcomes and continuation rules. It can span seasons, travel, recruitment, households and generations where supported. Authored scenes provide distinct voice and pacing; systemic changes determine which scenes and choices are truthful. A threshold may make a scene eligible but must not erase the reasons for the relationship or make every person follow the same ladder.

Define what happens when participants die, migrate, separate, change allegiance, fulfill a commitment independently or occupy different branches. Preserve completed history and use explicit continuation, inheritance or closure where appropriate; do not silently reroll a missing participant. Canonical facts, individual interpretations and player-visible accounts remain distinct. Ordinary summarization cannot delete unresolved relationship/story dependencies.

Performance reads this state: familiarity changes greetings and distance, mistrust changes gaze or hesitation, affection changes selected gestures, and old disputes affect how people share space. The [scene director](expressive-animation-and-choreography.md) stages those performances without fabricating relationship changes. It must also support NPC-to-NPC conversations the player can witness, with observations limited by actual access.

**Retained proof:** use at least three named people with asymmetric ties and an authored multi-stage arc. Let two NPCs interact while the player is absent under the declared world-time policy, then return to visible behavioral and narrative consequences. Save/restart mid-arc; recruit or relocate a participant; test a disputed memory and one branch intervention. Relationship changes and scene rewards occur once, named ties survive coarse/local transitions, and each person remains more specific than a procedural role label. This extends the first Wren/community proof without requiring a complete social simulator before any useful interaction can run.

## Three layers of quest authorship

**Authored inheritance.** Port all 45 existing quests, original offers, turn-ins, prerequisites, rewards, givers and target identities. Their current paths remain recognizable and available. A compatibility adapter first proves the original graph; richer scenes and alternate outcomes are then attached to the same content identities. A requirement to incorporate every original quest is not fulfilled by replacing them with generic bounties.

**Contextual story patterns.** An authored pattern defines the kind of conflict, required roles, valid world facts, available approaches, voice constraints and consequential outcomes. A binder selects actual people, places and objects deterministically. It checks feasibility before making an offer and revalidates when accepted. Preserve the selected bindings; never reroll the promised person or object after restart.

**Discovered opportunities.** Real shortages, damage, migration, promises and disputes can become visible requests when someone has motive and means to act. Not every low stockpile becomes a quest. Limit urgency and repetition, respect the player's interests, and let NPCs solve some problems themselves. Successful society simulation should not trap the player in an endless queue of chores.

Candidate selection and dramatic pacing can be deterministic. Optional language generation may realize a constrained voice or propose a scene; it does not invent the inventory, guilt, relationships, rewards or historical facts. Validate and record accepted proposals as inputs. The game must remain coherent and playable with that service absent.

## Example: Wren's Saplings becomes part of a living community

The original `wren_orchard` quest requests six wood for a windbreak. Preserve that offer, giver, six-wood completion path, original text and reward as imported content. The following expansion is a design proposal, not preexisting lore.

Wren's orchard has exposed saplings; a recent storm damaged the shelter, and the household needs the harvest for an existing obligation. The player can learn about the damage by speaking with Wren, seeing broken boards, or examining fresh storm debris. The request is grounded in a real site condition and work order. A neighbor may know why local timber is scarce; the player need not discover every explanation to help.

The legacy delivery completes the promised supply task and creates or advances a repair work order. Materials enter a reserved stockpile; they do not vanish into a quest flag while an unrelated free structure appears. If the player helps build the windbreak, terrain/structure changes affect the orchard's protection through the same building rules used elsewhere. A smith or carpenter can contribute labor, creating a small social connection rather than another mandatory errand.

Alternate approaches might use purchased timber, salvaged boards of adequate quality, or a different approved shelter design. These require explicit acceptance conditions. Unrelated scrap does not count merely because generated dialogue says it will. A previously completed windbreak should be recognized; if the player already solved the problem, Wren can offer acknowledgment or a revised agreement rather than demand redundant deliveries.

Later, Wren can remember the help, share produce, recommend the player to a relative or supply an expedition. An authored scene supplies the emotional specificity; world records explain why it can occur. A preserved sapling, board inscription or heirloom tool can carry history across years. A branch intervention changes that branch's harvest and memories, not everyone else's.

This is a stronger combination than attaching friendship points to a fetch quest: farming, construction, material ownership, authored characterization and remembered action all participate in the same outcome. Keep most of that machinery behind readable dialogue and scene feedback.

## Additional opportunities worth prototyping

| Pattern | RogueFiction lesson | Connection to Beta and original content |
|---|---|---|
| The person who returns | Corvus responds to mercy later | A rescued miner becomes a resident or craftsperson and later assists on a route; respect travel, injury and existing ties |
| An object with a name | Merrow's recovered lid and the Archivist's identities | A family tool, machine part or named weapon has custody and provenance; a better replacement can solve utility without pretending it is the original heirloom |
| The promise that costs something | Oaths alter choices/mechanics | A smith commits scarce material to Brasswing repairs while another community needs it; show terms and alternatives before commitment |
| Discovery through the environment | Examine/listen/smell/search | Forge noise, damp air, smoke or unusual growth points toward real workings, leaks, passages and hazards; text and visuals disclose consistent information |
| A secret with consequences | Prying wakes nearby threats | Opening a locked workshop exposes a record, damages property and may attract witnesses; permission, stealth and repair offer different resolutions |
| A settlement remembers | Flags, returning NPCs and changed fixtures | A repaired water route changes work and dialogue; a harmed household retains a specific grievance instead of a universal reputation penalty |
| History inside a material | Relics, names and the deeper vigil | Aethertech ruins and original dungeon rewards bear inspectable evidence of the war; an intervention changes actual lineage and ownership |

Import named RogueFiction characters or whole arcs only if they fit the chosen world treatment; the requirement here is deeper use of its concepts, not compulsory transplantation of every setting element. Original OpenFantasy content already inherited from RogueFiction is counted once in the migration.

## Failure and fairness rules

Bind roles to stable people and durable objects. If a quest giver dies, migrates or changes allegiance, apply a authored/typed continuation policy: estate or household handoff, renegotiation, a discoverable cancellation, or a changed story. Do not silently respawn the same person, transfer a personal vow to a stranger, or make an irreplaceable target disappear without a comprehensible outcome.

Revalidate reserved goods and completion predicates in one transaction. Two players turning in the same unique heirloom cannot both consume it or receive an exclusive reward. Decide explicitly whether credit belongs to a person, party, household or world. Overlapping quests may share evidence while maintaining separate obligations. A player's promise cannot bind an absent friend's inventory without permission.

Time limits use declared world time and a clear hosting/offline policy. A cozy relationship scene and a crisis need different urgency; the player should know when delay matters. Avoid arbitrary deadlines on inherited content that had none. Distant simulation may solve a problem before the player arrives; record that result and let the commitment adapt instead of fabricating a new identical crisis.

Terrain destruction must not strand a campaign silently. Support rebuilding, alternate access, relocation by actual travel, or a clear changed outcome. Do not use hidden invulnerable geometry everywhere merely to make quest scripts easy. Reserve invulnerability for explicit world rules or a deliberately bounded authored scene.

## First proof and acceptance

Use one imported quest, two households, one workshop, one environmental fault and one authored return scene. Add one sensory clue and one optional approach, then play through it in both solo and two-player sessions. The player should be able to explain who needed help, why, what changed and how someone remembered it.

Test that: imported text/IDs and original completion path remain available; offers refer to actual reachable people/resources; active observation has deliberate time cost; narration leaks no hidden facts or RNG side effects; duplicate turn-in is safe; accepted quest bindings survive restart; NPC relocation and target destruction produce legible continuations; distant simulation preserves material totals and identities; branch changes remain isolated; and the same checkpoint, versions and accepted inputs produce the same authoritative result. These are proposed tests, not outcomes already established by RogueFiction's 57 passing tests.

Measure authoring effort and playtest comprehension before expanding the vocabulary. Reject patterns that merely create repetitive chores, require a unique subsystem for every quest, or bury the character beneath simulation bookkeeping.
