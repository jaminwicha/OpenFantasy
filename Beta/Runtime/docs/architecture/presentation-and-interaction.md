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

The shared stall consumes a purpose-specific `FarmMarketPresentation` for the selected shopper.
Trade discloses that actor's wallet and usable commodity lots without requiring its full Inventory
facet. Selection scope and actor revision remain pinned through explicit review. Showing a lot,
withdrawing its disclosure, asking for terms and accepting an offer remain separate actions.
Saved terminal records require the independently authorized history reader; observed offer identifiers
are not a complete archive directory. Asynchronous results carry panel/record lifetime guards so an
old visit cannot update a different shopper's screen. Shared-world pause text describes input readiness,
not a promise that other players stop the clock.

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

[`FarmProductionPresentation`](../../client/scripts/FarmProductionPresentation.cs) and
[`FarmProductionActionOffers`](../../client/scripts/FarmProductionActionOffers.cs) share gathering,
quick crafting and worksite offer construction between home and scoped operated views. Production
does not require disclosure of the full Inventory facet. Selected craft preparation supplies exact
eligible ingredients, custody and recipe/storage revisions; it is not reconstructed from home stock.
The quick craft uses the known ration recipe or, for a selected character, its sole other admitted
recipe. It never selects the first of several unrelated recipes. The explicit recipe library is a
separate interface from that quick shortcut. Personal readiness comes only from operated disclosures and the ordinary
authority; shared world holds and phase may block work for everyone.

[`FarmRecipeLibraryModel`](../../client/scripts/FarmRecipeLibraryModel.cs) lists the disclosed admitted
nonconstruction recipes, including choices whose ingredients are missing. This projection does not
implement a learned-recipe filter. The scene-owned Recipes panel is available through the HUD without
requiring Inventory or Recovery. It keeps the recipe list and descriptions scrollable while preparation,
confirmation and closing remain explicit controls. Recipe labels, costs, outputs and by-products describe
the particular selected recipe; navigating does not reserve stock or submit a command.

Preparing a recipe captures its exact version, selected ingredient identities, provenance, quantities
and revisions, custody, storage and production revisions, plus the complete action scope. Confirmation
submits that original offer through the ordinary client session. Equal observation refreshes leave the
capture intact; changed terms invalidate it without automatic renewal. Actor, branch, session or selection
lifetime changes clear the review. Focus interruption also requires new preparation. Opening and closing
preserve host timing and suppress held walking. A public Potion name authorizes neither consumption nor
healing. Construction retains its separate work path.

The selection protocol pins reviewed production requests through the wheel, including home generation
zero. Its full selection, actor revision, node/tool/site and recipe/ingredient fields must still match;
equivalent newly projected craft requests compare structurally. A changed capture requires reopening,
not a silently refreshed command. Legacy unselected wheels retain live preview refresh for their pinned
plant while previously issued command objects stay immutable. Neither path can replace an already
submitted command or override an authoritative stale-request refusal.

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

