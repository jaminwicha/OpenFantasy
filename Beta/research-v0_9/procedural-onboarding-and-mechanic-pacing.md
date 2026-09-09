# Procedural onboarding and mechanic pacing

Research date: 8 September 2026. Project: **Beta**; predecessor: **Alpha OpenFantasy**. This paper supports the gradual-introduction requirement and [player experience contract](../spec-v1_1/PLAYER_EXPERIENCE_AND_ONBOARDING.md). It proposes retained validation, not completed playtests.

Beta should generate understandable invitations into its existing simulation. An ordinary local problem can teach one useful action while history, ecology and politics remain active around it. Approachable beginnings and sophisticated late-game coordination are compatible if the same actions acquire additional uses, collaborators and consequences. Full feature coverage does not require exposing every feature immediately.

## 1. What the primary evidence establishes

In a 2012 direct interview, *Super Mario 3D Land* director Koichi Hayashida describes levels organized around a clear idea: introduction, development, a changed application and a final demonstration. He also distinguishes getting a struggling player past an obstacle from helping them acquire the ability to overcome it. His account emphasizes early playable drafts and observing different people repeatedly; favorable questionnaire responses can conceal confusion. These are teaching and observation methods, not evidence that Beta should copy Mario's level lengths, time limits or assistance rewards. [Hayashida interview](https://www.gamedeveloper.com/design/the-structure-of-fun-learning-from-i-super-mario-3d-land-i-s-director).

