# Player interface, actions and navigation

Date: 2026-09-10. Status: living owner-directed supplement to the preserved [1.1 GDD](../spec-v1_1/GDD.md), [technical design](../spec-v1_1/TECHNICAL_DESIGN.md), [onboarding contract](../spec-v1_1/PLAYER_EXPERIENCE_AND_ONBOARDING.md) and [P00–P14 implementation plan](../spec-v1_1/IMPLEMENTATION_PLAN.md). Read this with those documents. The 181-requirement baseline and its reviewed hashes remain unchanged; this supplement refines interface obligations and supplies their execution criteria. It does not close any runtime gate or change phase priority/dependencies.

The owner wants broad, familiar RPG management screens, wheel-based quick actions, coherent local/world maps and calendar/atlas navigation, and interfaces that grow with usable skills without displaying irrelevant inert systems. The additional 2026-09-10 direction favors icons over text-heavy surfaces, with rich hover/tooltip inspection for item/artifact descriptions and functional explanations. This applies throughout the existing production phases. The semantic contracts below are the selected implementation direction. Exact buttons, wheel capacity, visual layout and numeric usability targets remain candidates for native evaluation, not newly claimed owner acceptance. The [previous reviewed interface edition](ui-management/archive/2026-09-10-before-icon-tooltips/MANIFEST.json) is preserved byte-for-byte.

Related evidence: [reference research](ui-management/RESEARCH.md), [supplement coverage and unrun acceptance cases](ui-management/COVERAGE.json), and [independent review and document validation](ui-management/REVIEW.md). These records distinguish documented precedents, our design decisions and implementation still to do.

## 1. A large action vocabulary with a small immediate surface

Use four complementary access levels. Every level refers to the same semantic actions and current host-provided information; a panel or wheel cannot invent a separate rule or bypass action costs.

| Surface | Purpose | Selection rule |
|---|---|---|
| Context prompt | One clear common interaction with the selected nearby person/object/place | Show action and target explicitly; offer other interactions when useful. Ordinary interaction must not silently become attack, theft, gifting or consumption. |
| Quick wheel / optional shortcut strip | A player's small, stable set of frequent actions, items, tools and party orders | Player-controlled favorites and named sets. Suggestions stay separate. Preserve positions across learning, equipment changes, depletion and restart. |
| Full action library | Find less frequent known actions and manage favorites | Browsable categories, filters, text search and controller navigation. Search is an accelerator, never the only access route. Show availability and supported targeting. |
| Management screens | Inspect and plan characters, possessions, skills, production, people, places and commitments | Persistent logical homes, relevant sections, simple default summaries and optional learned detail. Screens link to one another without making the player reopen a chain of unrelated menus. |

An experienced player may bind frequent commands directly. A newcomer can continue using context prompts and simple screens. Neither must browse every possible verb for routine work. Selecting a tool or action expresses an intention; concrete costs, targets and consequences remain readable before commitment. Preparation, targeting, submission and completion are different states.

Example: facing Wren offers Talk. Facing a damaged guard offers Inspect and, when supported, Repair. A pinned repair action can enter target selection. Its preview identifies required materials, access and tool/work conditions; confirming submits the same repair request that the full crafting/work screen would submit. If wood is unavailable, explain that fact rather than silently selecting another recipe or creating supplies.

## 2. Wheel behavior and discovery

Start native comparison with six to eight large sectors using recognizable icons, a selected-action name and a center description/cost/target area. Persistent per-sector labels are optional; icon recognition and native readability decide the final treatment. That size is an experiment, not a universal optimum. Compare pointer selection with discrete D-pad/keyboard stepping and a linear list using the same actions. Keep a stable management gateway available by ordinary menu navigation; a system wheel is an optional way to reach it.

