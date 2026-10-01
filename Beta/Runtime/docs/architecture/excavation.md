# Local excavation

## Ownership and source lineage

[ExcavationSnapshot](../../src/OpenFantasy.Core/Terrain/ExcavationSnapshot.cs) owns a finite catalog of
one through 32 [sites](../../src/OpenFantasy.Core/Terrain/ExcavationSite.cs). Each site binds one retained
stratified terrain cell, its original terrain-source identity, one distinct counted-source entitlement,
an authorized actor, exact item/physical calibration, required tool item, work threshold and adjacent
stance. Cells, terrain sources and counted sources are each exclusive within the catalog. The same item
requires the same physical calibration across sites and Production recipes.

Terrain ancestry and counted custody retain distinct identity roles. The site explicitly relates them;
it does not make every terrain source an admitted inventory source. The counted entitlement is present
at genesis but has no issued claims while its cell is solid. Whole-cell volume is 1,000,000,000 microlitres.
The positive per-unit calibration must divide that quantity exactly. Total mass must fit the supported
integer envelope. Required work is an explicit content choice from one through 4,096 actions; this
contract does not derive universal mining speed from hardness or tool power.

[WorldExcavationAdmission](../../src/OpenFantasy.Core/Worlds/WorldExcavationAdmission.cs) creates a fresh
profile-ten construction world with unworked sites. The admission version pins the prior admission and
canonical initial catalog. Existing saves do not acquire extraction rights when opened. This profile
excludes watershed and reservoir owners. Its terrain interpreter remains the exact stratified generator,
not the separate macro-to-local material registry.

## Work transaction

[ExcavateIntent](../../src/OpenFantasy.Core/Terrain/ExcavateIntent.cs) names the admitted site, expected
site and terrain-region revisions, exact tool lot/revision and destination inventory revision. The outer
command pins the actor revision and trusted control delivery. Players and NPCs use the same request.
The site owner is the permitted worker; permissions do not imply shared-party extraction rights.

[ExcavationPlanner](../../src/OpenFantasy.Core/Terrain/ExcavationPlanner.cs) uses sequential current
components from the [world processor](../../src/OpenFantasy.Core/Worlds/WorldStepProcessor.cs). Work
requires an advancing, unheld action opportunity, manipulation, an owned suitable unreserved tool and
400 banked effort under the shared gathering effort rules. Success spends effort, increments site work
once and suppresses that actor's movement sample for the selected step. The normal advancing step earns
its ordinary effort and personal/calendar exposure once. Held interactions earn no work or effort.

The original same-level stance is the centered, grounded cell beside a horizontal face of the target. It must be reachable
and supported. [Access](../../src/OpenFantasy.Core/Terrain/ExcavationAccess.cs) also traces directly from
the actor's actual body center to a point one spatial subunit outside that target face, under the same
448-subunit horizontal reach limit. The target remains solid during this test. Intervening terrain and installed
structure collision occlude the segment; the stance cannot extend reach or bend it around a wall.
This is a point reach test, not a swept animation or a guarantee that every part of the face is exposed.

Every work attempt prepares potential whole-cell removal and reconstructs the complete spatial set.
Earlier profiles require clearance/support for every actor and installed structure without displacement.
The [intact settlement profile](structural-settlement.md) instead prepares complete bounded harmless descent
before constructing the stable spatial root; unhandled motion still refuses the action. Gathering anchors
must also retain support. Such refusal preserves earned excavation progress, material and work effort.
The check proves direct support under the local spatial rules; it does not adjudicate structural loads,
collapse debris, safe routes after removal or a dynamically expanding hazard region.

When the world retains [physical construction members](construction-physical.md), that proposed complete
hole must preserve terrain support for every exact connected component. The check precedes partial labor
as well as final extraction; the original assembly's lowest collider cannot stand in for a detached higher
component. The ordinary candidate-action gate also validates physical contact before issuing identities
or successful receipts. An unsupported consequence is refused until its complete collapse contract exists.

Until the work threshold is reached, the published terrain and inventory remain unchanged. Completion
creates exactly one deterministic raw lot covering the entire admitted source interval and places it in
the worker's primary inventory. Terrain removal, progress, effort, inventory/storage revisions and the
command receipt publish in one ordinary durable world commit. Combined raw/transformed capacities,
protection, historical identity collisions and numeric bounds still apply. Failed completion retains the
prior work and solid cell. Repeating a completed site's action cannot issue more material.

