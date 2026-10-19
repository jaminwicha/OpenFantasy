# NPC-01: Wren responds to delayed supplies

Technical/content preparation for CURRENT_ROADMAP's NPC-01, based on source at
`ed7e0342fca59d23116aac55168ee8bb2815104d`. Implementation of permanent cognition waits for the
PLAN-STATE decision/review exit. This document does not allocate a new profile, save field or codec,
claim a new NPC behavior already runs, or reduce recruitment, relationships, memory and offscreen scope.

## Player experience and causal spine

The player accepts Wren's existing six-wood delivery agreement but has not supplied the project yet.
Wren can explain that the guard materials have not arrived; this is a supply condition, not an invented
broken promise or deadline. If the player explicitly gives Wren a suitable real gathering tool, Wren
can choose to gather a small personal stock while waiting. Actual availability, known location, safe
reach, effort and custody determine whether that plan is feasible. If no tool is given, waiting or
asking for help is an honest outcome; the engine cannot conjure one.

The first successful qualification uses one small authored personal-stock target and the original
sapling-guard commitment. Personal stock remains Wren's, distinct from the project's reserved supply.
The player subsequently makes the original qualifying delivery from actual owned material; the current
quest's six-unit requirement and funded rewards remain unchanged. Wren learns of that real delivery,
reconsiders at a lawful boundary and resumes the actual funded construction. Returning after an absence
reveals earned stock and/or guard progress, and permitted conversation explains the causal sequence.
The first episode does not manufacture a new crop, completed windbreak or social relationship bonus.

Giving an item transfers it under the existing custody rule; it is **not a loan** and promises no
automatic return. If the content instead needs borrowing, a real loan/return-obligation contract must
be designed and qualified separately. Do not describe an outright gift as lending in UI/dialogue.
The offered branch must show that the player is giving up a tool; declining preserves an understandable
path through the ordinary source quest. The game must not require surrendering the only tool without
making that consequence clear or proving the remaining progression route.

## Existing bindings and actual gaps

| Concern | Existing source / usable behavior | Required extension or guard |
| --- | --- | --- |
| Person and commitments | `AdventureWorldAdmission` admits profile40 with retained Wren; existing tests check original people/resources and an unworked guard. `LocalConstructionController` requires completed delivery participation before proposing funded work. | Preserve that identity, tool, home, body, inventory, quest and project. An aspiration before funding is not an accepted funded work command. |
| Movement and action | `LocalNpcController` proposes ordinary movement/work under host control; `WorldStepProcessor` admits canonical batches. | New policy proposes through these same action/control/hold rules; no teleport, direct progress setter or alternate simulation. |
| Gathering | `GatherPlanner` checks exact actor/node/tool state, control, reach, manipulation, effort, finite source stock and custody. | Wren's admitted joinery kit is not a gathering axe. A usable axe must actually transfer under ordinary rules; feasible node knowledge cannot come from global stock scans. |
| Money and trade | The quest manifest starts Wren at zero personal gil. `LocalTradingController` uses own stock/balance and in-person inquiries, but attempts/configuration are session memory. | Do not spend the quest reserve as Wren's purse, assume an affordable purchase, or treat transient attempts as persistent cognition. Merchant inquiry is an optional contrast, not a required detour in this first episode. |
| Supply and reward | The pinned source quest requires six actual wood units,420 XP and200 funded gil; construction separately requires200 real work units. | Preserve original requirements and source pins. Wren's personal stock is not silently inserted into player delivery or rewarded a second time. Do not reset a completed saved quest. |
| Speech | `WorldActionResolver` addresses the speaker's `OpeningUtterance`, checks communication/body/comprehension/reach and opens a shared reading hold. `DialogueSession` binds the utterance definition and cause. | Existing speech is not a dynamic memory system. Add explicit versioned explanation/observation bindings without rewriting old utterances, dialogue IDs, historical meaning or accessibility behavior. |
| Knowledge | `ObservationScope` identifies person/world/branch; resource observations distinguish private source identity and permitted disclosure. | Reuse the provenance/audience principles, not the assumption that constructing an observation object authenticates it. A world snapshot is not an NPC's knowledge. |

The six-unit/200-work numbers are existing authored content, not new tuning or worldwide depth limits.
Use the actual selected current genesis and ordinary prefix; validate its unfinished quest, stock,
tools and route before a long run. Existing completed lineages remain preserved, never reset to stage
the new episode. If a selected origin cannot admit the prerequisites, report that limitation and select
an explicitly supported new qualification origin rather than editing a lived world into the desired state.

## Atomic concepts and responsibilities

Retain meaningful high-level domain concepts, composed from typed facts rather than a universal
property bag. These are proposed responsibilities; durable layout follows the reviewed ownership plan.

