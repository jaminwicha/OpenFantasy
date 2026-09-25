# OpenFantasy architecture

OpenFantasy is a persistent, seeded world simulation presented through a Godot C# game client.
A portable C# domain core owns game rules. An in-process host serializes and durably commits
decisions. Presentation translates permitted world observations into controls, images and sound.
Players and NPCs participate through the same rules; changing a controller does not create a
different kind of person.

## Reading the reference

| Subject | Questions answered |
| --- | --- |
| [Runtime and authority](runtime-and-authority.md) | Which module owns state? How does an action become durable? Where do adapters belong? |
| [Identity and determinism](identity-and-determinism.md) | Which bytes, numbers, random streams and orderings define the same world? |
| [Persistence and history](persistence-and-history.md) | What is stored, replayed, migrated and addressed through history? |
| [Time and encounters](time-and-encounters.md) | How do calendar, exposure, Wait, rest and host readiness interact? |
| [World and geography](world-and-geography.md) | How do terrain, watersheds, streaming, planes, geological history and ecology fit together? |
| [Local worksite](local-worksite.md) | How do funded building, multilevel public geometry, contextual mining and a separate save entry connect? |
| [Local watercourse](local-watercourse.md) | How do finite flowing water, public cell geometry and the inhabited farm connect? |
| [Local excavation](excavation.md) | How does mining couple labor, source custody and terrain removal in one durable action? |
| [Constructed footing](constructed-footing.md) | How do completed floors support actors and preserve safe assisted traversal through saves? |
| [Stair navigation](stair-navigation.md) | How do ordinary steps and routes preserve real elevations and actors above/below each other? |
| [Structural loads](structural-loads.md) | How do funded members, carried goods, rated bearings and bounded failure plans conserve mass? |
| [Coupled contact assessment](structural-contact-assessment.md) | How do candidate assemblies share exact support, preserve mass and identify load-dependent neighbors? |
| [Structural policy and motion](structural-policy-and-motion.md) | How does saved load policy govern actions, carried materials and movement, and how are descent paths checked? |
| [Intact settlement](structural-settlement.md) | How does excavation atomically lower intact material and riders onto known rated ground? |
| [Coupled intact settlement](coupled-settlement.md) | How do separately backed assemblies share support and publish a bounded common descent with their riders? |
| [Event-ordered descent](event-descent.md) | How do unequal landing events retain stopped obstacles, actual mass and explicit impact-response boundaries? |
| [Consequence continuation](consequence-continuation.md) | How do accepted physical changes resume after restart and publish once without spending extra time? |
| [Physical construction](construction-physical.md) | How do member condition and placement retain completed work, material ownership and atomic persistence? |
| [Reservoir transport](reservoir-transport.md) | How do scheduled gates move actual water, preserve provenance and share time/capacity? |
| [Reservoir boundaries](reservoir-boundaries.md) | What proves floors and walls against actual terrain and completed material, including narrow gaps? |
| [Reservoir containment](reservoir-containment.md) | How do retained seals govern complete world admission, real actions and exact recovery? |
| [Detailed liquid flow](liquid-cell-flow.md) | How does source-backed liquid move between cells without relaying or losing material? |
| [Actors and society](actors-and-society.md) | How do bodies, knowledge, relationships, cultures, institutions and narratives interact? |
| [Materials and activities](materials-and-activities.md) | How do gathering, crafting, quests, construction, trade and travel conserve resources? |
| [Presentation and interaction](presentation-and-interaction.md) | How do actions, menus, maps, animation, art and audio express the simulation? |
| [Content and composition](content-and-composition.md) | How are source content, procedural opportunities, progression and cross-system changes admitted? |

**Runtime bindings** identify concrete classes and their composition in the application.
**Domain contracts** identify executable value types, codecs or pure operations; they need not be
connected to a complete gameplay system. **Design contracts** state required behavior and boundaries
without asserting the existence of a corresponding API. An operation named in the specification
is not automatically a method in the runtime. Qualification belongs to the
[implementation evidence index](../../../implementation/AGENT_HANDOFF.md), not to these descriptions.

## Architectural rules

- Consequential state has one owner and one committed identity. A UI, scene director, generator,
  worker or NPC planner proposes actions; it does not independently mutate world truth.
- Definitions are immutable, versioned content. Instances retain actual possessions, damage,
  learning, obligations and history. Derived display and search caches are rebuildable.
- Seed, rules, accepted inputs and canonical ordering determine results. Loading order, frame
  rate, locale and worker completion time cannot choose authoritative outcomes.
- Calendar date, personal exposure, tactical readiness and visual animation are distinct measures.
  Spatial elevation, realm identity and historical branch are distinct address dimensions.
- Preservation includes meaning and playable reachability. A copied asset, source row or save file
  alone does not establish semantic migration.
- Extensive mechanics enter play through understandable affordances and gradual discovery.
  Presentation simplifies access without concealing essential costs or weakening simulation rules.

## Source and document authority

The [preserved technical design](../../../spec-v1_1/TECHNICAL_DESIGN.md) and its linked domain
specifications define the game design. The [interface supplement](../../../implementation/PLAYER_INTERFACE_AND_NAVIGATION.md)
refines player interaction. This reference explains their architectural contracts alongside actual
source bindings. It does not silently amend the design to match a narrower implementation.

Each subject names its source owners. Architectural changes update the affected descriptions and
links in the same coherent change as the code. Stable rationale belongs here; dated decisions,
unresolved disagreements, migration execution records, measurements and milestone history belong in
the [implementation records](../../../implementation/AGENT_HANDOFF.md). Frozen specifications remain
preserved. A discrepancy is reconciled explicitly rather than hidden by rewriting either account.