The [terrain-bound liquid composition](terrain-liquid-breach.md) adds an explicit raised-bank top face
and proves the eventual opening before partial work. It publishes changed void capacity and conserved
water placement together with substrate issuance. The V2 extraction archive retains the reach mode;
old all-side site archives and interpreter meanings remain unchanged.

## Conservation and continued use

The local session admits ordinary excavation in the
[saved-proposal composition](material-proposals.md#excavation-composition),
[paid exchange](world-paid-exchange.md) and [local markets](world-markets.md) through the same advancing
cutoff. Their fixed reservoir seals and terrain-bound liquids exclude deferred structural consequences;
neither an open offer nor a larger profile number selects that separate consequence interpreter.

World admission and decoding validate the same relation as execution:

- An incomplete site retains its solid cell and has no live or directly retired source claims.
- A completed site retains its excavated cell, and sorted live raw plus direct retired intervals cover
  exactly `[0, quantity)` without holes or overlaps.
- A gathering cursor cannot own that counted source.
- Each owner's combined construction and excavation actions fit advancing steps and experienced time.

Ordinary transfer and crafting can use extracted stock. Direct raw retirement preserves source coverage;
recrafting a derived object does not count its ancestral inputs again. The physical hole stays excavated
when the extracted material changes custody or form. This contract does not replace the production ledger
or create a second spendable representation of terrain.

The [joined excavation material owner](excavation-material-continuation.md) extends this coverage across
explicit Production V8 boundaries. Partial mining and gathering both record earned work and shared
effort in their actual order, even when custody does not change. Its component archive retains current
terrain and actor action frontiers. That contract does not select a continued complete-world profile.

## Persistence and boundaries

[World snapshot ten](../../src/OpenFantasy.Core/Worlds/WorldSnapshotCodecV10.cs) wraps a genuine
profile-five basis and [ExcavationCodecV1](../../src/OpenFantasy.Core/Terrain/ExcavationCodecV1.cs). Terrain
edits retain their existing region codec. The world remains bounded to 16 MiB and uses the same exclusive
schema-three writer, receipt handling, restart replay and exact recorded-commit history.

The catalog frame `terrain.excavation.v1` contains canonical site-ID ordered rows, at most 32,768 bytes.
Each `terrain.excavation-site.v1` row contains site ID, signed cell XYZ, terrain-source ID, counted-source
ID, owner ID, item pin, physical material archive, tool pin, required work, completed work and signed stance
XYZ. Coordinates are signed big-endian 64-bit values, work is unsigned 64-bit, identities are 16 bytes and
item pins are 48 bytes. Site revision equals completed work. Decode validates values and exact re-encoding.
Action version eight contains the six intent fields above under `world.action.excavate`, version one;
its command bound is 512 bytes. Step profiles 10–18, 21–28, 30, 32 and 33 admit it. The local adapter
keeps privileged paired profile 28 separate. Older encodings keep their meanings.

The [constructed-footing composition](constructed-footing.md) retains excavation with explicit
installed-floor support and assisted traversal. Excavation preserves that support interpretation when
reconstructing terrain and occupancy; it cannot discard a constructed support dependency. Structure
bases remain terrain-grounded, and gathering anchors retain their terrain-only support contract.

The standalone [structural failure planner](structural-loads.md) provides rated load propagation and
canonical bounded continuations. It does not replace excavation's direct-support refusal. Physical
collapse requires a complete coupled publication with construction lifecycle, rechecked occupant
contact, falling or checked landings, and conserved debris ownership.

Selected dry-cell extraction supplies a durable integration boundary for material and local geometry.
General mining still requires variable tools/hardness, accessible vertical travel, extraction catalogs
prepared from world generation, broader rights and work allocation, structural failure, breach/spill
consequences and composed water policy. Reservoir-bearing worlds require an explicit composition that
preserves liquid custody through changed topology; diagnostic sealing policy cannot silently become
simulation law. Navigation affordances, mining presentation and production carry-mass balance belong to
their corresponding gameplay contracts.
