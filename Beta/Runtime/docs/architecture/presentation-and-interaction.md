# Presentation and interaction

## Runtime binding: permitted views

[LocalWorldView](../../src/OpenFantasy.Hosting/Simulation/LocalWorldView.cs) is an immutable cached
projection of a sight-complete locality. It exposes visible actors and nodes, the controlled actor's
owned stock, addressed dialogue and applicable progression, quest and construction information.
It omits other inventories, finite source claims, private project funding and storage diagnostics.
Sight-complete local visibility is not a general fog-of-war or global NPC knowledge system.

The session refreshes this view at its cutoff when the observed authority root changes, including changes
published by another caller. A refusal or reading hold can publish no new world step while still requiring a
new view, such as the dialogue needed to close that hold. Unchanged roots reuse their cached projection;
render callbacks never query storage to compensate for stale presentation.

For saved material proposals, the Host view includes participant offers and a bounded
[creation snapshot](material-proposals.md#captured-creation-choices) of exact own lots and named
recipients. Preparation requires an explicit lot, amount and recipient and captures expiry once.
This boundary supplies terms to a client without granting access to the private world. Archived
inspection is a separate background session task; it never belongs in a frame's view refresh.

In paid worlds the view lists the player's live quotes as participant-safe
[`PaidQuoteView`](scheduled-trading.md#participant-quote-view) values: exact terms and lifecycle without
another actor's delivery, a market's stock basis or a distant issuer's presence revision. The player's
transient trade inquiry is read separately from the session as a pure in-memory value. An answered
inquiry only names the offered quote; accepting it is a separate, explicitly confirmed command prepared
from the inspected view, and ignoring it lets the quote close by its own window. A superseded offer reads
as superseded with no fill amounts and nothing about the other party. Paid and market confirmations made
during a reading hold or other non-exploration boundary are refused before queueing, while a waiting
inquiry may stay queued over the hold. The saved-proposal creation form and ordinary-care controls are
projected in paid and market worlds exactly as in the proposal world. Archived quote outcomes
use the same background inspection boundary as proposals. In market worlds the view also carries the
player's own [`LocalMarketView`](native-trading.md#runtime-binding-participant-projection): the advancing
step and each perceivable stall, without merchant stock, funds or rule parameters.

The [native stall](native-trading.md) keeps inquiry, reading, review and confirmation as distinct acts.
The wheel's Market page (Buy, Sell, Offers) opens a scene-owned panel and submits nothing, also while
paused. The panel's own `Market` input layer suppresses walking without pausing the host, so a merchant
answers while it is open; an explicit local reading pause stops this single-player world. A review captures
one exact participant offer; any changed choice, Back, focus loss or disconnection discards it, and
nothing auto-accepts, renews or requotes. The trade card speaks in player terms: price, dues, total,
purse and shortfall, and the time an offer stays open while the world runs; the fixed-world step basis
appears only as an optional hover diagnostic. Its controls form one explicit cyclic focus chain, and an
open dropdown list owns input until a row is chosen or the list is cancelled (Escape or controller B),
without closing the stall.

[FarmGame](../../client/scripts/FarmGame.cs) coordinates session, input and presentation through
named collaborators. [FarmCanvas](../../client/scripts/FarmCanvas.cs) and its
[presentation components](../../client/scripts/Presentation) map projected state to terrain,
paperdoll actors, gathering nodes, structures, daylight and target markers. The scene graph and
Godot physics do not own authoritative movement, inventory, production or time.

[FarmHud](../../client/scripts/FarmHud.cs) keeps identity, clock and inventory text in flexible,
word-wrapped columns defined by its [scene](../../client/scenes/FarmHud.tscn). Pause and Pack retain
their intrinsic control widths; larger observed values increase the header's height instead of
pushing controls off screen. The target bar follows header resize events with the scene-authored
vertical gap. Field visibility and camera framing use the actual laid-out header and footer bounds,
so wrapped status text remains part of the occlusion calculation.

Each canvas frame names exactly one geometry mode, and every actor, object, target cue and camera limit uses
that mode's projection: the fixed farm grid (`FarmCanvasCoordinates`, one admitted region and floor), the
worksite/watercourse cutaway (`WorksiteCoordinates`), or the cargo world's
[bounded region projection](cargo-presentation.md), built per frame from the regional observation published
with that same view (observed regions, floor, openings, blockers and water) with the farm's origin and scale. A
position outside its mode is refused rather than clamped or drawn at a plausible place; an unsupported regional
view keeps the last coherent frame with a HUD notice instead of failing the session. The cargo projection also
maps pointer picks back to authority positions after the input router permits ordinary exploration selection;
the fixed modes keep their guards and bounds unchanged. The sight-complete `LocalWorldView` still lists every
resident; the cargo frame presents only those standing in its observation.

## Runtime binding: semantic input and captured offers

[FarmInputMap](../../client/scripts/FarmInputMap.cs) defines semantic actions.
[FarmInputRouter](../../client/scripts/FarmInputRouter.cs) manages ownership across world controls,
dialogue, pause, pack and wheel. Cancel unwinds the active layer. Focus loss and controller disconnect
clear or cancel applicable input; held directions require neutral release before movement resumes.
Held routed presses remember whether the router or a focused pack/wheel Control owns their release.
Bookkeeping runs before GUI delivery, but a Control-owned accept release propagates so native buttons
can complete activation. Layer commands retain router ownership; a press held across focus recovery
cannot be reclaimed by a panel until released. Missing panel releases cannot turn the next world
press into an action. Startup reads the window's current focus before publishing readiness; it does
not assume a future focus event will arrive. A rendering fixture that injects focus does not redefine ordinary gameplay focus policy.

The [control-return adapter](../../client/scripts/FarmGame.ControlReturn.cs) closes action drafts and
adds a pending-control input layer while committed autonomous work finishes. It disables Resume and
explains the wait. Restoration keeps the local pause, presents the safe view and enables deliberate
Resume using the marker captured for that presentation. Held directions/actions require release;
regained focus and an earlier Resume press cannot supply consent. Control is captured before the view
so a newer permission cannot appear beside an older frame. See the
[participant protocol](player-client.md) for the ordered observation and port-binding contract.

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
Gather, Craft and Give. An explicitly selected [worksite face](local-worksite.md) substitutes Mine and
Climb in the relevant sectors, without changing the number of choices. Open slots retain their exact
quest identity or ledge displacement; changed operations require a new selection. Opening selects none. Direction or pointer selection inspects; explicit
confirmation acts. Cancel, focus loss and disconnect do not submit. Opening stops player movement
but preserves host time policy: running time continues and an existing pause remains paused.

Known [ordinary restorative actions](ordinary-care.md) add a separate Care page without moving the
four primary sectors. Q/right shoulder or the page control changes pages only while the wheel owns
input; it never cycles the world target or commits a choice. Changing pages clears selection. Personal
medicine and patient-owned permission withdrawal use the same exact public offers as the HUD. An
open restorative choice pins its actor, recipient, permission and ingredient revisions; a changed
choice becomes unavailable until the wheel is reopened. The helper's private stock and executable
controller commands are not exposed by the player's permission controls.

[RestorativeActionOffers](../../client/scripts/RestorativeActionOffers.cs) translates typed eligibility
into an item label, effect, cost and known blocker. Full health, missing medicine and retained-history
capacity are different explanations. The personal-use HUD button is icon-led with an inspection
tooltip; withdrawal's details state its permanent effect on the selected agreement. Both require a
ready player session, and neither bypasses encounter timing. Helper aid is selected independently by
the Host under the patient's prior permission, not synthesized as a player item command.

The actor presentation observes the controlled player's permitted progression HP during both exploration
and encounters. Other actors use only their permitted encounter HP projection. Consecutive observed
changes drive the same short signed health cue; the initial observation and reopening produce no
historical cue. This presentation neither changes health nor exposes private NPC progression.

[FarmInventoryModel](../../client/scripts/FarmInventoryModel.cs) and
[FarmInventoryScreen](../../client/scripts/FarmInventoryScreen.cs) inspect actual owned stock,
tools and recipe information. Display groups distinguish raw/derived and protected/reserved stock
without merging canonical lots. Known restorative items expose their admitted physical label and
strength and direct use to the Care page. The pack does not execute equipment, use or transfer operations.

## Runtime binding: gift preparation

The proposal-capable pack adds a scrollable Gifts form beneath a fixed header and above a fixed
review control. Focused form controls scroll into view. It has explicit physical stack, named recipient and
integer quantity selectors, plus incoming/outgoing offer inspection. No field selects a first row
automatically. Preparing captures terms without transferring or reserving items. A separate icon-led
Gifts wheel confirms creation, acceptance, decline or withdrawal through the same action router.
Quest delivery remains a distinct action. Other profiles omit this view and wheel page.

Open dropdowns retain their displayed row identities while the world continues; selecting validates
that identity against the current public projection. Reselecting an offer explicitly captures a new
observation. Input or frame refresh never swaps an unavailable lot for another stack. Exact wheel
intent and actor pins survive inspection; a changed choice explains its blocker. Explicit history
inspection is asynchronous and displays a result only for the matching selected offer. See
[material proposals](material-proposals.md#native-selection-and-commitment) for authority and time rules.

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

Classic pixel locomotion uses readable contact poses, coherent small body responses and quiet idle
holds. Visual richness does not require continuous motion. Separate walking, running, purposeful
actions, hits and forced movement; match gait to observed travel without letting animation determine
physical progress. Frame counts, timing and secondary motion are calibrated at actual gameplay scale.

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

## Native encounter choices

The [native encounter reference](native-encounters.md) specifies public ability offers, the encounter
input context, captured wheel choices and condition-driven visual feedback over the durable lifecycle.
Its interfaces use the same authenticated session and timing policy as ordinary world interaction.
