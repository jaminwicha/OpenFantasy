# Content and composition

## Domain contracts and runtime admission

[PackageManifest](../../src/OpenFantasy.Core/Content/PackageManifest.cs) and
[PackageResolver](../../src/OpenFantasy.Core/Content/PackageResolver.cs) define namespaced,
version-pinned dependencies and deterministic resolution. A manifest is a content contract, not
permission to execute arbitrary native code.

[Hosting content admission](../../src/OpenFantasy.Hosting/Content) parses source packets, validates
canonical hashes, resolves supported definitions and builds exact world profiles. Separate farm,
gathering, production, quest and construction admissions retain their own input and identity contracts.
Admission establishes the relationship between source data and playable state; the client does not
invent equivalent-looking values when a source is missing.

[Encounter admission](encounter-actions.md#content-hosting-and-presentation) binds the retained Squire
Strike and Bramble Wolf records to real abilities, stats and a complete occupied farm world. Source
range/ATB transforms and newly authored anatomy are explicit. The extra resident is present before
custody and reservoir binding, has no invented stock or language grant, and leaves Wren's quest intact.
This source binding is distinct from full ability, monster and death-reward migration.

## Design contract: preservation and packages

Predecessor code, content and assets are preservation sources. Full import covers entities, species,
classes, items, monsters, NPCs, narratives, quests, relationships, locations, mechanics and assets.
Each mapping retains source identity/provenance, transformed fields, dependencies and reachable use.
An archive or matching record count proves retention, not complete gameplay import. Conflicting
interpretations require explicit transforms rather than deletion or silent replacement.

First-party and community content use typed definitions, effect operations, generators and presentation
manifests. Dependencies, versions and override order are pinned; ambiguous ownership rejects. Reviewed
engine extensions add native verbs with compatibility rules. In-game research and creation compile
through the same validators. Removing or upgrading a package analyzes live instances, branches and
arcs, validates a preserved copy and either supplies compatible meaning or refuses the change.

## Design contract: procedural composition and progression

Generation combines geography, ecology, cultural settlement grammars, inhabitants, resources,
adventurelets, encounters and narratives under shared constraints. It distinguishes hard invariants,
soft variety preferences and intentional fictional contradictions such as an unreliable rumor.
Bounded deterministic repair, optional pruning and rejection retain conflict evidence and the actual
accepted seed. Existing committed facts are changed by world actions, never rerolled on load.

Spatial design supports loops, alternate entrances, meaningful vertical access, secret connections
and inter-planar routes. Towns, cities, castles and dungeons have functional populations, streets,
rooms, sewers, caves and service spaces. Local adjacency validity does not prove that a settlement
functions, an adventure is reachable or a dungeon has a satisfying progression graph.

Challenge rating covers more than walkable paths. Quest predicates, combat, skill checks, resources,
social access, appointments, dimensions and historical destinations form an advancement graph.
World composition retains feasible witness routes through those conditions, with a smooth accessible
learning/progression path plus optional difficult gates, shortcuts and effective dead ends. A rating
is conditional on declared capabilities and context, not a promise that every seed is easy everywhere.

Mysteries distinguish truth, discoverable clues, observations and inferred beliefs. Zelda-like action
puzzles bind real switches, surfaces, items, skills and effects; they persist ordinary world state.
Timed hazards participate in the same authority and Wait dependency rules. Alternative skill, social,
spatial or temporal solutions must have valid consequences rather than hidden script exceptions.

Encounter decks select contextual opportunities without overriding NPC commitments or resources.
Authored adventurelets provide cohesion and continuations. Multiple larger arcs can emerge from
institutions, ecology, resources and individual choices; the player is not required to be the chosen
solver. Narrative scheduling and tutorial guidance never become independent sources of rewards,
actors, emergency supplies or world time.

## Cross-system composition examples

| Cause | Consequential owners | Permitted expression |
| --- | --- | --- |
| Flood reveals a crypt | Geography/support, water custody, routes, property, actors and opportunity eligibility | Changed terrain, water sound, observed news and discoverable access |
| A caravan fails to arrive | Cargo/crew, care and wages, commitments, market evidence, relationships | Local prices and dialogue change as relevant people learn the cause |
| A ruler dies | Life/form, office mandates, estate custody, kinship, succession and beliefs | Public notices and personal reactions follow actual knowledge |
| A ship needs repair | Damaged part, reserved plank/tool, worker duty, work exposure and travel capability | Wear, repair choreography and sound follow accepted work |
| A traveler enters a historical shadow realm | Address/branch, transit rights, actor form, source custody and perception | Atlas and visuals identify the destination without leaking undiscovered history |

For each integration, name the triggering cause, canonical owners, knowledge path, time domains,
reservations, commit or staged boundary, interruption/recovery and presentation semantics. This is
how depth accumulates across systems without adding independent special-case authorities.

## Validation boundaries

Canonical vectors establish byte and arithmetic contracts. Domain tests establish bounded rules.
Composition tests establish ownership across modules. Actual host crash/restart and malformed-save
tests establish durable boundaries. Native client journeys establish adapter behavior. Representative
workloads, platform/device tests, independent critique and human play/art acceptance establish
different claims; one category cannot substitute for another.

Windows, macOS and Linux with one to four hosted players define the desktop production target;
mobile uses the same portable core through its own qualified adapter/export path. Small world limits
and local tools are not evidence of production scale or multiplayer feasibility.

The [full requirements](../../../spec-v1_1/REQUIREMENTS.json),
[implementation plan](../../../spec-v1_1/IMPLEMENTATION_PLAN.md) and
[evidence handoff](../../../implementation/AGENT_HANDOFF.md) own coverage, order and acceptance status.
The [cross-system design](../../../spec-v1_1/CROSS_SYSTEM_SYNERGIES.md) expands the integration cases.
