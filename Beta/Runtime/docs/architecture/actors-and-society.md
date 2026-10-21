# Actors and society

## Domain contracts and runtime bindings

[Actors](../../src/OpenFantasy.Core/Actors) defines bodies, parts, dimensions, injuries, functions
and form bindings. [Language](../../src/OpenFantasy.Core/Language) defines language/register
comprehension requirements and query results. [Observations](../../src/OpenFantasy.Core/Observations)
defines scoped resource observations and knowledge projection. These contracts establish typed
boundaries; they do not themselves provide a complete psychological, language-learning or lifecycle
simulation.

[WorldResident](../../src/OpenFantasy.Core/Worlds/WorldResident.cs) owns the controller, home,
comprehension, experienced time and inventory reference.
[ActorPresence](../../src/OpenFantasy.Core/Spatial/ActorPresence.cs) owns the form binding and body
condition; world admission joins those records by identity.
[Progression](../../src/OpenFantasy.Core/Progression) owns admitted class/stat
profiles, level progress, reward rules and currency ledgers. The farm content binds actual source
definitions and a funded delivery agreement rather than manufacturing display-only XP or money.

[Actor resources](actor-resources.md) defines named immutable damage, restoration, MP expenditure
and reward transitions over that same progression owner. Restorative item composition couples exact
material consumption and capped HP changes without making either candidate a complete treatment
authority or a second health store.

[Physical encounter actions](encounter-actions.md) use those same progression resources for damage
and MP costs. Explicit ability grants require actual functional anatomy, including `NaturalWeapon`
for the authored wolf's teeth. Missing ancestors disable their descendant functions. Zero HP excludes
an actor from combat choices without destroying its identity, possessions or body; it is incapacity,
separate from the complete lifecycle contract below. Encounter sides are local assignments, not
inferred species hostility or moral alignment.

[LocalNpcController](../../src/OpenFantasy.Hosting/Simulation/LocalNpcController.cs) proposes
bounded home wandering. [LocalConstructionController](../../src/OpenFantasy.Hosting/Simulation/LocalConstructionController.cs)
proposes legal routes and work with the actor's real tool and commitment. The shared world processor
adjudicates those proposals under the same body, movement and resource rules as player actions.
Committed samples are replayed without rerunning controllers. These policies do not model complete
desires, relationships or institutions.

## Runtime binding: speech and host policy memory

[WorldActionResolver](../../src/OpenFantasy.Core/Worlds/WorldActionResolver.cs) resolves addressed speech
from the target resident's `OpeningUtterance`. Reach, functional vocalization/hearing and conversational
comprehension govern admission; the resulting [DialogueSession](../../src/OpenFantasy.Core/Worlds/DialogueSession.cs)
binds the utterance definition, participants, cause and shared reading hold. This fixed opening binding
is not a general evidence-dependent dialogue or persistent cognition system.

[LocalTradingController](../../src/OpenFantasy.Hosting/Simulation/LocalTradingController.cs) uses configured
host preferences, own holdings/balance and in-person inquiries. Its attempt bookkeeping is session
memory; it is not a saved belief/goal history. Ordinary world commands still authorize and commit every
material or monetary effect. Reopening does not turn transient policy advice into a persistent person facet.

## Runtime binding: cognition interpretation (profile 42)

[WorldCognitionAdmission](../../src/OpenFantasy.Core/Worlds/WorldCognitionAdmission.cs) creates profile 42 only
from an unplayed profile-41 world. The root is profile 41's complete frame plus one final section: the
[cognition facet](../../src/OpenFantasy.Core/Cognition/CognitionSnapshot.cs), with exactly one record per resident,
players included. A record holds the actor's acquired evidence, its current intention, the
[current deferral](../../src/OpenFantasy.Core/Cognition/CognitionIntention.cs) beside that intention (reason, the
policy version that first gave it, and since when), remembered decisions and the pending wake. Its codec maximum is reserved inside the 16 MiB root budget, so a record growing within its own
bound can never make the root unencodable. The root also stores the interpretation's exact `RulesPin`, and decoding
refuses any other. The interpretation is internal and non-default. Content still creates
profile 40, and no existing save changes profile.

[WorldCognitionLaw](../../src/OpenFantasy.Core/Worlds/WorldCognitionLaw.cs) holds the laws the profile replays:

- **Evidence comes from committed outcomes.** The step processor derives evidence after an action has passed
  complete-candidate admission, beside the material-evidence capture, and publishes it with that action's
  outcome. If the action is refused or rolled back, nobody acquires anything.
  - A gift by direct handover or a settled proposal gives the recipient `ItemReceived`. Consented pairs exist only in
    profile 28, so their branch is unreachable here.
  - A completed delivery gives the quest giver `QuestDeliveryReceived`, because the giver takes it in person.
  - A gather gives the gatherer its own yield and a fresh look at the source it harvested.
  - An inspection gives the inspector a `SourceObserved` fact.

  Evidence identities derive from the causing command and a fixed per-kind ordinal. They join the world's identity
  index and the batch's reserved identities.
- **Retention replaces what nothing cites.** A newer `SourceObserved` replaces the actor's older observation of the same
  source, and a newer `GatherOutcome` replaces its older outcome there, when no intention or memory cites the older
  entry. This is the market-knowledge rule: the replaced value stays in the journal and archived roots, and canonical
  state holds the actor's latest view. Cited evidence is always kept, received items and deliveries are never
  replaced, and the bound is checked after replacement. A root holding two uncited entries of one replaceable kind
  and subject is not canonical and does not decode. Beliefs therefore read each known source's latest observation and
  latest own yield; an actor's lifetime gathering total is history, not a held belief. A gathering completion is
  admitted while a yield since adoption is held, but nothing cites that yield, so after the next gather what justified
  the completion is recoverable only from the journal. Replaced identities stay reserved in the database.
- **Perception rule.** [InspectGatherSourceIntent](../../src/OpenFantasy.Core/Worlds/WorldCognitionIntents.cs) is a
  deliberate look at one source within four cells. It uses the same anchored terrain and structure sightline as
  gathering reach, needs no tool, and reveals only that source's visible remaining harvests. Nothing farther, and
  nothing behind an occluder, is observed. Sight is tested before anything else about the source. An absent source
  and one out of sight give the same refusal, and a stale revision is a conflict only for a visible source. This is
  the first admitted perception rule, not a general vision system;
  [WorldCognitionPerception](../../src/OpenFantasy.Core/Worlds/WorldCognitionPerception.cs) publishes its range.
- **World truth wakes nobody.** A change elsewhere, such as another person harvesting a known source, leaves every
  other person's evidence, derived beliefs and wake untouched until they acquire new evidence.
- **Own work is a self-owned fact.** Progress on an actor's own project is read directly, so a work step is not
  evidence. The step that completes the project schedules the builder's `ActionFinished` wake.
- **Wakes and suppression.** Evidence makes reconsideration due at the advancing step the batch produces, unless an
  earlier wake is pending. Every committed gameplay boundary records its condition on pending wakes: a reading hold,
  an encounter, or a paid continuation such as pending care. Due step and cause are kept, and this never writes a
  commit of its own.
- **Recorded decisions.** [RecordCognitionDecisionIntent](../../src/OpenFantasy.Core/Worlds/WorldCognitionIntents.cs)
  is the actor's own action at an advancing step. It carries the complete decision: policy version, intention,
  deferral, next fallback wake and completion flag. Admission requires:
  - the actor's own control, with no delegated witness;
  - no reading hold, including one opened earlier in the same batch;
  - the expected cognition revision;
  - a due, unsuppressed wake;
  - a fallback within the declared 100-step cadence;
  - a decision that changes something;
  - support from the actor's own evidence and project. A new intention is adopted at the current instant. Waiting
    and working name the actor's own unfinished project. Working cites a received delivery for that project's
    quest, and gathering cites an observation of its source. Completion is recorded only for a completed project,
    or for gathering at its stock target that has yielded since it was adopted.

  Deferral reasons and the stock target are the planner's claims; the law does not validate them. The decision is
  then applied exactly as recorded.
- **Deferral memory.** A decision that keeps the intention but gives a new reason replaces the current deferral and
  adds one `Reconsidered` memory with that reason, so the actor's own state explains why it is still waiting. The same
  reason again keeps its original policy and instant and adds nothing. Without an intention nothing is deferred, and a
  reading hold is never a recorded reason. Replay applies the same bytes and never runs a policy, so a
  changed planner cannot rewrite history.
- **Overflow stops the authority.** A capacity overflow propagates out of the processor. It is not a refused action:
  `CognitionCapacityException` derives from none of the exceptions admission converts into refusals, and commit
  preparation does not convert it into a dispatch refusal. `SqliteWorldAuthority` marks itself as
  requiring reopening, and nothing is published. The host policy proposes a decision whose own preflight overflows,
  so a live session reaches this route too and ends with the overflow.

Profile-42 roots use storage schema twelve, like profiles 40 and 41.