- **Evidence:** what the actor actually acquired, from whom/how, event identity/time, acquisition
  boundary, world/branch, recipient and permitted disclosure. Observed delivery and somebody's claim
  of delivery are different evidence. Owning a tool is directly knowable; remote merchant stock is not.
- **Belief:** a proposition, its supporting/contradicting evidence and uncertainty/staleness. Distinguish
  “no delivery observed” from “nobody has delivered,” and an unknown route from an impossible route.
  A fact changing elsewhere does not silently update this person's belief.
- **Motive/preference:** protecting saplings, keeping useful stock and respecting commitments. No new
  personality or alignment score is invented to justify one scripted branch. Authored preferences
  influence feasible choices; physiology, rights and actual resources still govern legality.
- **Intention and accepted commitment:** a proposed personal stock task is not a funded project,
  custody reservation, permission or paid action. Accepted obligations retain their real causal identity
  and can constrain replanning. Avoid duplicate acceptance after reload.
- **Decision/deferral:** selected next activity or a typed reason such as missing tool, no known source,
  insufficient effort, unreachable site, occupied opportunity, reading hold or existing commitment.
  Diagnostic reasoning is not itself a published world effect or public knowledge.
- **Memory:** retain the evidence/decision/outcome links needed to explain this episode and future
  relationships. A summary cannot discard unresolved promises, private provenance, outstanding rights
  or the difference between an attempt and its committed result.

Core owns these domain contracts, deterministic decision semantics and admitted transitions. Hosting
selects eligible work and publishes via the existing authority. The client renders permitted explanations
and semantic action/condition cues. Content defines motifs, supported tasks and constrained wording;
artist/audio work supplies accepted cues. No runtime LLM call, wall-clock seed or controller-private
permanent memory is introduced. Replay executes recorded commands and versioned transitions, not
unrecorded decisions from the latest policy version.

## Evidence and permission flow for the episode

1. Wren knows the aspiration/request they actually own and can inspect their own permitted holdings.
   Record the source of the unfinished-supply belief; do not infer a player promise from merely opening
   a conversation. The original quest has no new invented delivery deadline.
2. An actual accepted tool transfer can update Wren's self-knowledge from its committed outcome.
   A submitted or rejected gift cannot do so. Whole-candidate acquisition and relevant ownership effects
   must agree; do not publish a memory claiming receipt before the material transfer commits.
3. A gathering location must be directly observed under the admitted perception rule or deliberately
   communicated through an authenticated disclosure. Reachability queries establish physical feasibility,
   not permission to know every node. Revalidate live state on arrival; a previously known node can be
   depleted, inaccessible or occupied. Missing observation/range semantics remain an implementation
   dependency; do not pretend the present code supplies a general vision system.
4. Each gather attempt uses actual revision-bound intent, real tool and finite effort/stock. On commit,
   Wren learns its own result. Failure records only the information actually exposed to that actor;
   a generic rejection must not reveal a hidden rival's stock or secret route.
5. A real qualifying delivery provides source-backed evidence of the funded project. Direct involvement
   or a permitted later inspection/disclosure is required. Do not wake a distant NPC with omniscient
   world truth. Replanning preserves accepted in-flight actions and changes the next legal opportunity.
6. Dynamic conversation draws only on Wren's permissible evidence and chosen disclosure. Statements
   distinguish remembered facts, current observation and uncertainty. A UI reason code is not a new
   memory, revealed private motive, resource grant or social consent.

## Decision cadence, conflict and absence

Use deterministic event-triggered reconsideration plus a declared bounded fallback cadence in the
appropriate simulation domain. Relevant acquisitions, own-resource changes, a commitment becoming
feasible, invalidated route evidence and a finished action can wake evaluation. Do not run a planner
per rendered frame or make each actor earn an extra world interval. Persist enough policy/version and
causal state for the selected interpretation; transient route/search work is disposable.

Priority is conditional: an accepted feasible guard obligation takes precedence over optional personal
stockpiling; unavailable input defers it, not erases it. Do not abandon a paid combat/care/withdrawal
continuation mid-command. Host participation exclusions and canonical batch order remain in force.
Two NPCs choosing the same source cannot both spend stale stock; ordinary sequential admission resolves
it, and the losing actor learns only its actual outcome. Fair service is separate from deterministic
actor sorting and remains an explicit measured requirement.

Offscreen continuation uses the same owned state, acquired evidence and legal actions at declared
boundaries. Absence from the camera neither grants information nor stops due exposure. World time is
not laptop wall time. A distant approximation must show which boundaries it preserves and when it
falls back to detailed admission; it cannot skip a rival, hold, source depletion or pending obligation.
On return, reconcile exact identities, inventory/provenance, work, memory and temporal state. Do not
claim an offscreen proof from executing a permanently full resident world and merely hiding its sprite.

