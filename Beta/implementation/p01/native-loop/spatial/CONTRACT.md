# Native-loop spatial foundation

This retained core increment admits a finite local set of actual terrain and corporeal actor bodies, queries their geometry, and prepares bounded walking candidates. Player and NPC use the same types. It does not authenticate controllers, advance time, execute a world action, commit persistence, or implement a dialogue/vision/structural simulation. The parent world authority composes those responsibilities and owns canonical root decoding.

## Public values and operations

```csharp
new SubunitPosition(long x, long y, long z);
new ActorPresence(ActorFormBinding binding, BodyConditionSnapshot condition,
    TerrainScope scope, SubunitPosition feet, ulong revision, EntityId homePlaceId);
new LocalSpatialSnapshot(TerrainScope scope,
    IEnumerable<TerrainRegionSnapshot> regions, IEnumerable<ActorPresence> actors);

ActorPresence actor = spatial.GetActor(actorId);
SpatialOccupancy occupancy = spatial.QueryOccupancy(actorId, candidateFeet);
SpatialMoveResult result = spatial.PrepareMove(actorId, expectedActorRevision, deltaX, deltaY);
SpatialInteraction interaction = spatial.QueryInteraction(actorId, targetActorId);
```

`ActorPresence` exposes `ActorId`, `Binding`, `Condition`, `Scope`, `Feet`, `Revision`, and `HomePlaceId`. It verifies form/condition instance and exact body pins through the existing `ActorFormBinding.ValidateCondition`. Home cannot alias its actor/form/body/tether. Construction is metadata, not a placement or control grant. It does not clone inventories or create a second actor for the NPC controller.

`LocalSpatialSnapshot` exposes `Scope`, `Regions` in canonical minimum-coordinate order (Z/Y/X), `Actors` sorted by actor ID, and `GetActor`. It copies inputs and returns read-only collections of immutable values. Each method reads one fixed snapshot; successful movement returns a new **presence candidate**, leaving the original actor and spatial state untouched. The outer authority must form, validate and atomically commit its next complete root.

## Finite local admission

The initial profile supports at most **16 non-overlapping terrain regions**, **64 actors**, **2,048 subunits per body axis**, and **256 subunits per movement attempt**. Each existing terrain region retains its own 32×32×16 maximum. Counts reject at the first excessive item; data is never truncated. These are qualification envelopes, not population/world-size limits. The authoritative owner must supply every terrain region and actor whose occupancy could affect the local operation. Omitting a relevant actor is not made legitimate by constructing this value.

Terrain scope must match exactly in world, branch and realm. All regions use the same seed and exact generation rules hash. Their inclusive cell bounds may touch at neighboring faces but must not share any cell. Different region revisions are legal: independently edited regions still share initial generation rules. There is no implicit grid snapping, height flattening, regeneration or reconciliation of contradictory terrain copies.

Only corporeal form bindings are admitted into this initial collision profile. Actual full body dimensions are used, and every admitted actor must have complete clear volume, solid footprint contact and no positive-volume overlap with another actor. An injury that removes ground locomotion does not remove the body: an immobile injured actor remains valid stationary occupancy. Incorporeal, flying and other unsupported movement profiles require explicit later admission/geometry rules; they are not silently ignored or assigned a humanoid collider.

Actor IDs, form instances and physical body instances are unique. Known world, branch, realm, actor, form, body, home-place and body-definition roles cannot alias. Several actors may reference the same home or exact body definition. One definition ID cannot have different body hashes within the set. This is a local known-identity check; the root remains responsible for the world-wide registry, source/material IDs and historical identity rights.

Out-of-scope actors, unsupported forms, duplicate/overlapping regions or actors, incompatible pins, missing placement dependencies and invalid dimensions reject construction. Unrepresentable body bounds throw `OverflowException`; nothing clamps an actor into the world. Actual dynamic falls, forced displacement, collapse or loss of support require a later state-transition profile rather than admitting an impossible grounded state.

## Exact body geometry

`SubunitPosition` has signed 64-bit X/Y/Z and exactly 256 subunits per cell. Z is feet elevation; XY is the body's placement anchor. Negative cell conversion uses floor division, including `Int64.MinValue`. Explicit offsets and full body bounds use checked arithmetic. This is independent of sprite pixels, camera floor, facing animation and Godot physics.

Bodies are half-open axis-aligned boxes:

- X: `[feet.X − floor(width/2), feet.X + ceil(width/2))`.
- Y: `[feet.Y − floor(depth/2), feet.Y + ceil(depth/2))`.
- Z: `[feet.Z, feet.Z + height)`.

An odd dimension places its extra subunit on the positive side. No rounding changes the requested size. Positive-volume intersection blocks; touching a boundary alone does not make bodies/solid cells overlap. The maximum occupied cell uses `floor((exclusiveMaximum−1)/256)`, preserving exact half-open semantics at cell edges. The original humanoid candidate is 180×140×464 subunits: it occupies two vertical cells above its feet. No one-cell helper or shrunken collider substitutes for that headroom.

