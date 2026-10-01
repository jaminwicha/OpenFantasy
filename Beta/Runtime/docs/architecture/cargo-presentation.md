# Cargo presentation

The native cargo binding presents the accepted [two-market cargo world](cargo-circuit.md) in the Godot client
with its existing C# authority. One bounded terrain observation and one projection place the ground, actors,
objects, target cue, camera limits and pointer picks; the retained merchants are composed from their own
source appearances; and either stall is chosen explicitly by its market identity. Travel is ordinary
movement input and trading is the unchanged [native trading](native-trading.md) panel. Nothing here adds
authority, rules, codecs, schema, IPC or a second world model.

## Runtime binding: selection and opening

[`NativeCargoWorld`](../../src/OpenFantasy.Hosting/Content/NativeCargo/NativeCargoWorld.cs) is the explicit
native selection. It reuses `CargoWorldAdmission` unchanged for the fresh genesis and the merchants' opt-in
trading advice (resupplied on every opening, never saved), names both stalls (`NativeCargoStall`: market,
merchant, source identity and name, retained place and place name) and binds each merchant's retained
`appearance` record, read from the digest-pinned source packet and checked against the appearance hash the
source binding already authenticated. `Corridor` rebuilds the authored lane from the genesis's actual basis.

[`FarmClientSession.OpenCargoAsync`](../../client/scripts/FarmClientSession.cs) creates a new cargo save, or
opens an existing one only with its expected genesis bytes and the same world, branch and admission. It never
upgrades, resets or relabels a save, and opening any other world as a cargo world is refused.
`OpenCargoFixtureAsync` continues an exported fixture's existing save after complete recovery, and only for a
fresh cargo genesis at commit and step zero. The [entry](../../client/scripts/NativeCargoEntry.cs) is the
`NativeCargo.tscn` scene, launched by name with `--cargo [SAVE]`; the default user save is
`user://worlds/openfantasy-cargo.sqlite`. Other entries and saves keep their behavior.

## Runtime binding: bounded observation and one projection

[`LocalRegionObserver`](../../src/OpenFantasy.Hosting/Simulation/CargoPresentation/LocalRegionObserver.cs)
projects the player's terrain observation from one already selected immutable root. A regional
[`LocalWorldSession`](../../src/OpenFantasy.Hosting/Simulation/LocalWorldSession.cs) applies it whenever it
publishes a view, on its serialized host owner, so `LocalWorldView.Regional` comes from the same root as that
view's actors, offers, worksite and liquids. A frame reads only the published view: no authority lock, history
or database read is ever part of a frame, and frames keep rendering while an explicit recorded read holds the
authority's writer gate. The observation includes every admitted region whose columns reach within 24 cells of
the player's column, at the floor the player's feet stand on, and always the region holding the player's column.
[`LocalObservedRegion`](../../src/OpenFantasy.Hosting/Simulation/CargoPresentation/LocalRegionObservation.cs)
carries bounds, revision, each column's ground kind and, for openings, the known depth to solid ground below
(read through the regions stacked under it). Kinds are `Surface`, `Soil` and `Stone` for walkable intact ground,
`Raised` when the cell above the floor is solid (a bank or blocker, never walkable ground) and `Open` for a
channel, opening or excavated cell. `LocalObservedWater` gives each wet observed column's committed volume and
exact fill surface across the observed column from eight cells below the floor through the cell above it. No material identity,
source lineage, reservoir identity or excavation history is carried. Region projections are cached by bounds,
revision, floor and the revisions of the regions stacked beneath, so unchanged regions are not recomputed.

The observation follows the legal feet floor without requiring solid terrain immediately beneath it.
The cargo binding admits terrain support, including lower terrain floors. A construction-supported world
needs its physical supporting geometry bound to the renderer as well; admitting its feet alone does not
provide that geometry. A player outside every admitted region or between
cell floors has no observation (`Regional` is null while `IsRegional` is true). The client then keeps its last
coherent frame, says so in the HUD and keeps input and offers working; the next supported view presents
normally. It never latches a failure or draws a guess. This is presentation locality, not a visibility,
discovery or streaming system.

