# Presentation and interaction

## Runtime binding: permitted views

[LocalWorldView](../../src/OpenFantasy.Hosting/Simulation/LocalWorldView.cs) is an immutable cached
projection of a sight-complete locality. It exposes visible actors and nodes, the controlled actor's
owned stock, addressed dialogue and applicable progression, quest and construction information.
It omits other inventories, finite source claims, private project funding and storage diagnostics.
Sight-complete local visibility is not a general fog-of-war or global NPC knowledge system.

[FarmGame](../../client/scripts/FarmGame.cs) coordinates session, input and presentation through
named collaborators. [FarmCanvas](../../client/scripts/FarmCanvas.cs) and its
[presentation components](../../client/scripts/Presentation) map projected state to terrain,
paperdoll actors, gathering nodes, structures, daylight and target markers. The scene graph and
Godot physics do not own authoritative movement, inventory, production or time.

## Runtime binding: semantic input and captured offers

[FarmInputMap](../../client/scripts/FarmInputMap.cs) defines semantic actions.
[FarmInputRouter](../../client/scripts/FarmInputRouter.cs) manages ownership across world controls,
dialogue, pause, pack and wheel. Cancel unwinds the active layer. Focus loss and controller disconnect
clear or cancel applicable input; held directions require neutral release before movement resumes.
A rendering fixture that injects focus does not redefine ordinary gameplay focus policy.

[FarmTargetSelection](../../client/scripts/FarmTargetSelection.cs) holds an explicit transient target.
A selected plant remains selected when another becomes nearer. Its unavailable Talk/Give action
cannot redirect to a person. Restart discards client selection, not world state.

[FarmActionOffer](../../client/scripts/FarmActionOffer.cs) and
[FarmActionOffers](../../client/scripts/FarmActionOffers.cs) capture the exact target, intent,
costs, revision expectations and known blocker. Hover or focus inspects without acting. Buttons and
hotkeys submit that captured intent; the host revalidates legality and may reject stale revisions.
Neither preview nor execution silently substitutes a target, recipe or ingredient lot.

[FarmQuickWheel](../../client/scripts/FarmQuickWheel.cs) and its
[model](../../client/scripts/FarmQuickWheelModel.cs) present four stable cardinal actions: Talk,
Gather, Craft and Give. Opening selects none. Direction or pointer selection inspects; explicit
confirmation acts. Cancel, focus loss and disconnect do not submit. Opening stops player movement
but preserves host time policy: running time continues and an existing pause remains paused.

[FarmInventoryModel](../../client/scripts/FarmInventoryModel.cs) and
[FarmInventoryScreen](../../client/scripts/FarmInventoryScreen.cs) inspect actual owned stock,
tools and recipe information. Display groups distinguish raw/derived and protected/reserved stock
without merging canonical lots. The pack does not execute equipment, use or transfer operations.

## Design contract: a small immediate interface

The action vocabulary can be large while the immediate surface stays small. Contextual primary
actions and curated wheels expose useful choices; stable favorites and clear categories avoid an
unbounded wheel of every possible verb. Inspect, select, aim and commit are explicit concepts.
Known blocked actions explain visible reasons; undiscovered capabilities do not leak through menus.

Icons lead navigation, with accessible labels and hover, focus and explicit-Inspect equivalents.
Essential costs, risks and irreversible consequences remain visible before confirmation without
requiring a tooltip hunt. Skill progression exposes deeper analysis and specialized tools, not basic
control instructions or indispensable safety information. A character without magic does not need
an inert spellbook screen.

Management surfaces include character/body/stats, inventory/equipment, skills/classes, applicable
spellbooks, crafting, journal, party/relationships, trade, institutions and settings. Shared navigation
preserves a clear focus owner and return path. Device bindings, remapping, scaling and accessibility
belong to the adapter and must preserve identical semantic intent. Interface convenience cannot bypass
reservations, time, permissions or knowledge.

## Design contract: atlas, calendar and progressive discovery

Local and world maps share an atlas context with explicit scale, floor, realm and historical edition.
Zoom and linked views navigate knowledge; changing a map layer does not teleport the actor. Known
routes, discovered topology, historical facts and uncertain reports remain distinguishable. A calendar
shows world appointments and each commitment's duration policy without implying one uniform clock.

Onboarding opportunities come from actual feasible generated locations and actors. An introduction
graph controls teaching and guidance separately from causal progression. Hidden-in-plain-sight gates
can reward later skill without cluttering the opening experience. Advanced management combines learned
mechanics through summaries, policies and exception handling rather than hundreds of mandatory clicks.

## Design contract: animation, art and audio

Semantic actions emit performance requests for posture, gesture, emotion, interaction, objects and
spatial context. Idle quirks, breathing, glances, animal behavior and environmental motion express
character and world state. Visual variation uses restrained reusable combinations rather than repeated
identical sprites or an uncontrolled asset Cartesian product.

Puppetry reserves actors and props, defines interruption/skip/rejoin and distinguishes cosmetic motion
from validated consequential actions. A conversation animation cannot grant affection, move cargo,
complete labor or advance a held clock on its own. Restoring a scene reads its authoritative phase;
replaying presentation cannot duplicate rewards.

The visual direction combines readable pixel composition with rich material and cultural detail.
Age, maintenance, scars, layered architecture, seasonal changes and weather leave selective evidence
of history. Small magical details support focal points rather than filling every surface. Species,
culture, clothing, equipment and body changes shape compatible sprite/animation profiles. Artist-owned
assets follow accepted calibration before production expansion; rendered screenshots do not imply
human style acceptance.

[FarmDaylight](../../client/scripts/Presentation/FarmDaylight.cs) is a concrete outdoor palette binding
to the public calendar. UI and target visibility have separate brightness. This palette is not
authoritative weather or seasonal simulation.

Audio is a first-class projection of place, activity and mood: wind, rain, seaside birds, distant
creatures, footsteps, work and cave reverberation communicate context. Spatial/acoustic rules and
listeners govern audibility; sound may supply evidence only through an authorized perception path.
Music uses restrained adaptive layers and fades between exploration, settlement, danger and encounter
contexts. Quiet crafting and travel allow space; intense battle music requires an appropriate event.
Playback deduplication, loops and reconnection cannot replay a world action.

Detailed contracts: [interface navigation](../../../implementation/PLAYER_INTERFACE_AND_NAVIGATION.md),
[presentation](../../../spec-v1_1/PRESENTATION_CONTRACTS.md),
[audio](../../../spec-v1_1/AUDIO_DIRECTION_AND_RUNTIME.md),
[onboarding](../../../spec-v1_1/PLAYER_EXPERIENCE_AND_ONBOARDING.md) and
[art calibration](../../../art-style-calibration/README.md).