- Favorites are an accelerator, not an inventory mirror. Learning twenty spells or gathering twenty items must not automatically generate twenty new wheel slots/pages. Offer a modest starter arrangement and an explicit, dismissible suggestion to pin newly relevant actions.
- Named favorite sets can reflect exploration, battle or work. Players choose their contents and order. A declared context-specific set may be configured explicitly; do not silently switch an open wheel or replace positions as nearby objects change.
- Use a flat quick wheel or a category plus one subordinate selector as the initial navigation budget. Larger catalogs go to the full library, not indefinitely nested wheels or endless page cycling. Test that occasional actions remain discoverable without memorized hotkeys.
- Freeze slot identities and the selected target while a wheel is open. Costs and availability may refresh; identity/order may not. If the target disappears, moves out of reach or becomes invalid, cancel or explain. Do not act on the nearest replacement.
- Known pinned actions stay recognizable when temporarily blocked, with concise reasons such as insufficient mana, no stock, occupied tool, unsupported body or wrong location. Lost/deleted package definitions retain a removable unavailable placeholder rather than binding to a different action.
- Default accessible operation is open, choose, confirm, with a cancel path. Optional hold/select/release operation may execute only a deliberately selected sector; opening, neutral-stick release, closing, focus loss or controller disconnection never submits an action. A targeting action first enters targeting; highlighting it spends nothing.
- Assignment mode is visibly separate from use mode. Rearranging a slot cannot consume its item. Input-mode changes update glyphs and navigation without submitting, moving the character or resetting the user's arrangement.
- Do not add confirmation dialogs to every routine harmless action. Reserve an additional review for consequential or ambiguous commitments such as dropping treasured gear, spending scarce progression resources, bulk transfers or entering a historical branch. Exact categories follow the action's real cost/reversibility.

Stable favorite slots and capability-dependent screen disclosure serve different purposes. The quick surface preserves motor memory; the screen library prevents irrelevant domains from overwhelming a new character.

### Icon-led surfaces and rich inspection

Favor recognizable icons for wheels, inventory grids, tools, skills, conditions, map markers and navigation. Use coherent silhouettes and stable meanings across screens; do not distinguish critical meanings solely by color or decorative rarity borders. Icons remain legible at actual play scale and available UI scaling. Retain a localized accessible name for every actionable icon and an optional icon-plus-label mode. Short selected-item names, counts and essential information can remain visible; this preference does not remove journal prose, readable stat comparisons or necessary explanations.

Rich information uses the same inspection content through mouse hover, keyboard/controller focus and an explicit Inspect/details control. On touch, expose a clear detail/info action; any long-press shortcut is optional. Inspection and execution have distinct input paths, so obtaining a tooltip cannot equip, consume, cast, buy or commit an action. Disabled known actions remain inspectable to explain their blockers. Reading details never changes the established menu/Wait/time policy.

Use a compact initial card and a pinnable expanded inspector. An item/artifact card can show its known name, depiction, description, effects, requirements, condition, material, provenance/lore, value source and equipment comparison where relevant. An action/function card explains what it does, target, cost, prerequisites, controls and current blocking reason. Distinguish known facts, uncertainty and legitimately unlocked expert detail. An unidentified relic cannot reveal its secret power, history, exact value or quest role through tooltip text, icon art, search or accessibility labels.

Keep known consequential costs, severe hazards and confirmation consequences visible at the point of commitment; do not require hover to discover essential risk. Deeper explanations may unfold in the inspector as relevant skills/knowledge grow, while basic function and accessibility stay available.

Tooltip placement must stay within the usable viewport, avoid obscuring the inspected target or required confirmation, and remain readable at large text sizes. Moving into a tooltip or pinning it must not dismiss it; explicit dismissal, sensible hover/focus delay and stable focus prevent flicker. No reading timeout is needed. Put long lore, linked definitions and side-by-side comparisons in the expanded inspector with a clear Back path rather than an unbounded chain of nested floating cards. Pinned views retain their selected identity, label stale live values and obey existing scoped-data rules.

P02 introduces real icon-led controls and item/action inspection alongside its first working screens. P03 verifies stale targets, disabled states, focus and multiplayer timing; P04–P10 expand domain-specific and earned detail. P11 reconciles native icons, descriptions and localized/accessibility names, P12 validates unaided recognition and reading comfort, P13 qualifies desktop behavior and P14 supplies full touch equivalents. This refines those phases without reordering them; no native icon or tooltip acceptance is claimed yet.

## 3. What is hidden, discoverable or blocked

Eligibility depends on actual knowledge, applicable capabilities, control/permissions and useful content, not a hard-coded class name.