Valve's 2008 *Portal* postmortem describes revisions driven by actual difficulties: losing the Companion Cube caused frustrating backtracking; designers removed troublesome elements, made the destination button visible, and tried environmental hints before dialogue. Incineration was introduced in a lower-stress context before its later importance. The transferable lesson is to teach the prerequisite action where its result is legible and recovery is affordable. Portal's controlled chambers do not establish that an arbitrary persistent world will preserve those conditions. [Swift and Wolpaw, developer slides, especially slides 24–30](https://cdn.fastly.steamstatic.com/apps/valve/2008/GDC2008_PortalPostMortem.pdf).

Nintendo's 2024 *Echoes of Wisdom* developer interview provides a complementary warning. Distinct, memorable functions helped players choose among many tools; developers removed restrictions that prevented a previously working solution from working elsewhere. They also adjusted delayed, passive outcomes to make the player's contribution feel immediate. This supports readable affordances and consistent alternatives. It does not authorize giving Beta's companions knowledge they lack or bypassing their actual work duration. [Nintendo, Chapter 2](https://www.nintendo.com/en-za/News/2024/September/Ask-the-Developer-Vol-13-The-Legend-of-Zelda-Echoes-of-Wisdom-Chapter-2-2659795.html).

Michael Booth's 2009 *Left 4 Dead* presentation separates threat frequency from threat difficulty and describes recovery intervals between intensity peaks. However, its population creation/deletion and survivor-intensity estimate serve a cooperative shooter. Beta can take the need for breathing room without importing disposable population management or treating one party-wide number as every person's readiness. Existing threats and commitments must remain truthful during quiet periods. [Booth, developer slides, especially slides 65 and 77–81](https://cdn.fastly.steamstatic.com/apps/valve/2009/ai_systems_of_l4d_mike_booth.pdf).

Smith, Whitehead and Mateas's 2011 **Tanagra** research combines authored geometry patterns, reactive planning and numerical constraints to generate playable platforming sequences. Its guarantees depend on a specified movement model and connections between action beats. It separates fast machine generation from human judgment of quality. That is useful architectural evidence for constrained scenario assembly; it proves neither human learning nor Beta's much larger resource, social and temporal model. [Author-uploaded paper, sections III, V–VIII](https://www.researchgate.net/publication/224242275_Tanagra_Reactive_Planning_and_Constraint_Solving_for_Mixed-Initiative_Level_Design).

## 2. A teaching grammar bound to real world facts

**Proposed Beta adaptation:** author teaching units with a concept, prerequisites, observable affordance, plausible motivation, recoverable first use, independent variation and later combination. Each unit also specifies alternative solutions, controls/accessibility explanations, permitted bindings, practical hazards and evidence of learning. An interaction is not taught merely because a notification appeared.

Bind units to actual people, places, objects, supplies and commitments. A household's damaged water trough can introduce inspecting condition, obtaining a permitted tool and performing a repair. The owner has a real reason to want it repaired; materials leave actual stock; repaired storage helps actual animals. A later irrigation task can reuse inspection and repair while adding flow control. Beginning with this action need not expose the entire fluid, soil, animal-care and property system.

A visible demonstration may be an NPC performing their ordinary job, provided the task is feasible and consistent with that person's knowledge and priorities. Teaching cannot freeze their other responsibilities indefinitely. If the player hires a specialist or solves the trough problem with a legitimate alternative, the world accepts that outcome. Offer future practice where useful; do not withhold the reward because the player omitted the designer's preferred gesture.

Generation should select compatible patterns, bind stable identities, validate them and retain accepted bindings with their world revision. Bound preparation work. On failure, try another compatible pattern or reject the uncommitted start under its declared profile. After play begins, do not reroll established people or conjure inventory to repair an invalid lesson.

## 3. Prove an opportunity, then observe understanding

Maintain separate graphs for teaching order and actual capability/progression dependencies. A supported-start witness must contain a feasible sequence of actions, including costs, quantities, permissions, travel, opening hours, season, actor availability and recovery. Account for consumables shared between steps: one plank cannot repair both a bridge and a trough. A geometric path to a shop is insufficient if it closes before arrival or sells an unaffordable prerequisite.

Represent alternatives explicitly. A winter introduction might use stored food, tool maintenance or a suitable local harvest, depending on the real settlement. Validate combinations of prerequisites together; individually modest obstacles can form an impossible chain. Do not label search timeout as proven impossibility, or an incomplete search as successful qualification.

The accepted witness proves that an opportunity exists under recorded assumptions. It cannot prove that players notice the cue, understand causality or enjoy the task. Observe native sessions with unfamiliar players: what they attempted, misinterpreted, repeated, asked about and subsequently transferred to a different situation. Agents can execute traces and identify contradictions; human acceptance remains necessary for clarity, comfort and discovery.

## 4. Quiet presentation and distinct forms of knowledge

Initially foreground one chosen local intention and its relevant actions. Keep nonurgent discoveries in an optional journal; bundle notifications and prevent queued explanations from erupting after dialogue or combat. Use a small, measured attention budget rather than treating every state change as a call to action. Rain, birds, workers and caravans can communicate a living place without generating four new objectives.

Practical feedback should answer what changed, what it cost, and what can be tried next. Optional detail explains causes as the character earns relevant knowledge. Player-facing language should describe the blocked action, not internal graph nodes. Expose real hazards early even when their deeper mechanism remains unfamiliar.

Separate assistance preferences and observed human familiarity from in-world expertise. Skipping inventory help cannot teach a new character alchemy. Conversely, an expert companion can perform a commission without forcing the human to learn every production formula. Re-enable help on request. A veteran's familiarity may shorten explanations, but cannot satisfy missing resources, skills or permissions.

Later play should combine familiar actions through genuine delegation: reserve household food, commission a tool, schedule repairs or ask a caravan to transport seed. Display meaningful exceptions and decisions. Mastery should increase useful choices and coordination, not mandatory acknowledgments of routine work. These are Beta design choices, supported by the owner's experience goals; no cited game proves their combined production cost or fun.

## 5. Changed worlds and mixed-experience parties

The introduction director selects eligible opportunities; it does not control NPC decisions or add another simulation clock. A mentor can depart, a bridge flood, supplies sell out or a historical branch change the relevant facts. Revalidate dependencies and find an honest continuation: another available teacher, a retained record, a different task or a recurring opportunity. Communicate actual unavailability when none exists. Preserve the event and its consequences.

Supported beginner starts constrain initial availability and risk. They cannot guarantee recovery after every voluntary destructive action. Equally, a routine introduction should not secretly demand six advanced disciplines merely because its causes involve six systems. An available specialist or simple response can provide an approachable route while deeper investigations remain possible.

For one to four players, keep guidance personal and shared commitments explicit. A veteran may demonstrate an action without marking all beginners proficient. A late joiner needs orientation to the current world, not a reenactment of the initial seed. Private tutorial overlays reveal only appropriate knowledge; shared encounters, rest and hazards obey the existing host/Wait contracts. Prevent overlapping guidance from drowning out conversation or environmental cues.

## 6. Retained qualification cases — all unrun

| Case | Required retained proof |
|---|---|
| **LEARN-01 — Grounded introduction and transfer** | Across supported biome, season and starting-role profiles, retain an actual observe/practice/independent-use/combination sequence with actors, stock, permission, costs, time and recovery witnesses. Include shared-consumable and shop-closing counterexamples. Native novice sessions must demonstrate understanding in a changed task; solver success and tutorial completion alone do not pass. |
| **LEARN-02 — Optional help and legitimate alternatives** | Ignore, skip and re-enable help; complete the initial problem through a supported alternative; change character and input device. Verify actual rewards and consequences, no unearned powers or knowledge, and a later opportunity to practice the bypassed mechanic. Test readable controller and accessible explanations without compulsory timed input. |
| **LEARN-03 — Different readiness in one world** | Four players with different familiarity and character knowledge cooperate, separate, reconnect and hand off control during teaching, rest and an encounter. Verify personal guidance, correct action/learning credit, scoped information, ordinary clock rules and no party-wide forced tutorial. Retain human observations of interruption and clutter. |
| **LEARN-04 — Changed opportunity and accumulated complexity** | Interrupt a lesson through mentor death/departure, flooding, resource consumption, season change and a historical branch. Retain truthful invalidation and feasible rebinding or stated unavailability, without replacement identities or free supplies. Repeat during later multi-system coordination; verify current actionable summaries, bounded prompt queues and understandable exceptions in extended native sessions. |

P02 should retain the first native introduction; P03 should exercise failure, alternatives and cooperative continuity before broad content production. P04–P10 extend the same grammar through real mechanics. P12–P14 qualify understanding, sustained attention, release completeness and mobile access. More patterns improve coverage only when their bindings and human-facing results remain testable.