## Presentation and onboarding

Introduce one understandable choice at a time: Wren needs project supplies; the player can inspect a
specific proposed tool gift and its cost; Wren visibly approaches/works/rests; the later real delivery
changes the next task. Existing Pack/wheel/quest affordances remain primary. No debug cognition panel,
wall of motive bars or irrelevant menu is required. Optional expert inspection may explain learned
reasons without exposing private thoughts automatically.

Use the existing restrained classic movement direction: normal locomotion, a readable gather contact,
a brief rest/inspection and correct construction pose. Emit semantic events from actual committed
activity; art/animation never supplies progress. Avoid constant bobbing, repeated celebratory motion
or notification spam. Tool contact and footsteps are source-positioned, quiet and deduplicated;
conversation/reading holds and reconnect do not replay old transient cues. Artist/audio acceptance
remains unrun until real native assets and listening/motion evidence exist.

Illustrative explanation intent, **not new pinned source dialogue**: “I hadn't received the guard
materials, so I gathered some stock while waiting. Once the delivery arrived, I returned to the guard.”
Only select clauses whose facts actually occurred and are known; replace certainty with remembered or
reported wording where appropriate. Never claim a built windbreak because a small guard was completed.

## Implementation sequence and acceptance

1. Complete PLAN-STATE decision/review before the permanent persistence binding. Define lawful evidence
   acquisition and the typed actor cognition contracts against that ownership/rollout decision.
2. Bind the minimum evidence→belief→feasible-plan→ordinary-command→committed-outcome cycle. Reuse
   existing gathering/construction/custody rules and genuine source content; do not encode story beats
   as world facts. Include infeasibility and privacy contrasts before adding more plan types.
3. Add explicit versioned persistence/history and dynamic dialogue bindings, preserving all original
   profiles and source utterances. Atomic evidence/effect coupling and original-history reconstruction
   are required, not eventual afterthoughts. No broad codec rewrite or implicit migration.
4. Qualify the whole same-save episode with absence/restart and actual changed-place return. Measure
   decision cost, loaded dependencies, state/durable bytes, service latency and metadata separately.
5. Bind native action/animation/audio and accessible conversation; observe human understanding and
   sustained burden. Recruitment/companion contributions, STORY-01, broader merchant/caravan behavior
   and full relationship/personality depth remain additional P02/future obligations, not this case's completion.

| Acceptance contrast | Required evidence |
| --- | --- |
| Real preparation | Actual current origin, unchanged source quest/reward, genuine owned tool transfer, known lawful route and source. Preflight rejects synthetic stock, joinery-as-axe, invented funds and completed-quest reset. |
| Delayed/unknown evidence | Actor does not react to an undisclosed remote event; reacts after lawful acquisition. Belief can be stale without changing world truth. |
| Feasible alternative and reprioritization | Personal gathering spends actual effort/stock/time; funded guard becomes preferred only after valid evidence. Missing tool, inaccessible/depleted source and full custody yield honest deferrals. |
| Conflict and refusal | Two users/actors cannot duplicate a tool/lot, work or outcome. Changed revision/control/hold invalidates stale proposals without leaking hidden data or automatically retrying. |
| Same person and history | Save/restart/absence preserves identity, original commands/receipts, knowledge audience, intentions and memories. No duplicate reward, promise, accepted work or tool return. |
| Detailed/offscreen agreement | At declared boundaries, exact material/progression/work/exposure and meaningful cognition agree; named interruption cases and real residency limits remain visible. |
| Narrative/UI | Conversation explains the actual causal chain with permitted wording; no fixed response pretending to know new events. Player recognizes the changed plan/place without opening debug state. |
| Full scope retention | First episode is not full cognition, friendship, recruitment, politics or population-scale acceptance. Fixture goal/memory counts are workloads, not character-depth caps. |

## Documentation review

- **Plan/spec:** supplies the scheduled NPC-01 technical/content design without bypassing PLAN-STATE,
  changing pinned source quests or erasing later NPC/relationship/recruitment and P02 obligations.
- **Reference:** actors/society records actual fixed speech and transient host-policy boundaries plus
  timeless distinctions among observation, belief, gift/loan and accepted commitment.
- **Accuracy/completeness:** checked admitted tools/funds, ordinary gather constraints, work funding,
  dialogue/comprehension, observation scope and transient trading attempts. This is not an executed
  full causal episode or a persisted cognition implementation.
- **Checks:** [packet source/documentation record](p02/partition-state/npc-causal-design/RESULT.md).