Direct support is deliberately conservative: feet must lie exactly on a whole-cell top plane, and **every** cell touched by the XY footprint directly below that plane must be a remaining full solid cube. Partial overhang, diagonal support, bodies-as-platforms and partial shapes are unsupported. This establishes contact, not structural bearing capacity or route reachability.

`QueryOccupancy` uses the selected actor's actual body dimensions at the candidate feet and excludes only that actor's current volume. It reads the complete bounded body/support set. Result priority is `Unavailable` if any required terrain cell is missing, then `BlockedTerrain`, then `BlockedActor`, then `Unsupported`; otherwise `Clear`. Missing state never becomes air or solid by assumption. Invalid actor IDs and unrepresentable query bounds are caller errors, not claims about hidden gameplay state.

## Cardinal walking candidate

`PrepareMove` requires exactly one nonzero XY displacement, magnitude at most 256. It cannot change Z. Zero displacement, diagonals and excessive requests throw instead of being clamped. The exact actor revision must match; otherwise `RevisionConflict` returns no candidate. The body's current condition must satisfy its explicit `LocomotionMode.Ground` requirement; otherwise it returns `NoGroundLocomotion` with the stationary body preserved. This is anatomy-derived locomotion only, not a general action or learned-power grant.

The union of the source and destination AABBs is the exact swept volume for this cardinal, fixed-orientation move. All its terrain cells, intersected actor volumes and complete swept footprint supports are checked, so endpoint-only clearance cannot pass through a thin actor or skip missing floor. A maximum-size body and one-cell step cover at most 810 candidate volume cells, plus bounded footprint support cells. All iteration limits follow the admitted dimensions and move bound.

Clear movement returns `SpatialMoveStatus.Prepared` and a new actor presence with unchanged identity/form/body condition/scope/home, destination feet and actor revision increased by one. Failed geometry maps directly to the corresponding movement status, with null candidate. Coordinate or revision overflow returns `NumericLimit` and no candidate. There is no clock cost, receipt, movement queue, energy debit or persistence side effect here. The world owner must revalidate the full root, current control and step allowance before commit; repeated direct calls do not establish extra allowed movement.

## Interaction geometry and occlusion

Actors must already belong to the same admitted scope. Self-interaction and unknown IDs are caller errors. Distinct feet elevations return `DifferentFloor`; the current profile does not reach between floors. Exact XY center distance must be at most **768 subunits** (three cells), inclusive. This is the proposed spatial profile for the source's three-tile talking range; it is not gathering range, perception radius, arm reach or a magical speech range.

True AABB centers can lie on half subunits. The implementation doubles all center coordinates, preserving those halves exactly. It subtracts in Int128, rejects excessive absolute XY differences **before squaring**, and compares bounded squared distance against `(2×768)²`. Opposite signed extremes cannot overflow a squared-distance shortcut. Same-feet Z and bounded body heights also bound the vertical center difference.

The center-to-center segment is clipped against full closed terrain-cell cubes using exact rational entry/exit parameters and checked Int128 cross products. This is conservative occlusion: touching a solid face, edge or corner blocks the segment. Candidate cells include both sides of an exact grid boundary. Only cells actually touched by the segment affect the result; an unrelated missing cell inside the segment's bounding box does not block it. Any touched missing cell yields `Unavailable`; otherwise a touched solid cell yields `Occluded`, and a clear segment yields `Reachable`. Reversing the endpoints preserves the result.

The segment uses actual center Z for each body, not a flattened XY test. Actors block movement but are not sightline occluders in this initial profile. This geometry result does not prove sight/hearing, comprehension, conversation consent, knowledge, a quest offer or transfer permission. The root's interaction action supplies those separate prerequisites and listener-scoped outcomes. Blind/hearing-impaired actors are not silently healed by `Reachable`.

## Retained tests and integration boundary

The authored 26 Facts exercise negative/extreme floor conversion; actual humanoid headroom; exact support and odd half-open extents; horizontal/vertical region dependencies; incompatible scope/seed/rules; identity/form/condition failures; noncorporeal/oversized bodies; copied/sorted maximum collections; unchanged movement candidates; player/NPC parity; swept thin-actor collision; touching versus overlap; support/ceiling/boundary failures; injury-derived immobility; invalid cardinal displacement and overflow; maximum body volume; inclusive/circular/half-subunit range; different floors; actual wall excavation; face/corner occlusion; missing versus unrelated cells; signed extremes; vertical sightline clipping; and malformed caller inputs.

The supporting terrain fixtures use the retained generator and real immutable excavation, not a second fake occupancy map. This worker has not run .NET or Godot; the coordinator owns the integrated build. No new clock, authentication code, runtime codec, fixture loader, project file, journal writer or client authority is introduced in this worker's scope. The parent root decoder will compose these validated types with existing terrain and the separate body decoder, and must preserve their full input pins/identities/revisions across restart.

The next native proof must still execute fixed-step movement and interaction through the same durable world root, show only permitted projections, and validate restart/retry/clock/readiness behavior. Passing pure geometry tests does not close P01/P02, actor simulation, four-player authority, traversal breadth, native art acceptance or the Wren quest.
