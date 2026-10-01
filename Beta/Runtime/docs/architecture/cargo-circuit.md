# Cargo circuit: two retained markets on one walked route

The cargo world is a fresh, explicitly selected world in which the farm player earns money, buys an
actual lot from one retained merchant, carries it along a real local corridor and sells it to a second
retained merchant. It reuses every existing owner — counted custody, production, progression currency,
paid exchange, market books, the encounter lifecycle, the durable authority and the local session. It adds a
source binding, authored corridor geometry, a declared overlay set and declared provisions in
[Content.Cargo](../../src/OpenFantasy.Hosting/Content/Cargo), and transient walking advice in
[Simulation.Cargo](../../src/OpenFantasy.Hosting/Simulation/Cargo).

## Source binding

[`CargoCircuitSources`](../../src/OpenFantasy.Hosting/Content/Cargo/CargoCircuitSources.cs) reads the
retained packet's `npcs.json`, `items.json` and `world.json` through the content reader with their exact
file digests, then binds records by source identity:

| Source record | Retained facts | Binding rule |
|---|---|---|
| `npcs/marla` | tile (96,350), appearance, dialogue, shop list, no quests, wander 1 | Tile lies inside `regions/brasshollow` (94,339 33×27); shop lists `rations` |
| `npcs/kegga` | tile (347,140), appearance, dialogue, shop list, quest `kegga_timber`, wander 1 | Tile lies inside `regions/gearford` (344,127 33×27); shop lists `rations` |
| `items/rations` | price 25 | Equals the market rules' cited `MarketSourcePrice` item, price and file digest |
| `world/regions/*` | all 25 retained place rectangles | Read-only with pointer and payload hash; unique identities; used for the overlay check below |

Each bound record keeps its JSON pointer and canonical payload hash. A missing or duplicated identity, a
tile outside its place, a shop list without the shared good, a cited price or file that differs, or any
changed source byte is refused; nothing is repaired or substituted. Dialogue, Kegga's quest and the other
shop goods are retained facts only: they are not runtime utterances, quests or stock in this world.

## Fresh admission

[`CargoWorldAdmission`](../../src/OpenFantasy.Hosting/Content/Cargo/CargoWorldAdmission.cs) composes:

1. The unplayed ordinary-care farm genesis: Wren, her funded quest, crafting, care and the two wolves.
2. [`CargoGenesisExtension`](../../src/OpenFantasy.Hosting/Content/Cargo/CargoGenesisExtension.cs): the
   corridor terrain and both merchants. Each owner receives its own record — presence at the source tile
   with a home-place identity derived from the source place, a resident, a store, a control binding, an
   owner policy, a progression state, a currency account, a gathering work slot, a construction exit and a
   lifecycle debt row. Anatomy and the NPC stat profile are Wren's admitted definitions, named as explicit
   transforms because the source records carry none; the wander radius is not admitted.
3. Paid exchange, then the two counters as markets
   ([`CargoCircuitPolicy`](../../src/OpenFantasy.Hosting/Content/Cargo/CargoCircuitPolicy.cs)): two distinct
   versioned rules over distinct stores, both citing the unchanged rations price.

The extension's admission identity is a canonical frame (the core `OFC` version-one layout, mirrored in
[`CargoCanonicalFrame`](../../src/OpenFantasy.Hosting/Content/Cargo/CargoCanonicalFrame.cs)) of the basis
hash, every added region as explicit signed 64-bit minimum and extent fields, each merchant's source,
place and provision hashes, the shared good and the declared overlay version. No culture-dependent or
compiler-generated text is hashed. Composition is byte-deterministic for one seed and player name.

### Inherited encounters

The lifecycle owner requires every scenario to name every resident, so both wolf scenarios gain Marla
and Kegga as side-zero bystanders with no abilities. Existing participants, target, abilities and entry range are
unchanged, but each **scenario version differs** from the farm's, because the version covers the combatant
list. An entered encounter's tactical timing and lifecycle phase are **world-wide**: every resident,
including Kegga hundreds of cells away, moves to tactical timing until release. The wolves remain
player-initiated only, so they are ordinary farm encounters, not threats to cargo in transit. A fought and
released wolf leaves both merchants' HP, feet, presence revision, stock, purse and readiness debt unchanged,
and both counters answer afterwards; the session refuses walking during the encounter and the player's
retained readiness debt refuses movement after release until ordinary exploration pays it.

### Provisions and opening stock

Declared provisions ([`CargoMerchantProvision`](../../src/OpenFantasy.Hosting/Content/Cargo/CargoMerchantProvision.cs))
are new finite authoring with canonical versions. Brasshollow's opening shelf is one admitted **raw counted
source** of the farm's own `craft_rations` output definition: eight units under the derived role identity
`marla.opening-stock-source`. It is not produced in this world and not relabelled from berries; rations the
farm makes are transformed lots with production output intervals, so one definition has two honest provenance
kinds. `CargoWorldAdmission.OpeningStock` derives the expected role from supplied Host authoring and checks
the admitted source capacity and recipe output definition. Its returned provision version comes from that
authoring; it is not independently recovered or authenticated from the save. Callers must select compatible
authoring. The save holds the admitted source and the admission hash covering the provision version, but
no independently queryable durable provision record. Historical authoring lookup requires an explicit
version interpretation; no production lineage is claimed. Gearford's purse is new admitted currency supply (eighty gil) in Kegga's own account. The
legacy quest reserve remains Wren's reward reserve; the player starts with no money.

### Water owners