| State | Presentation |
|---|---|
| Irrelevant and unknown | Omit the domain/action from ordinary navigation and search. Do not expose a secret name, recipe, requirement or silhouette merely because it exists on the server. |
| Legitimately discovered but not learned | Offer an explanatory entry in the appropriate knowledge/progression view, with only known prerequisites. Do not populate the quick wheel with inert abilities. |
| Learned/usable in some supported context, temporarily blocked here | Keep the known entry, target/cost preview and reason. A zero-mana spell does not disappear. |
| Domain first becomes useful | Introduce its existing logical home once, preserve navigation order and offer a short explanation. Do not rearrange other tabs to fill a gap. |
| Capability is later lost or a controlled actor changes | Recompute the relevant view; preserve profile arrangements and recoverable known records. Retain a useful known-domain view where information remains meaningful; collapse genuinely irrelevant empty navigation. Explain unavailable pinned actions without revealing another actor's secrets. |

A nonmagical character has no empty spellbook tab. A fighter carrying a usable enchanted item can receive its item action without an irrelevant full research screen. A character who learns a spell, rite or magical discipline gains the applicable spell/knowledge view regardless of class label. A companion's abilities appear as permitted companion orders, not automatically as the player's own knowledge or power.

Essential settings, controls, own-character condition, known danger, costs, deadlines and accessibility never require a skill unlock. Skill progress may reveal justified precision, comparisons, production reasoning, forecasts or diagnostic overlays. Simple mode remains available to experts. A player knowing how a menu works does not grant the character knowledge of a toxin or secret route.

## 4. Screen inventory and connected navigation

| Screen family | First useful form | Deeper form as the character/world makes it relevant |
|---|---|---|
| Field HUD and encounter UI | Selected target/action, relevant health/resources, concise party condition, optional compact map/compass and date/time, restrained notifications | Encounter readiness/Wait state, permitted cooperative actions and important hazards; specialist overlays remain optional |
| Character, equipment and inventory | Stats, conditions, equipment slots, owned items, weight/capacity, compare/equip/use and clear costs | Traits, injuries/body compatibility, class/loadout planning, provenance, containers, filtering, sorting and reviewed bulk operations; progression spending follows actual rules |
| Skills, abilities and spellbook | Known abilities and a readable focused progression view | Interdisciplinary skill graph, teachers/books/practice routes, known prerequisites, magical schools/research and elemental composition; hide undiscovered branches rather than displaying an omniscient giant tree |
| Crafting, research and work | Known recipes, real ingredients/tools/station, result/cost preview and progress | Substitutions, quality, byproducts, process graphs, reservations, queues, delegated jobs, repair and salvage; simple recipes remain accessible |
| Journal, investigations and knowledge | Accepted commitments, actual progress, useful notes and legitimately learned clues | Separate facts, testimony, hypotheses and stale information; evidence links, recurring characters, bestiary/codex, historical references and optional guidance without solved mysteries appearing automatically |
| Map and atlas | Explored local map, zoom/pan, selected floor, markers, legend, location breadcrumb | Linked regional/world views, discovered settlements/routes, underground/underwater spaces, learned overlays, known planes, historical editions and branches |
| Calendar and schedules | Current world date/time/season, known appointments and explicit deadlines | Crop/camp/work schedules, known festivals and travel commitments, learned forecast ranges and historical chronology; distinguish an estimate from an appointment or task duration |
| Party, relationships and household | Controlled actors, permitted companion orders, visible condition and known commitments | Relationships expressed through observed evidence, care/work roles, crew and household planning; no omniscient friendship number or secret NPC motivation unless rules explicitly permit that knowledge |
| Trade, transport, construction and institutions | Relevant merchant/owned cargo/build/work interactions | Prices with place/time provenance, routes/crew/mount care, stockpiles, settlement maps, budgets, delegates, law and politics when the player has a useful role/permission |
| Session, social, help and settings | Save/session/host information, zone/system chat as applicable, input/device settings, accessibility and assistance preferences | Multiplayer permissions, party communication and supported creator/package controls; always reachable without character progression |

Cross-links preserve context: journal entry → known map place → appointment → relevant person; item → learned recipe → real station/job; condition → known remedy → inventory or legitimate provider. Return goes to the previous selection/filter/scroll position. Links never teleport items, commit travel, learn secrets or order another actor without an ordinary validated action.

