# Identity and determinism

## Identity and canonical bytes

[EntityId](../../src/OpenFantasy.Core/EntityId.cs) is a 128-bit value, canonically compared in
big-endian order and displayed as 32 lowercase hexadecimal characters. Zero is representable;
admission rules decide whether a particular role permits it. Generated identity derives from a
framed seed, generator version, stable address, role and ordinal. Live-created identity derives
from an accepted cause and spawn ordinal. Both use the first 16 bytes of SHA-256; owners reject
detected collisions rather than assuming hashes make collision checks unnecessary.

Definition keys, immutable version hashes, ancestral identity, branch incarnation and live instance
identity are different concepts. Two branches can refer to the same ancestral material without
receiving two export allowances. World, branch and realm are explicit scope, not incidental labels.

[CanonicalEncodingV1](../../src/OpenFantasy.Core/CanonicalEncodingV1.cs) frames primitive fields:

| Element | Encoding |
| --- | --- |
| Header | `OFC` followed by a zero byte |
| Encoding version | Unsigned 32-bit big-endian value `1` |
| Field count | Unsigned 32-bit big-endian count, including the domain tag |
| Each field | Unsigned 32-bit big-endian byte length followed by exactly those bytes |
| First field | Exact domain tag |

Fields do not use delimiter splitting. Embedded NULs and separators are ordinary field data.
Text uses strict UTF-8, without BOM or normalization; invalid surrogate input rejects. Domain
codecs define their own tags, versions, field bounds and integer interpretation. Unknown versions,
trailing fields and malformed encodings reject rather than fall through to a permissive parser.
The [primitive contract](../../src/OpenFantasy.Core/CONTRACTS.md) owns exact derivation field order
and golden-vector semantics; this reference does not redefine its wire format.

[WorldSnapshot](../../src/OpenFantasy.Core/Worlds/WorldSnapshot.cs) memoizes its canonical digest per
immutable root. Concurrent first readers can compute independently; atomic reference publication
retains one complete immutable `ContentHash`. A successor owns a new cache. The cache is neither a
serialized field nor an admission shortcut: encoded inputs are fixed before validation can request
the digest, and persistence still encodes and verifies full canonical bytes. Mutable encoding buffers
are not retained or shared by this cache.

[TerrainRegionCodecV1](../../src/OpenFantasy.Core/Terrain/TerrainRegionCodecV1.cs) memoizes a region's
canonical encoding by the exact immutable `TerrainRegionSnapshot` object, using weak keys. Repeated
world encodings reuse unchanged region bytes without rescanning their cells. Separate snapshots with
equal bounds and revisions still have separate encodings; excavation creates a new snapshot. Each
public export returns its own byte array, and concurrent requests cannot mutate the retained bytes.
This cache changes no encoded field, version, admission rule or decoder refusal. Recovery still
replays and authenticates every journal entry and compares complete canonical states.

## Numeric domains

Authoritative geometry uses signed 64-bit coordinates at 1/256 cell. The physical profile binds
one cell to one metre. Realm identity is independent of Z. Typed material quantities distinguish
mass, volume, counted units and source intervals; equal underlying integer types do not make them
interchangeable. Render coordinates can be floating point without becoming movement authority.

[RuleMath](../../src/OpenFantasy.Core/RuleMath.cs) uses checked arithmetic and signed 128-bit
intermediates for basis-point multiplication, dividing by 10,000 toward zero. Positive scheduling
durations round upward where the rule calls for a ceiling. Caps and minimum costs are explicit
rule stages; overflow rejects instead of wrapping or silently saturating.

[WorldInstant](../../src/OpenFantasy.Core/WorldInstant.cs) stores signed 64-bit days plus canonical
milliseconds within an 86,400,000-millisecond day. Arithmetic normalizes negative instants with floor
semantics and rejects representational overflow. Calendar representation does not prescribe the
relationship between real time and an actor's exposure; that belongs to [time policy](time-and-encounters.md).

## Randomness and ordering

[CounterRandomV1](../../src/OpenFantasy.Core/CounterRandomV1.cs) uses SHA-256 counter streams keyed
by seed, rules version, domain and responsible identity. A draw reads the first eight digest bytes
as an unsigned big-endian integer. Bounded draws use rejection sampling with threshold
`2^64 mod bound`. The counter records every raw draw, including rejected draws; bound one still
consumes a draw. The maximum counter is usable once and then exhausts. These are game reproducibility
contracts, not cryptographic security services.

The counter generator exposes capture/restore state. `WorldSnapshot` does not contain an RNG-counter
component; accepted local NPC movement samples are replay inputs. The **integration design contract**
requires authoritative consumers to stage and persist counter changes with their owning outcomes.
Separate domains isolate generation, actor planning, loot and research; purely cosmetic variation
does not mutate authoritative streams.

Stable IDs, explicit ordinal comparisons, sorted collections and versioned tie-breaking determine
serialization and algorithm order. Runtime hash codes and dictionary enumeration are not durable
identifiers or scheduling rules.

Accepted external decisions and movement samples are replay inputs. Recovery does not rerun an
NPC controller and hope it makes the same choices. Likewise, differently timed human input need
not produce the same outcome merely because the world seed matches.

## Extension contract

A change to bytes, field order, rounding, RNG consumption, tie-breaking, generated identity or
rules meaning is a compatibility decision. Preserve the old interpreter or supply an explicit
validated migration. Performance work must retain the exact contract or introduce a separately
pinned version; a matching partial hash cannot prove full-world equivalence.
