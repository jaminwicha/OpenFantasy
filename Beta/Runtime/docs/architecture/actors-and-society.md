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