Inventory sorting, favorites and notes are management. Equipping, moving stock, reserving supplies and starting work may be consequential actions with distance, ownership, time and encounter restrictions. Remote storage can be inspected when legitimately known, but moving its goods requires actual transfer/logistics. Group order screens do not bypass NPC agency, travel or work costs.

## 5. One atlas, several explicit dimensions

Use one navigational shell for local → settlement/region → world scale. Zoom preserves the chosen anchor and orientation, changes detail/aggregation deliberately, and offers breadcrumb/direct scale controls and fit-to-selection. Search and filters operate on the observer's known index; icon clustering and optional layers prevent marker overload. Keyboard, controller and pointer all support zoom, pan, select and return. A minimap is a compact projection of the same permitted information.

Keep these axes separate:

1. **Scale:** how much space the map displays. Zooming out does not reveal an unexplored world or begin travel.
2. **Elevation/floor:** surface, upper floors, subfloors, caves, sewers and underwater depth. Mark known stairs, shafts and connectors explicitly; changing the view does not move an actor.
3. **Realm/plane:** material, shadow or another known realm. Portals link actual known destinations. Plane is not a synonym for basement.
4. **History/branch:** the map's observed/recorded date and branch, distinct from the player's current location and world date. Comparison is a read-only view until a separate permitted travel/intervention action is confirmed.

Always show enough of the current address to distinguish similar places: place, relevant floor/depth, realm and, outside the current-world view, date/branch. Retain a clear Current location return action and useful navigation history. Align cross-era/plane comparisons only where a justified correspondence exists; otherwise show separate frames rather than suggesting a false one-to-one geography.

Distinguish surveyed terrain from bought maps, reports, uncertain routes and outdated annotations. Record observer/source and observation date. A cartographic overlay for resources, political influence, threats or archaeology requires appropriate known information. Ordinary character access never queries administrative truth. Exploring or buying a map may reveal information through a real action; camera zoom does not.

Browsing a known historical map must not grant exact knowledge of an unvisited future, reveal secret historical entities or invoke unrestricted reconstruction on every drag. Request bounded, cancellable, versioned views of permitted data; use explicit preparation for supported expensive queries and discard stale results after changing the selected address. UI caches are not a second world store. World/branch versions invalidate stale views without deleting the character's old annotated map record.

Marking a route is planning. Traversal feasibility depends on actual capabilities, equipment, vehicles, portals, knowledge and world changes; label uncertainty and revalidate at commitment. The atlas must not promise that a reported bridge still exists. Looking at another date neither ages the character nor advances the world.

## 6. Calendar and time while using interfaces

World date, biological exposure, job duration, tactical readiness and the selected historical map date are different facts. Appointments use a named world/branch clock and absolute date; relative labels identify that clock. A personal work estimate or crop forecast states its assumptions. Known NPC schedules are expectations that may change, not omniscient guarantees. Known time-critical events remain readable during a permitted encounter hold.

Every major overlay displays authoritative timing status when relevant: world paused, encounter waiting, or world continuing. UI visibility does not decide time policy. Solo/shared-pause menus may request the existing host's allowed pause; independently browsing a menu cannot stop other players, manufacture safety, grant extra work or slow projectiles. Closing one screen releases only its own pause request, never another encounter/player's hold. Losing OS focus follows the host policy and consumes no pending action. Calendars may propose rest or travel, but acceleration and travel need the existing explicit action/consent rules.

Keep ordinary nonurgent notifications in an inbox/journal with a small visible budget. Important known danger, failed commitments or impending deadlines may interrupt appropriately; weather, background wildlife and every routine simulation change need not produce a popup. UI sounds support focus/confirmation with restrained, accessible alternatives rather than continuous repeated chirps.

## 7. Input, focus and action ownership

Bindings name intentions rather than physical keys: Interact, Alternate actions, Quick wheel, Management, Map, Journal, Confirm, Cancel/back, Inspect, Target cycle, Zoom and any explicitly assigned direct actions. Exact default keys/buttons will be selected and tested with the actual launch-device layouts. Preserve discoverable on-screen buttons and dynamic prompts, not keyboard-only instructions. Keyboard/mouse and gamepad must both reach every launch action; remapping, conflict explanation and restore-defaults apply per input context and device.