The water owners pin the exact counted admission and terrain. Adding residents and sources therefore
re-prepares the retained dry capacity domain against the extended inventory (same network, mapping, owner,
material, terminals and geometry) and rebinds the placed cells to it and to the extended regions. The closed
budget identity is read from the retained capacity state. The re-preparation is lossless only for the fresh
dry genesis, so the extension first proves that the same preparation against the retained pins reproduces the
retained capacity and reservoir hashes exactly; pending runoff, received totals, another closed budget or any
lived reservoir state is refused rather than reset. Reservoir geometry, projected units, closed budgets,
receiver totals, capacity revision, seals, cell quantities, portions, domain cells, boundary and liquid
topology (nominal and open cells, open capacity) stay equal; only the inventory pins change.

The extension refuses any played, held or otherwise interpreted root, and it never upgrades a save: it is
fresh selection, not migration. Migrating a lived farm into a cargo world would need its own explicit
transition with replayed history.

## Corridor geometry and retained places

[`CargoCorridor`](../../src/OpenFantasy.Hosting/Content/Cargo/CargoCorridor.cs) authors an L of fourteen
full-size regions: the farm region, seven east along its rows and six south along the last column. With the
farm's retained breach receiver the world holds fifteen of the fixed sixteen regions. Added regions use the
farm region's own seed and stratified rules, so they are ordinary flat support; every retained region,
excavation and revision is kept exactly. Standing cells one diagonal step from each merchant tile serve as
approaches; authored waypoints between them are at most eight cells apart (457 cells approach to approach,
461 between the source tiles). The lane runs along y=349 from x=97 to x=346, then along x=346 down to y=141.

Source tiles and cells share one plane, so the corridor overlays retained places.
[`CargoCorridorOverlays`](../../src/OpenFantasy.Hosting/Content/Cargo/CargoCorridorOverlays.cs) enumerates
every `world.json` place intersecting the inherited farm region, an added region or a lane segment, and the
corridor is refused unless that set equals the declared, versioned set exactly (an undeclared, changed or
vanished overlay fails):

| Place | Footprint (tiles) | Lane crossing |
|---|---|---|
| `brasshollow` | inherited farm 403 of 891 | y=349, x∈[97,127) |
| `tavern_brasshollow` | inherited farm 63 of 70 | — |
| `farm_verdance_small` | inherited farm 63 of 63 (full) | — |
| `gearford` | added 208 of 891 | x=346, y∈[141,154) |
| `tavern_gearford` | added 49 of 70 | — |
| `castle_aldermere` | added 406 of 667 (rows 320–333) | — |
| `standing_stones` | added 12 of 380 | — |
| `bandit_camp` | added 84 of 180 | **x=346, y∈[305,317)** |
| `farm_sylvas_medium` | added 108 of 108 (full) | x=346, y∈[330,339) |
| `port_gearford` | added 140 of 140 (full) | — |

The inherited rows are the farm admission's existing mapping; the walked approach from the player's start
crosses only inherited farm ground. Over every added place the corridor is ordinary flat ground: no town,
castle, port, farm or hostile terrain, resident, structure or encounter of that place is imported. This is
finite local qualification geometry, not procedural geography, streaming, a town layout or an
infinite-frontier result.

## Itinerary ownership and restart

[`CargoLegCoordinator`](../../src/OpenFantasy.Hosting/Simulation/Cargo/CargoLegCoordinator.cs) is host
advice for one walking actor. For each leg it queries the unchanged bounded ground search from the actor's
actual feet and issues one ordinary 32-subunit movement sample per cutoff; the world and session decide
whether the step happens, with full body occupancy, support and actor availability. If a sample did not move
the actor, it replans once from the actual state; a leg that cannot be planned, or a replanned step refused
again, stops with the search status and no movement.

A stop is latched to the situation it was decided in: the walker's feet, presence revision and ground
locomotion; bodies within sixteen cells of the walker or the leg's waypoint; terrain region revisions and
support modes; structure identities and collider/bearing geometry; and, when advised from a world, the walker's movement admission (encounter phase,
readiness, pending consequences). An unchanged situation returns the same stop with no query or sample, so
the coordinator never cycles refused samples; a fresh snapshot object or a world tick does not release it, and
a relevant change revalidates from actual state. Missing coverage (`Unavailable`) and an exhausted budget
(`ExpansionLimit`) are reported as such, never as proof that no route exists. The coordinator owns no
authority and is not saved: a reopened session resumes from the recorded position with a new coordinator.
No position is set directly, no budget is enlarged and no leg is free.

## Trading along the route

Markets remain mobile actor-markets ([world markets](world-markets.md)). The player reads, asks and accepts
only in person. The authority enforces reach: an offer captured while open and submitted from beyond reach
inside its window is refused `OutOfReach`, with balances, stock and the open quote unchanged; hand-built
distant observation and showing commands are refused `OutOfReach` and create no knowledge or disclosure.
Another actor's commands are refused at dispatch or as `NotAuthorized`. A remote price is the player's own
dated memory, aged but never refreshed by travel. Selling at the far counter uses the ordinary showing of the
exact carried lot. An offer carried until its window closes is refused `Unavailable` and nothing requotes it.
Lot lineage is the custody owner's source interval: the carried and delivered lot keep the shelf source's
identity and a sub-interval of it, and each settlement moves exactly the quoted total between the named
buyer and seller accounts.

## Presentation and remaining joins

The fixed farm renderer is not used for this world. Native presentation must consume actual observed
regions with camera-relative drawing, distinguish several stalls by identity and let the player choose one.
The corridor threat join must admit an encounter whose entry geometry and aftermath lie on the
`bandit_camp` crossing cells (x=346, y∈[305,317)); escort and guard wages, traveling NPC traders, lived
migration and multiplayer are separate joins, and no narration stands in for them. The sixteen-region local
bound is fixed and fifteen are used, so a corridor encounter footprint, escort staging or town layout needs
reshaped geometry or streamed regions (the infinite-frontier gate), not another added region.