[`CargoViewProjection`](../../client/scripts/Cargo/CargoViewProjection.cs) is the engine-free projection of
one frame. It is a fixed affine map of authority subunits: 32 pixels per cell with the farm embedding's
origin cell (96, 320), so the retained farm keeps its exact canvas positions, and no authority position moves
when the viewport or the observed set changes. It admits only positions on the observed floor inside an
observed region and refuses everything else (no clamping). `Unproject` is its exact inverse for pointer
picks at cell-centre precision, and `Bounds` is the observed terrain extent.

[`FarmGame.Cargo`](../../client/scripts/FarmGame.Cargo.cs) builds each cargo frame from that projection:
actors, gathering nodes and completed structures are presented only when the projection admits them;
`FarmCanvasFrame` and `FarmActorFrame` carry the projection and validate every position against it; the
camera follows the projected player within the visible field between the laid-out HUD header and footer.
[`CargoCameraFraming`](../../client/scripts/Cargo/CargoCameraFraming.cs) derives its follow offset and limits
from that field, the viewport, zoom and observed terrain extent. Empty padding beyond observed terrain lets
actors near an edge stay clear of the HUD; this padding does not admit or draw unobserved geography.
Resizing or expanding the footer changes camera framing without moving authoritative feet. Fixed farm,
worksite and watercourse frames keep their own embeddings and guards unchanged.
The [threatened-world selection](road-threat.md) uses a stable 1× zoom and frames the complete sprite bounds of
the player and observed threats inside this same field. Ordinary cargo selections retain their existing zoom and
player focus. The visibility probe additionally excludes the target-controls overlay from visible sprite bounds.
[`CargoTerrainView`](../../client/scripts/Cargo/CargoTerrainView.cs) draws exactly the observed columns:
intact surface ground uses the farm's own retained source floor only inside the farm's own region and the
grass tile elsewhere, exposed soil and stone use their atlas tiles, raised blockers are drawn as banks above
the floor, openings are dark by known depth, and committed water uses the retained watercourse surface (full
at the floor, a low fill below it). The player's own work targets are outlined on their columns. No source tile
array is repeated over another place, and nothing outside the observed regions is drawn. It redraws when the
regions, revisions, water or work targets change and records what it last drew (shape, revisions and a digest
of every column's choice). The target cue is drawn at the target's projected feet; an excavation target's cue
is on the worked cell's own column, as on the worksite grid, never on the worker's stance. A target outside
the observation has no cue. The inherited breach farm's mining and water actions keep their unchanged
authority and offers; their effects reach the frame only through this observation. Actor sprites keep the
classic four-direction walk cycle and interpolation.

## Runtime binding: merchant appearance and stall choice

[`CargoPaperdollPlan`](../../client/scripts/Cargo/CargoPaperdollPlan.cs) is the predecessor compositor's
order for one source appearance: body (skin ramp), outfit sheets A and B (their outfit ramps), hair (hair
ramp), a race layer for non-human races (a dwarf's beard takes the hair ramp, elf or gnome ears the skin
ramp) and an accessory in outfit B's ramp. [`CargoMerchantAppearances`](../../client/scripts/Cargo/CargoMerchantAppearances.cs)
composes it from the exact copied layer sheets bound by `assets/cargo-circuit/merchant_layers.tres` and the
copied source palette manifest, through the shared `FarmPaperdollComposer`. Marla (elf) and Kegga Toolwright
(dwarf) are therefore their own original appearances, not Wren and not a generic merchant. This is an exact
import, not new art or accepted production style.

Stall identity follows [native trading](native-trading.md): quotes belong to a stall through the canonical
market record on the participant view, and the explicit choice is the draft's market identity. A stall
becomes the explicit choice only through the Stall selector, a field pick, or opening its panel while it is
in reach (a world's only stall is likewise chosen). Opening Offers with several stalls and none in reach only
looks at one: the selector stays on its prompt and closing the panel forgets it, so no arbitrary stall is
pinned. An explicit choice is never downgraded or retargeted; at Gearford with Marla chosen, the wheel's Buy and
Sell stay on Marla and their blocker names Kegga's stall and the Offers → Stall route.
[`CargoStallPick`](../../client/scripts/Cargo/CargoStallPick.cs) lets a left click on the field choose the
stall of the merchant drawn within one cell of the picked column, only when that merchant keeps exactly one
stall in view. The input router decides first: a pick belongs to focused, ready, ordinary exploration, never
to a failed or closing game, a conversation, encounter, pause or open modal. Picking asks, shows and spends nothing.

## Native qualification binding

[`CargoRoutePlayProbe`](../../client/scripts/Cargo/CargoRoutePlayProbe.cs) is the disposable keyboard route
over an exported fixture whose funds were earned through Wren's quest
([`NativeCargoFixtureTests`](../../tests/OpenFantasy.Hosting.Tests/Client/Cargo/NativeCargoFixtureTests.cs)).
It walks by holding injected arrow keys through the ordinary input map; read-only
[`LocalRouteAdvice`](../../src/OpenFantasy.Hosting/Simulation/CargoPresentation/LocalRouteAdvice.cs) only
chooses which key to hold from the bounded ground route, and the world validates every step. It walks the
farm approach and the authored corridor out and back, uses the production wheel, stall selector and panel to
buy, show, sell and inspect saved records, and samples projection agreement (presented feet, drawn player and
merchants, observed ground, camera limits and framing) along the way.
[`CargoRenderedEvidence`](../../client/scripts/Cargo/CargoRenderedEvidence.cs) adds what was actually
rendered: the settled actor node transform, the terrain the regional layer last drew against the presented
observation, and framebuffer pixels at the player's column and sprite, visible water and empty padding. The
probe also opens Offers at the farm before visiting Marla and runs one explicit saved-history verification
(`LocalWorldSession.VerifyRecordedHistoryAsync`, a complete verified replay in the background history slot)
while frames keep rendering. A separate paused process reopens and inspects both stalls' records without
advancing the world. [`CargoOnboardingPlayProbe`](../../client/scripts/Cargo/CargoOnboardingPlayProbe.cs)
uses the production `--cargo` selection on a fresh disposable save to earn Wren's reward by keys, mine the
breach divider through the wheel, observe the released water and walk to Marla.
[`check_native_cargo.py`](../../tools/check_native_cargo.py) runs them with a disposable no-focus project,
records observed window focus state and audits probe sources for focus requests, and
[`NativeCargoResultTests`](../../tests/OpenFantasy.Hosting.Tests/Client/Cargo/NativeCargoResultTests.cs)
authenticate both journals: actors, markets, prices, balances, lot intervals, coordinates, every player movement
sample as one cardinal 32-subunit step, and the onboarding quest, mining and water.

## Boundaries

- Finite admitted regions only. Streaming, frontier geography, discovery, maps and production load remain
  their own gates; the 24-cell window is a presentation bound, not a sight rule.
- One observed standing floor at a time, with a bounded per-column water aggregation from eight cells below
  its ground cell through the cell above it. This binding serves the exposed breach channels; general
  occlusion, hidden aquifers, overhead solids and construction-supported cutaways require explicit geometry
  and visibility rules before using it for those worlds.
- Keyboard qualification of the plain cargo route; the separate `--cargo-threat` selection
  ([road threat](road-threat.md)) uses this same presentation with keyboard, controller and pointer threat/retreat
  routes from a road fixture. Physical devices, OS focus, human art acceptance, escort, NPC travel and multiplayer
  remain open; the chief reuses the Squire prototype sprite.