**Remaining bounds:** looks and gathers no longer approach the 64-entry evidence bound, because each source keeps at
most one uncited observation and one uncited outcome per actor. Evidence still grows with items received, deliveries
(one per quest), sources first seen and each citation, and memory grows with every intention change and reason change.
Reasons are planner claims the law does not yet check. One adoption
followed by 63 reason changes fills the 64 memories, for example under a planner that alternates two reasons. Decision 7's
second half, moving closed episodic memory into immutable actor-owned segments, is not bound; the decision-3
baseline measures how fast both bounds fill in sustained play.

[LocalCognitionController](../../src/OpenFantasy.Hosting/Simulation/LocalCognitionController.cs) is the host policy for
profile 42. It drives host-controlled residents that have an unfinished project of their own. For each such
resident, in order, it proposes:

1. a due decision, computed by `CognitionPolicy` from the resident's own tools, stock, effort and known sources. While a
   gathering plan's next swing waits for effort, the policy keeps the plan and its reason, so waiting adds no memory;
2. a pre-planned gathering swing and ordinary approach movement, for a gathering intention;
3. a look at the first source in sight that the resident has never observed.

[LocalNpcController](../../src/OpenFantasy.Hosting/Simulation/LocalNpcController.cs) takes a profile-42 builder's
funded commitment from its own `WorkFundedProject` intention, not from quest participation. A builder therefore never
starts funded work it has not learned of. Earlier profiles keep the construction controller's quest read. The player
adapter refuses recorded decisions as player requests. Physical feasibility, such as ground routes and the gather
planner's preflight, is host advice that avoids futile commands; it never becomes a belief. Residents without such a
project, including players, are not driven, and their due wakes simply age.

## Design contract: one person, several forms of control

The [world composition contract](world-composition.md) distinguishes facets, capabilities, typed
relations, activities and causal observations. Treatment, self-use and passive effects compose those
owners without making health, consent or relationships properties of a combat UI acknowledgment.

Player and NPC depth shares one character model: anatomy, traits, capabilities, needs, possessions,
learning, experiences, relationships, commitments and history. Controller permission and decision
cadence vary; intrinsic capability does not depend on whether a human is steering. Detailed and
distant simulation preserve each person's actual state and feasible obligations.

Bodies expose functional requirements to actions and equipment. Injury affects applicable functions,
movement and presentation. Rare permanent injuries and common scars retain causes; appropriate
restoration can heal functional loss under declared rules. Regrowth, transformation and resurrection
cannot duplicate items or erase the causal record.

Classes/jobs grant versioned proficiencies, learning and specializations. Professional employment and
political office are separate relationships. Changing a class preserves learned state under explicit
rules. Powers and mundane skills use common action contracts, so a magic school, musical performance
or acrobatic manoeuvre has real costs, targets, consequences and animation semantics.

## Design contract: cognition, relationships and world events

World truth produces scoped evidence; evidence informs beliefs; beliefs and preferences inform
feasible plans. An NPC reacts to a political event through what they can learn and do. A secret
succession cannot instantly alter everyone's dialogue through an omniscient event broadcast.

Needs, desires, motivations, likes/dislikes and memories remain distinct. Planners first enforce
capability, resources, rights and commitments, then evaluate expected relief, preference, risk and
social consequences. Personality changes thresholds and habits; experience can revise priorities.
Plans have bounded search and persistent reasons for deferral.

Cognition is persistent actor state rather than a controller's private working memory. Evidence
records identify their source, observed event/time and permitted recipient; a belief records what the
person infers, not an unqualified copy of world truth. Motives and preferences inform proposed plans,
while accepted commitments reserve actual roles, resources and time through the ordinary authority.
Dialogue projects the person's permitted knowledge, memory and reasons without manufacturing an action
or leaking facts outside the permitted conversation. Deliberate revelation or deception follows explicit
social rules. Memory summaries and retention rules must preserve the causal
obligations and evidence needed for relationships, mysteries and historical reconstruction.