The Care page's Library sector opens the shared scene-owned
[care choices](ordinary-care.md#public-controls-and-patient-owned-permission). Its modal layer owns
selection, Review and Confirm while suppressing walking; inspection does not change world readiness.
Use definitions and permission identities distinguish choices even when their medicine or helper is
the same. A changed observation, selection lifetime, focus loss or controller disconnect discards
review without submitting. Confirmation requires readiness and sends only the exact issued offer.

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
without merging canonical lots. Public item descriptions identify exact content pins independently of
private capabilities. Strength and Care guidance appear only when restorative advice is actually
disclosed. Tool-kind fallback describes its function without inventing a particular material or item
definition. The pack does not execute equipment, use or transfer operations.

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

## Runtime binding: participant History

The shared [participant History screen](private-history-directory.md#native-presentation-ownership)
discovers terminal exchanges and proposals independently of the Pack and Market. It uses a bounded
page and explicit inspection of one selected record. Its dedicated input context, captured opening/
page/row lifetimes and admitted-task owner prevent old replies from populating another visit or actor.
Opening, browsing and closing submit no gameplay command; the screen displays the actual local/shared
timing policy and cannot independently stop other participants.

## Runtime binding: semantic sound

[FarmSoundscape](../../client/scripts/Presentation/Audio/FarmSoundscape.cs) is the Godot audio adapter
for the presented home or regional view. It reads only the participant's own `LocalWorldView` through
[SoundscapeObservation](../../client/scripts/Presentation/Audio/SoundscapeObservation.cs), so sound can
never reveal an actor, source or event that view withholds. It never submits, confirms or delays an
action and never reads world time; its dwell, fade and spacing use presentation time only.

[SoundscapeCueDeriver](../../client/scripts/Presentation/Audio/SoundscapeCueDeriver.cs) turns committed
changes between consecutive observations into transient cues: footsteps from a visible actor's committed
travel per stride, gathering contacts from a decrease in a visible source's remaining swings, and joinery
or digging contacts from an increase in completed work. A submitted or refused action changes nothing it
reads and therefore makes no sound. Each cue key names the kind, the source, its committed revision and an
ordinal, and one listener visit delivers a key at most once. A visit is one world, branch and participant
between resets. Its first observation, a newly visible source, travel beyond four cells in one observation
and a moved incapacitated body establish baselines only, and one observation yields at most one cue per
source. Opening, reopening, reconnecting or presenting another character's view therefore never replays
earlier actions, and a delayed frame cannot release a burst. Regrowth makes no contact sound. Footsteps use
generic ground: surface-specific steps need terrain material in the view and are not inferred.

[SoundscapeContextDirector](../../client/scripts/Presentation/Audio/SoundscapeContextDirector.cs) owns the
sustained score, ambience and speech ducking. A visit restores the current situation without a transition:
an engaged encounter restores encounter music, a concluded one restores the aftermath and exploration
otherwise. Encounter entry is immediate. Leaving an encounter waits a short minimum, then holds a quiet
aftermath while the outcome is read and for a further dwell before exploration returns. A conversation or
reading hold ducks music and ambience. Only the open outdoor ambience is bound; enclosure, portals and
daylight variation are not yet represented.

[AcousticIntentCatalog](../../client/scripts/Presentation/Audio/AcousticIntentCatalog.cs) declares an intent
for every derivable cue pairing and every sustained context: bus, stream, caption, repetition spacing and
simultaneous voices. An unknown pairing throws rather than playing nothing. The status distinguishes an
intent awaiting a creator asset, a deliberate silence and a mapped stream; only a mapped intent plays.
Captions state only what a legitimate audible cue conveys, and music and ambience beds carry none.
The scene-owned [bus layout](../../client/default_bus_layout.tres) provides independent Music, Ambience,
Effects and Voice buses. The adapter checks the shared voice pool before its playback gate, positions
cues at the canvas projection of their source and withholds sources outside a regional frame.
Probes read bounded [diagnostics](../../client/scripts/Presentation/Audio/SoundscapeDiagnostics.cs);
nothing about sound is persisted or sent to the world.

The controlled-character presentation suspends cue derivation; the next home view is a fresh baseline
while the sustained context continues. Controlled views do not yet derive their own cues.

## Runtime binding: client settings and captions

[FarmSettings](../../client/scripts/Settings/FarmSettings.cs) holds the player's mixer levels for Master,
Music, Ambience, Effects and Voice and a captions preference. They are presentation preferences stored in one
user file by [FarmSettingsStore](../../client/scripts/Settings/FarmSettingsStore.cs), outside every world save,
so changing them never alters a world, its history or another participant. Missing, unknown or malformed
entries fall back to their defaults one at a time, and a damaged file never prevents play. A zero level mutes
its bus rather than requesting infinite attenuation. Disposable fixtures use defaults in memory unless they
name their own settings file, so a probe never reads or writes the player's settings.

The scene-owned [settings panel](../../client/scenes/FarmSettingsPanel.tscn) opens with O, a controller's
share or capture button, or the HUD's Settings control. Its input layer sits above other panels and is
reachable during play, encounters, pause, conversation and while a world opens. It owns input while open:
walking is suppressed and world actions are refused, but opening it never pauses the world, changes readiness
or advances time. O, Escape, Start or Cancel closes it and writes the file; a failed write keeps the applied
preferences for the run and is reported. A control-return boundary closes it like other panels.

[KeyBindings](../../client/scripts/Settings/KeyBindings.cs) is the single table of remappable actions and their
default primary keyboard keys; [FarmInputMap](../../client/scripts/FarmInputMap.cs) installs its defaults and
applies the player's saved choices after them. Escape, the arrow keys, Enter, Space and Tab are reserved, so pause,
panel closing, arrow walking and interface navigation always work, and controller bindings are not remapped.
Choosing a key that another action uses swaps the two, so no action is ever unbound; a saved set with a reserved,
unknown or duplicated key falls back to the defaults. While the panel captures a key, the next key press belongs to
it and never reaches gameplay routing, and Escape keeps the current key. Routing and walking read the input map, so
a rebound key acts immediately. [FarmKeyHints](../../client/scripts/FarmKeyHints.cs) carries the current keys'
display names for on-screen hints only; the client rebuilds it from the input map whenever bindings change, and
fixtures keep the defaults.

[CaptionFeed](../../client/scripts/Presentation/Audio/CaptionFeed.cs) shows short text for notable derived
sounds when captions are enabled, in the scene-owned [caption layer](../../client/scenes/FarmCaptions.tscn).
An intent is captioned only when the catalog marks it notable: tool, joinery and digging contacts are;
routine footsteps, music and ambience are not. A caption describes the sound event and appears whether or not
a creator asset is mapped. Sources withheld from the presented frame are never captioned, a repeated caption
refreshes rather than stacks, at most three lines show and each fades after a short lifetime. Captions follow
the same derivation as sound, so reopening or reconnecting never replays an earlier caption.

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

Shelter acoustics follow the listener's enclosure, intervening materials and open or closed portals.
Moving indoors changes the permitted mix and reverberant character while outdoor weather and world
time continue under their own rules. Two listeners on opposite sides of the same wall can receive
different mixes. A vessel interior follows the same boundary while retaining its motion and condition
context. Entering, leaving or reconnecting restores ongoing ambience without replaying transient actions;
an acoustic cue cannot disclose an otherwise unauthorized source identity. Filter and reverb algorithms
are presentation choices, separate from authoritative perception and physical state.

Autonomous action presentation connects observable preparation, commitments, actual activity and its
consequences. Dialogue and environmental cues explain what the player may know without exposing
private motives or inventing a causal story. Meaningful personal or cultural belongings communicate
habitation alongside selective wear; greater surface detail is not a substitute for lived context.
These obligations apply to routine work, defense preparation and recovery as well as dramatic events.

Detailed contracts: [interface navigation](../../../implementation/PLAYER_INTERFACE_AND_NAVIGATION.md),
[presentation](../../../spec-v1_1/PRESENTATION_CONTRACTS.md),
[audio](../../../spec-v1_1/AUDIO_DIRECTION_AND_RUNTIME.md),
[onboarding](../../../spec-v1_1/PLAYER_EXPERIENCE_AND_ONBOARDING.md) and
[art calibration](../../../art-style-calibration/README.md).

## Native encounter choices

The [native encounter reference](native-encounters.md) specifies public ability offers, the encounter
input context, captured wheel choices and condition-driven visual feedback over the durable lifecycle.
Its interfaces use the same authenticated session and timing policy as ordinary world interaction.