Use one explicit focus owner for each active input event. A modal stack determines where Confirm, Cancel/back, movement, scrolling and typing go. A top confirmation receives input before its parent screen; a screen before its underlying wheel/targeting layer; world actions receive only unconsumed input. Apply the same ownership to continuously polled sticks/keys: consuming an event alone does not prevent a movement loop from reading held input. Cancel unwinds the top logical layer; only from ordinary gameplay does the configured pause/menu command request its normal behavior. Text entry consumes gameplay shortcuts. Preserve focus when closing a child view and choose a nearby valid focus if the former control disappears.

Opening a wheel/screen consumes the initiating press and associated release. A blocking input context sends neutral player movement intent through the ordinary control path; this does not pause the world or cancel a vehicle's physical motion. Restoring gameplay focus must not reinterpret held menu-navigation input as movement: require neutral/released input or a fresh deliberate world command. Device hot-swap, resize, focus loss, controller disconnect and closing a modal cannot leak a click, craft action or movement into the world. No selection timeout is required in Wait; actual outside-world deadlines remain truthful. Provide visible focus, non-color-only selection, adjustable text/UI scale and dead-zone/selection tolerance, hold/toggle alternatives, and discrete navigation without simultaneous chords. Touch adaptation is P14, not an excuse to require analog precision at desktop launch.

The action flow is **offer → choose → target/parameterize if needed → preview → confirm → host validation → committed result → feedback**. Trivial interactions may combine steps without redundant dialogs. An offer is a read-only scoped snapshot, not a reservation or promise. The host revalidates action identity, current actor/control, target, knowledge, resources, costs and world revisions. Stale selection yields an explanation or a fresh preview, never substitution or automatic retry of a different action. Once committed, cancel can only request a legal interruption; it does not undo costs by hiding animation.

Long activities such as construction, repair and production must eventually be comfortable to start,
inspect, interrupt and resume without confirming every atomic work step. A one-step command proves
authority and resource composition; it does not qualify that long-activity interaction. P04 production
activities and P07 construction/delegated jobs must declare their target, resource budget, stopping
conditions and interruption/return policy. Each step still uses current authority and world rules.
An explicit activity commitment must not be implemented as UI click replay, an unlimited retry loop
or permission silently surviving owner return. Preserve already earned work and show the reason for
any stop; qualify the sustained interaction with human playtests at its normal phase.

## 8. Modular implementation boundaries and persistence

These are cohesive responsibilities, initially within existing assemblies. Names describe proposed roles, not already implemented types; avoid a universal UI manager or new services/processes solely for menu organization.

| Responsibility | Owner and boundary |
|---|---|
| Action legality and consequences | Portable Core retains typed actor/actions and rules. Hosting provides permitted action offers and command submission using existing durable authority. NPC and player control share applicable rules. |
| Action presentation | Client maps stable semantic action IDs to localized labels/icons, targeting and supported screen/wheel presentations. Host offers carry permitted availability, known blockers, costs, target semantics and relevant revisions. No private facts may appear in blocked reasons. |
| Input routing and focus | Client input router and screen navigator own semantic mappings, modal focus, draft selection and controller/pointer behavior. Editable scene layouts and small C# presenters own views. |
| Atlas/calendar/journal projections | Hosting supplies bounded observer/world/branch/address-scoped queries; Core remains owner of facts and historical reconstruction. Client owns view caches, filters and map navigation state. |
| User preferences | Versioned local/player profile owns remaps, wheel arrangements, UI scale, assistance, filters and layout. Identify action/item bindings explicitly; a favorite may bind a declared item family or exact instance, never silently substitute a different effect. |
| Character/world records | Authority owns discoveries, knowledge, quest facts, calendar commitments, actual item/job state and permitted persistent world annotations. User-private notes are scoped to their author/world/branch; sharing is explicit. UI familiarity does not unlock character knowledge. |

Persist favorites using stable identifiers and explicit binding kind, not list indices or localized text. Keep device remaps separate from actor loadouts. On switching characters, saves or branches, apply only eligible references; stale slot IDs remain understandable and removable. Draft filters, map zoom and focus may restore locally, but pending action submissions recover by their real receipts. Never replay a UI click because a menu was restored after reconnect.