Decision cadence, search work, resident bytes and durable bytes are distinct budgets. A distant actor
keeps identity and meaningful state while bounded due work advances at declared reconciliation
boundaries. Returning to detailed simulation cannot grant unearned time, complete an impossible job or learn
unobserved events. The [partitioned-persistence contract](persistence-and-history.md#design-contract-partitioned-persistence-and-version-ownership)
applies to cognition, relationships and commitments just as to physical custody. Fixture counts for
memories, relationships or goals describe a measured workload; they do not define maximum character
depth. These are design obligations, not capabilities implied by a routed amble or construction policy.

Relations are directed, with trust, affection, respect, fear, grievances and obligations. Friendship,
romance, household, party, religion, guild and nation are distinct relationships. Recruitment and
joint actions reserve actual roles and time; one companion cannot simultaneously adventure and work
a full remote shift. Other NPCs form relationships and pursue commitments independently of the player.

Narrative arcs bind real participants and resources. Adventurelets and encounter decks propose
eligible opportunities; a director cannot force actors into roles that contradict their state.
Multiple seed-dependent grand narratives can coexist. A rising dark power can be defeated, joined,
ignored or confronted by another person through ordinary world consequences.

## Design contract: ancestry, culture and institutions

Species and subspecies distinguish ancestry, cosmic affinity distributions, individual variation and
acquired culture. Humans, elves, dwarves, gnomes, halflings, dragonkind, quicklings, giants and diverse
animal-like peoples use extensible definitions rather than a fixed inheritance tree. Birth pins
parentage, historical context and variation. Training, migration, vows and transformation can change
an individual without implying that species determines moral worth or destiny.

Language, dialect and cant affect comprehensible evidence and social affordances while preserving
playability. Localization does not grant character knowledge. Partial understanding, translation,
learned vocabulary and alternative routes provide readable access; inaccessible wording cannot hide
basic controls or fabricate knowledge. Dreams can reflect beliefs without proving them true, and
entering a person's dream realm requires an explicit capability and realm transition.

Cultures influence construction grammars, room use, settlement layout, clothing, possessions, weapon
and armour lineages, tools, magical traditions and the balance of martial and magical technologies.
These are functional and historical relationships as well as visual palettes. Cultural affiliation
can influence social expectations without automatically rewriting individual preferences or old
buildings. Migration, conquest and exchange can leave layered styles and hybrid craft traditions.

Institutions own offices, mandates, laws, membership, taxes, claims and treaties. Overt and covert
alliances are distinct from public reputation. A mandate names its issuer, scope, delegation and
survival policy; cycles and unauthorized delegation reject. Construction ownership, claimed territory,
recognized sovereignty and effective control are separate. Building a castle alone grants no nation.
Alignment represents declared ethical/order axes, reputation and cosmological obligations separately.

## Design contract: lifecycle, death and the divine

Biological aging and development follow applicable exposure, often much slower than the calendar
impression of daily play. Birth creates one identity and actual care obligations. Marriage, consent,
legal recognition, kinship and household membership remain distinct. Different species need appropriate
development and reproduction profiles.

Death commits once, revokes living-body actions and personal office powers, interrupts applicable work
and places possessions in explicit remains/estate custody. Succession, inheritance, dependants and
unfinished promises have continuations. An office mandate may survive its holder under its own policy;
the deceased cannot continue exercising revoked personal authority.

A ghost or other eligible afterlife form continues the same person through an explicit transition
with its own senses, capabilities, control and containment. Echoes and illusions are distinct entities.
Resurrection restores identity without cloning property or automatically reversing inheritance and
remarriage. Deities have domains, limits, knowledge, institutions and accountable interventions;
divine action is not unrestricted administrator mutation.

The [technical design](../../../spec-v1_1/TECHNICAL_DESIGN.md) and
[language and afterlife contract](../../../spec-v1_1/LANGUAGE_AND_AFTERLIFE.md) own the detailed rules.

## Design contract: causal plans and explanations

Evidence acquisition, belief revision, proposed intention and accepted commitment have distinct meaning.
A source event affects a person's knowledge only through lawful acquisition; a complete world snapshot
is not that person's knowledge. Self-owned facts and directly received outcomes differ from testimony,
remote truth and inferred belief. Rejection reasons reveal only information permitted to the actor.
Dynamic explanation preserves those distinctions and cannot create a resource, grant consent or expose
private motives merely because a planner used them internally.

A proposed alternative must satisfy actual capability, tool, stock, effort, custody, route and control
requirements. A joinery tool does not confer gathering capability, and a quest reward reserve is not
the giver's personal purse. Existing item gifts transfer custody under their actual rules; describing
one as a loan requires a separately represented and admitted return obligation. Accepted funded work
and paid in-flight actions cannot be discarded merely because an optional intention becomes attractive.

Reconsideration follows versioned simulation policy and relevant acquired evidence; rendered frames
and wall-clock time do not award opportunities. Meaningful cognition persists under the chosen authority
and history contracts, while disposable search/route caches do not become a second person model.
Offscreen reconciliation preserves actual knowledge, rights, resources, commitments and exposure;
forgetting an obligation or knowing an undisclosed event is not a valid simplification.
