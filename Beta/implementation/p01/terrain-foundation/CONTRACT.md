# Retained terrain foundation v1

This increment supplies actual deterministic 3D occupancy, local candidate excavation, standing-space queries and strict snapshot round trips for the portable core. It is the small local substrate foundation for exploration. It does not close the macro-geology, watershed, historical reconstruction or geological-event requirements in [the technical design](../../../spec-v1_1/TECHNICAL_DESIGN.md#7-space-generation-and-streaming) and [geological world design](../../../spec-v1_1/GEOLOGICAL_WORLD_DESIGN.md).

The authored example numbers and material pins are calibration candidates. They do not map predecessor geography, infer source material stats, accept species art, or supply an imported playable world.

## Expected behavior established before implementation

| Case | Required result |
|---|---|
| Identical world/realm, seed, rules and cell addressed through different region bounds or generation orders | Same substrate role, material pin, formation ID and source ID |
| Adjacent columns across negative coordinates and an eight-cell lattice boundary | Common global lattice inputs; floor division, deterministic integer interpolation |
| Same seed on another branch | Same initial substrate/source identity; distinct scoped snapshot hash; no new export right |
| Changed world/realm, seed or pinned rules | Distinct generation context; no permission to replace existing lived terrain |
| Excavating remaining solid cells at the exact revision | New immutable candidate, revision increased once, original unchanged |
| Repeated/depleted/void/outside cell, stale revision, empty or oversized batch | Reject the complete candidate; no partial publication |
| Two tunnels at different Z under the same X/Y | Independent empty cells and supports; surface occupancy remains independent |
| Standing cell or required support outside the available region | `Unavailable`, never guessed empty/solid |
| Maximum signed extents, base relief/soil envelope, exhausted revision | Checked bounds; no overflow, wrapping or saturation |
| Encoding and loading edited state | Same exact bytes/hash and all excavation flags; no regrowth |
| Malformed, noncanonical, oversized or unsupported encoded state | `FormatException` before any returned snapshot |

## Concrete API and ownership

- `TerrainScope(WorldId, BranchId, RealmId)` identifies one authority scope. It is neither an authenticated capability nor a history-head/instant binding. The owning commit/checkpoint provides the latter metadata.
- `CellCoordinate(long X, long Y, long Z)` uses signed whole cells, with positive Z upward. Canonical order is Z, Y, X; X varies fastest. A cell is the same logical cell used by the core's 1/256-cell dimensions, independent of sprite pixels. Converting arbitrary global cells into signed subunits can exceed `Int64`; adapters must use checked local differences rather than unchecked global multiplication.
- `TerrainRegionBounds(minimum, width, depth, height)` has positive dimensions up to 32×32×16 and inclusive extents checked before use. `CoordinateAt` and `IndexOf` are exact inverse mappings within the region. This is the qualified operation/storage envelope, not a world-size cap. Neighboring regions are independent addresses; boundary queries need an explicit complete dependency set.
- `TerrainMaterialPalette(surface, soil, stone)` pins existing `CountedItemDefinition` values. A single exact material version may serve several roles. Different versions of one material ID in a palette reject. Content admission must resolve the pins through the real material catalog; mere hash shape does not establish accepted definitions.
- `StratifiedTerrainRules(baseSurfaceZ, relief, soilDepth, materials)` binds relief 0–8, soil depth 1–16 and exact material pins. Its `Version` is SHA-256 of canonical rules bytes, including the v1 tag. Every possible surface and soil boundary must fit signed 64-bit Z. There is no unpinned mutable generator setting.
- `new StratifiedTerrainGenerator().Generate(scope, seed, rules, bounds)` prepares fresh revision-zero terrain. It is stateless and does not accept an existing snapshot. Owners load existing terrain and apply edits; they must never replace it with this fresh baseline because another view requests the same area.
- `TerrainRegionSnapshot.GetCell(coordinate)` reads actual occupancy and original substrate metadata; outside coordinates reject. `QueryStanding(coordinate)` returns the four explicit results below. `Excavate(expectedRevision, coordinates)` returns a complete immutable candidate, with at most 256 unique cells per batch. `ComputeContentHash()` hashes its canonical snapshot.
- `TerrainRegionCodecV1.Encode/Decode` writes/restores actual state. `EncodeRules` exposes the exact pinned generation profile bytes. All output arrays are fresh; inputs and exposed cell collections cannot mutate snapshots.

## Generation algorithm and identity

Only the finite minimum local substrate is implemented. An eight-cell XY lattice supplies relief samples. For each global X/Y, use Euclidean floor division by eight and remainders 0–7, including at `Int64.MinValue`. The four containing lattice corners use the same context regardless of the requesting region. Their coordinates can increment safely because they are quotients of signed 64-bit cell coordinates by eight.

The canonical generation context frame is `terrain.generation.v1(worldId, realmId, seed, rulesVersion)`. The seed is exactly 128 bits; all bit patterns are valid. Branch is deliberately absent from initial source derivation. Each corner's `terrain.relief.v1(context, latticeX, latticeY)` SHA-256 digest supplies the first big-endian UInt64 modulo `(2×relief+1)`. Bilinear integer weights sum to 64. Floor the nonnegative weighted total divided by 64 once, subtract relief, then add the checked base Z. There are no floating-point operations, global random state, streaming-dependent draws or generation-order inputs.

This is small local relief only. It proves shared column inputs, including at horizontal and vertical region boundaries. It does **not** establish coherent drainage, finite outlet witnesses, tectonics, climates, lakes, a macro catchment or the GEOR-02 qualification.

At each X/Y, cells above the sampled surface are initial void. The surface cell is `Surface`; the next `soilDepth` cells below are `Soil`; lower cells are `Stone`. Solid cells are full unit cubes. They are stored and queried at their actual Z, not a height field masquerading as collision. There are no slabs, stairs, ramps, partial excavation shapes, liquids or magically supported placed floors.

For a nonvoid cell, the initial column/stratum formation identity is the first 16 bytes of SHA-256 over `terrain.formation.v1(context, originalX, originalY, stratumUInt64)`. Its substrate source identity is the first 16 bytes of SHA-256 over `terrain.source.v1(formationId, originalZ)`. Different domain tags separate the roles; a detected source/own-formation collision rejects generation. Inserting global authorities must still reject detected collisions with any existing identity. No mathematical uniqueness or global collision check is claimed.

These are stable **initial** substrate identities, not identities of a river, city, entire mountain range or moving continent. Depletion retains both identities and the original material pin/stratum. Future transported/deformed material must preserve these original identities and provenance, rather than derive replacements from destination coordinates. That future geometry requires an explicit newer representation; v1 does not claim to serialize moving formations or physical source budgets. A generator/rules revision cannot silently replace an established source.

## Occupancy, support and immutable edits

`TerrainCell` exposes initial `Stratum`, optional exact `Material`, optional `FormationId` and `SourceId`, and current `IsExcavated`. Initial void has none of the material/identity fields. `IsSolid` is true only for nonvoid, unexcavated substrate. A fully excavated cell retains its original substrate fields and is empty for the supported occupancy query.

`QueryStanding` is for a one-cell upright actor only:

1. Outside region → `Unavailable`.
2. Current cell is solid → `Blocked`.
3. Below coordinate cannot be represented or is outside region → `Unavailable`.
4. Empty current cell with a solid full cell directly below → `Supported`; otherwise → `Unsupported`.

This checks clearance and contact. It does not prove structural stability, bearing capacity, a route into the space, a valid step up/down, multi-cell body clearance, actor occupancy, breathable atmosphere, fluid safety or encounter admission. A blocked cell can be reported without loading its below neighbor. A clear boundary cell requires that dependency. Selected camera floor never changes the result.

Excavation validates revision, bounded count, distinct cells, in-region addresses and remaining solids before creating the new candidate. One accepted batch increments revision once. Input ordering does not affect resulting bytes. Independently ordered batches touching distinct cells yield the same occupancy; equal batch counts also yield equal revision/hash. Different batching has different revisions even when occupancy matches. Replayed requests fail either the expected revision guard or the already-depleted check. No command receipt/idempotency service is claimed here.

Every snapshot is immutable. The revision guard is against the supplied snapshot, not a concurrent database by itself. The owning world transaction must compare the currently stored revision and couple actual work, tool suitability/wear, body clearance, occupants, support dependencies, fluids, produced material and custody before it publishes this candidate. This method alone does not pay mining costs, grant ore, delete physical mass or authorize excavation. A cell is not implicitly one counted material unit; physical quantity and yield calibration remain explicit admission work.

Conserved construction must place real shapes/materials and source claims through that same transaction. It cannot fill cells by rerunning generation or resetting `IsExcavated`. This increment deliberately provides no material-free placement API. Its stacked-floor/tunnel test uses remaining natural strata as the separating floors; it does not certify artificial construction.

## Wire contract and loading

All frames use the existing canonical `OFC\0` magic, encoding version UInt32 1, exact UInt32 field count (including the tag), and length-prefixed fields. Integers are big endian. Signed coordinates/base Z use eight-byte two's-complement `Int64`; unsigned values use eight-byte `UInt64` except excavation indices, which are packed four-byte `UInt32`. Identities are 16 raw bytes and SHA-256 pins are 32 raw bytes.

| Frame tag | Fields after tag, in order |
|---|---|
| `terrain.material-pin.v1` | definition ID, definition hash |
| `terrain.scope.v1` | world ID, branch ID, realm ID |
| `terrain.rules.v1` | base surface Z, relief, soil depth, surface pin frame, soil pin frame, stone pin frame |
| `terrain.bounds.v1` | minimum X, minimum Y, minimum Z, width, depth, height |
| `terrain.region.v1` | scope frame, seed, rules frame, bounds frame, revision, packed excavated indices |

Rules and region hashes are SHA-256 over their entire canonical frames. The original cells are a pure derivation of the embedded inputs. The persisted edit field contains **every** excavated cell index in strictly increasing order. Loading validates schema/size/order before generating the bounded baseline, then reapplies all excavations. Indexing an original void rejects. Initial revision zero with an excavation rejects. Nonzero revisions need not equal the excavation count: a future owning transaction can advance a region revision for another consequential change, and this codec does not pretend to prove a commit history. The full unsigned revision range round trips, and incrementing the maximum throws.

Strict decoding bounds the top frame to 70,000 bytes, rules to 512, scope/bounds/pin frames to 128 each, cells to 16,384, and packed edits to four times the actual region cell count before allocation. Unknown tags/version/counts, wrong scalar lengths, nested trailing bytes, truncations, unsorted/duplicate/outside indices, invalid pins/rules, numeric overflow and top trailing bytes reject. The reader has no unbounded recursive structure or collection. Decoding does not authenticate the source, validate material catalog acceptance or prove that a historical excavation was authorized.

Region ownership must prevent concurrent overlapping authoritative region records from independently publishing inconsistent versions. Overlap tests prove generation/query consistency, not a multi-region transaction coordinator. The later store integration must atomically bind this canonical payload with its full world commit and all coupled domains; a separate terrain file/cache is not an additional authority. Loading a current stored region must choose this payload rather than recompute the fresh baseline. No new persistence service is introduced by this core increment.

## Retained validation

The independent Python stdout oracle implements framing, integer interpolation, hash identity derivation and fixture construction without invoking the C# implementation. Its frozen JSON contains five generation examples, 154 exact expected cells, 13 initial/edited/maximum-revision snapshot states and 29 deliberately malformed snapshots. On this worker, rerunning the oracle reproduced all **124,983 bytes** exactly; SHA-256 is `9b1dfeccc71642f0d96b50d41f5206e2880995f80ad85f0eee8a102a268c0074`.

The 18 authored .NET test cases cover exact cell/identity/byte/hash oracles; negative lattice edges and signed extrema; overlapping and vertically split regions; branch source reuse and scope isolation; bounds/index inverses; input/pin/overflow validation; immutable candidate edits; order/replay/depletion; stacked tunnels and support; missing boundary dependencies; maximum region depletion across 64 bounded batches; exhausted revision; all malformed vectors; every truncated prefix; nested header corruption; and ownership of arrays/collections.

This worker has not executed .NET or Godot; the central task coordinates builds to avoid concurrent toolchain contention. Python equality is actual worker evidence. Build/test and store/presentation evidence must be recorded by the coordinator after execution.

## Next integration obligations

1. Bind terrain payload reads/commits to the real world store, exact accepted content package/rule pins, world/branch/realm identity and pinned commit/instant. Retain edits across real process restart and region eviction.
2. Add a region dependency resolver for boundary support/traversal and multi-region edits. An unavailable neighbor must hold the relevant action, not assume geometry or truncate the write set.
3. Present the real occupancy in Godot with local coordinate rebasing; demonstrate walking/support and actual visible excavation from committed state. Add floor navigation, stairs/ramps and complete body clearance under their own tested shapes.
4. Couple mining work, material source quantities, yielded lots, costs, structural loads, occupants and hazards atomically. Implement conserved construction without resetting initial substrate or cloning source rights.
5. Extend the pinned generation model with retained macro constraints, historical feature/formation motion, rivers/caves, ecological/site constraints and fluid reservoirs through explicit versioned migrations and qualifications. Preserve established source identities and imported-content obligations.

No listed future item is closed merely by a passing terrain codec or an attractive rendered surface.