## 9. Integration into the existing phases

This table adds obligations within existing work, not a request to accelerate this lane ahead of other dependencies. Each phase introduces useful functioning screens with its mechanics; empty future tabs are not completion evidence. Independent preparation can follow the master plan's existing parallel rules.

| Phase | Required interface increment and evidence |
|---|---|
| P00 | Record screen/action/availability/map-address contracts and a finite UI catalog. Name interaction, navigation and knowledge ownership; keep default keys and wheel sizing as explicit test candidates. |
| P01 | Retain input focus/semantic binding and profile seams; scoped action/map-query contracts; early real controls and minimal own-character views. Qualify the same boundaries in the planned desktop exports. |
| P02 | Context interaction, first customizable wheel and navigable full-action view; real stats/inventory/equipment/known skills/crafting/journal, local explored map with zoom/floor/markers, and calendar/known appointments. Prove through the actual inhabited-place/encounter/trade/mystery/puzzle loop with remapping, device parity and native readability. |
| P03 | Prove stale targets, changing stock/control, modal interruption, multiplayer clock behavior, reconnect and preference migration. First real historical/realm fixture verifies read-only atlas selection versus travel and return context. Test mature action-library layouts with explicitly labeled synthetic catalogs without claiming those gameplay abilities are implemented. |
| P04 | Deep production/research/jobs, stock/reservations, seasonal cultivation, camp/watch and household schedules. Demonstrate simple and learned detail views of the same real activity. |
| P05 | Complete supported character/class/body/skill/magic navigation, focused interdisciplinary graph, spell/research eligibility, known item powers and loadouts. Teach new menus when relevant. |
| P06 | Integrated local/regional/world atlas with bounded zoom/pan, transport/crew/cargo/mount views and known route/travel constraints across streaming. |
| P07 | Settlement/construction/stock/job/commerce maps and delegated intentions, budgets and exceptions; no mandatory micromanagement dashboard for an uninvolved traveler. |
| P08 | Known relationship/household/lineage/cultural/political information, institutions and consequential news, with evidence and permission boundaries. |
| P09 | Full journal/investigation and logical advancement support, uncertain clues, changing commitments and optional assistance across varied seeds; guidance cannot reveal hidden solutions. |
| P10 | Complete known planar/historical/branch atlas and calendars, bounded preparation, provenance/comparison/return navigation, creator UI extensions and cross-era links. |
| P11 | Reconcile every required screen, semantic action, blocked reason, localized label, glyph, supported device interaction and content-domain mapping; no orphaned abilities or permanent unaccepted placeholder navigation. |
| P12 | Human novice/expert and long-session tests across real full catalogs; selection errors, discoverability, distraction, navigation effort and endgame delegation must pass alongside technical correctness. Revisit layout/caps using evidence. |
| P13 | Verify desktop production profiles, accessible input/UI, saves and session behavior on actual supported artifacts; no unresolved required UI paths. |
| P14 | Touch, device interruption, mobile readability and host interoperability using the same semantic actions and full content; avoid mandatory hover, tiny radial wedges or hardware-key shortcuts. |

See [coverage](ui-management/COVERAGE.json) for baseline requirement links and explicit unrun cases. Before each relevant native packet, choose a finite scenario/catalog and record device, resolution, actor knowledge, enabled assistance and menu state. Never infer comfort from low popup counts or claim new UI tests have run because old farm tests passed. Artists own interface visual assets; client builders own navigation/rendering, domain builders own truthful actions, and independent critics plus human playtesters evaluate actual results.

## 10. Current implementation boundary

As reviewed on 2026-09-10, the client has prototype-specific FarmInputMap bindings and a fixed FarmHud action strip. Controller B performs ration crafting and Escape directly toggles local pause; those bindings require context-aware routing before adding menus. The gather preview selects a nearby nonempty node, and talk targets the admitted Wren fixture. There are no general inventory/stat/skill/journal/map screens, wheel or in-game rebinding screen yet. LocalWorldView is deliberately sight-complete for the farm and is not a production discovery/atlas feed.

This supplement changes documentation and reusable workflow guidance only. It neither replaces the current controls nor pauses the running game. Native implementation and user acceptance remain future work at the phases above.
