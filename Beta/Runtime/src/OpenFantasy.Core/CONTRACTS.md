# Portable primitives, version 1

These retained primitives have no Godot dependency. This document describes their byte and numeric contracts. Domain modules extend them; the primitives themselves do not own world state, saves, scheduling, content migration or transport. Host code must stage RNG changes with its future authority transaction. Each RNG stream has one owner; sharing mutable streams between workers is invalid.

## Canonical binary encoding

The v1 frame is the four bytes `4f 46 43 00` (`OFC` plus NUL), unsigned 32-bit big-endian encoding version `1`, unsigned 32-bit big-endian field count **including the tag**, then each field as unsigned 32-bit big-endian byte length followed by those bytes. The first field is the tag. Integers are fixed-width unsigned 64-bit big-endian fields unless stated otherwise. Text is strict UTF-8, without BOM or implicit Unicode normalization. Invalid UTF-16 surrogate input rejects instead of being replaced. Text identity fields and world seeds must be nonempty. Embedded text separators and NULs are ordinary bytes, never field boundaries. Changing any encoding requires an explicit version migration.

| Operation | Tag | Ordered fields after tag | Result |
|---|---|---|---|
| Generated identity | `entity.generated` | seed bytes; generator version text; stable source address text; role text; ordinal UInt64 | First 16 bytes of SHA-256(frame) |
| Live spawned identity | `entity.spawned` | accepted cause identity, 16 canonical bytes; spawn ordinal UInt64 | First 16 bytes of SHA-256(frame) |
| RNG stream fingerprint | `rng.sha256-counter.key` | seed bytes; rules version text; domain text; responsible identity, 16 canonical bytes | All 32 SHA-256 bytes as 64 lowercase hex characters |
| RNG draw | `rng.sha256-counter.draw` | same four key fields; draw counter UInt64 | First 8 bytes of SHA-256(frame), read as UInt64 big-endian |

The remainder of each draw's digest is unused in this algorithm version. An unrelated stream cannot advance this stream. The stream fingerprint verifies restored counter ownership; it is not authentication or a replacement for the original key fields. All seeds and text key data are copied at construction.

`EntityId` serializes as 16 big-endian bytes or exactly 32 lowercase hexadecimal characters without punctuation or whitespace. Its comparison is unsigned high word followed by unsigned low word, equivalent to lexicographic canonical-byte order. Zero and all other bit patterns are representable. A registry must reject a detected collision before inserting an identity; this primitive cannot prove global uniqueness. Definition IDs, ancestry and branch incarnation remain separate owner-level identities. Never persist runtime `GetHashCode()` results or use them to order authoritative operations.

## Random bounds and continuation

For a positive exclusive bound `b`, the rejection threshold is `2^64 mod b`. Draw until a word is at least that threshold, then return `word mod b`. This removes the incomplete low residue cycle. The persisted counter counts hash draws, including rejected candidates, rather than calls or accepted outcomes. A bound of one still consumes one draw and returns zero. Invalid bounds reject before changing state.

Counter state contains algorithm version, full stream fingerprint, next counter and an exhaustion flag. Counter `UInt64.MaxValue` can be drawn once; it is then retained with `Exhausted=true`. Exhaustion never wraps. A bounded request stages all candidate counter updates: if no accepted value can be obtained before exhaustion, it throws and leaves the stream unchanged. A valid explicit restore can rewind a stream; restoring saved authority revisions is the caller's responsibility. These are deterministic gameplay streams, not security tokens or a source of secrets.

## Time and arithmetic

`WorldInstant` stores signed 64-bit days and milliseconds within a day in `[0, 86400000)`. Stored constructors reject noncanonical milliseconds; `AddMilliseconds` normalizes with floor division even across negative days. Its Int128 intermediate covers every representable day plus any Int64 millisecond delta. An unrepresentable resulting day throws. Differences use signed Int128 milliseconds and remain exact across the entire day range. The explicit JSON constructor preserves and validates these two fields when using System.Text.Json; a future save envelope must still validate required fields, versions and ownership. Instants do not themselves own branch calendars, biological exposure, wall time or tactical clocks.

`RuleMath.ApplyBasisPoints(value, multiplier)` multiplies two signed Int64 values in checked Int128, divides by 10,000 toward zero, and rejects a result outside Int64. Rule-defined modifier ordering and caps belong to the eventual rule owner. `DivideDurationCeiling` accepts a nonnegative duration and positive divisor and computes the exact ceiling without an overflowing addition.

## Independent fixtures and evidence

The test fixture `reference-v1.json` was produced by the independent Python standard-library implementation in `generate_reference_vectors.py`. Its full framed payloads are retained alongside expected identity hashes, first words, a rejecting bounded draw, a stream fingerprint, the final counter draw and a rejecting final-counter case. C# tests consume those constants; they never generate expected outputs from production code. Additional tests cover field-boundary separation, invalid text, big-endian identity ordering, eon-scale arithmetic against independent .NET BigInteger calculations, signed rounding, stream isolation, serialization/restore continuation, limits and overflow.

Test dependencies are explicitly pinned to xUnit.net v3 3.2.2, Visual Studio adapter 3.1.5 and Microsoft.NET.Test.Sdk 18.0.1, with transitive packages locked. These are qualification pins, not an assertion that they are the newest releases. Package availability and net8 compatibility were checked against the [xUnit package](https://www.nuget.org/packages/xunit.v3/3.2.2), [adapter package](https://www.nuget.org/packages/xunit.runner.visualstudio/3.1.5), [test SDK package](https://www.nuget.org/packages/Microsoft.NET.Test.Sdk/18.0.1) and [official xUnit runner guidance](https://xunit.net/docs/getting-started/v3/getting-started).

## Domain extensions

`Authority` now admits finite counted sources, current actor controls and storage policies, and produces pure transfer decisions. It does not authenticate deliveries or persist results. `Terrain` provides pinned, deterministic three-dimensional substrate, canonical excavation history and standing/support queries; it does not mint mined items or own global region claims. Their contracts and limits are retained in the [authority contract](../../../implementation/p01/authority-contracts/CONTRACT.md) and [terrain contract](../../../implementation/p01/terrain-foundation/CONTRACT.md). `OpenFantasy.Hosting` composes the authority with one real durable counted command stream; it is deliberately outside this dependency-free library.
