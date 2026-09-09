# Exact history request and commit-boundary contracts

This retained P00 increment implements the exact address, request, prefix-boundary and bounded commit-metadata selection portion of [technical design §9](../../../spec-v1_1/TECHNICAL_DESIGN.md). It does not implement historical reconstruction, generated ancient history, checkpoints, persistence, branching, transit, exposure-clock reconstruction or authorization. The full chronology and arbitrary-destination requirements remain unchanged.

## Immutable request

`HistoryAddress` names the world, requested branch and realm, with signed 64-bit X/Y/Z coordinates measured in 1/256 cell subunits. All signed coordinate values are retained exactly. The initial profile's cell-to-metre mapping is a rule definition, not a second floating-point position in this contract. Realm identity remains independent from elevation. Conversion to chunk/local coordinates belongs to the topology implementation; it must preserve these coordinates and use floor division for negative addresses.

`HistoryBoundary` contains the historical origin, known head instant, requested instant, pinned head commit revision and optional earlier commit cursor. Its constructor requires:

- `historicalOrigin <= requestedInstant <= knownHead`, including exact endpoints.
- `commitCursor <= pinnedHeadRevision` when a cursor is supplied.
- Every instant uses the existing normalized signed-day/millisecond `WorldInstant` value, without rounding to a keyframe, named era or second.

`HistoryRequest` combines these values. The effective sequence boundary is the optional cursor or otherwise the pinned head revision. Revision/cursor zero represents an empty committed prefix; actual `CommandCommitIdentity` values remain positive. An absent cursor and an explicit zero cursor retain different canonical request identities. Neither changes the requested date.

These are caller-supplied metadata. Construction or hashing is not evidence of a committed database head, visible world address, permitted history access or a prepared destination. A later service must obtain and pin the head from the exclusive durable writer and bind this request to its authorized preparation work. Later commits cannot mutate the accepted object.

## Selection and lineage seam

`HistoryCommitStamp` contains a world-scoped `CommandCommitIdentity` and an authoritative instant for a committed transaction. Its sequence names a commit, not an individual event; one transaction may contain multiple ordered events. Selected commits remain in ascending unique commit-sequence order, even when authoritative timestamps are non-monotonic. Two transactions at the same instant are both selected through the effective sequence boundary.

`HistoryRequest.Includes` applies the exact metadata predicate:

```text
stamp.world == request.world
and stamp.sequence > 0
and stamp.sequence <= (request.cursor ?? request.pinnedHeadRevision)
and stamp.authoritativeInstant >= request.historicalOrigin
and stamp.authoritativeInstant <= request.requestedInstant
```

The public predicate does not resolve branch membership. `HistoryCommitSelection` requires an explicitly branch-resolved candidate batch, its branch ID and its pinned head revision. Both scope values must match the request. The storage resolver must eventually provide that branch's inherited prefix plus its own commits. Comparing an inherited commit's original branch to the destination branch would incorrectly exclude ancestry, so the stamp deliberately contains no original-branch filter.

Selection validates the complete supplied batch before filtering. Foreign-world commits, sequences beyond its pinned head, timestamps outside the inclusive historical-origin/known-head window, duplicate sequences (including duplicates that a cursor would filter out), null entries and excessive input all reject. Timestamps above the requested instant but within the known window and sequences above an earlier cursor are valid batch members that are excluded from the result. The historical origin is the start of available chronology, not a checkpoint lower bound that permits older causal commits. No timestamp monotonicity is assumed.

The immutable result copies its input and returns a read-only collection. Realm/X/Y/Z locate the reconstruction request and do **not** filter the causal branch prefix: a remote war, upstream flood or cross-realm action may affect the requested location.

The current 4,096-candidate limit bounds one in-memory selection operation. It is not a save-size, chronology, population or total-history limit. A future storage service must supply ordered pages with completeness, cross-page duplicate detection and continuity checks; independently selecting pages does not prove a complete reconstruction. Likewise, supplying the right branch label is not proof that the resolver supplied the correct ancestry.

This metadata unit assumes the indexed transaction has one authoritative instant. Future event storage must preserve the original order of every event within each selected commit. If an eventual storage format permits one commit to contain events at different authoritative instants, it must qualify an event-level selection/replay contract rather than claim that this stamp alone proves that reconstruction.

## Canonical version 1 interchange

`HistoryRequestCodecV1` reuses the existing canonical framing:

```text
4 bytes magic "OFC\0"
UInt32 big-endian framing version 1
UInt32 big-endian field count 12, including the tag
for each field: UInt32 big-endian byte length, then field bytes
```

Fields occur in exactly this order:

| Field | Encoding |
| --- | --- |
| Contract tag | ASCII `history.request.v1` |
| World, branch, realm | Three separate canonical 16-byte `EntityId` fields |
| X, Y, Z | Three separate signed 64-bit big-endian integer fields |
| Historical origin, known head, requested instant | Three separate 12-byte fields: signed 64-bit days followed by unsigned 32-bit normalized milliseconds |
| Pinned head revision | Unsigned 64-bit big-endian integer |
| Optional cursor | Empty field for absent; otherwise unsigned 64-bit big-endian integer, including explicit zero |

The complete request occupies 194 bytes without a cursor or 202 bytes with a cursor. `ComputeContentHash()` returns SHA-256 over all canonical bytes, binding every address, time and prefix field. Encoding returns fresh bytes and decoding retains no caller buffer.

The fixed-format reader rejects unsupported headers/tags/counts, truncated fields, invalid field lengths, malformed cursor representations, non-normalized instants, impossible windows, cursor overflow beyond the pinned revision, trailing bytes and inputs over 202 bytes. It does not allocate according to untrusted declared lengths. Parse failures are `FormatException`; programmer-facing constructors use argument exceptions.

## Retained validation

`generate_history_vectors.py` is an independent Python `struct`/`hashlib` oracle that imports no application code. Its checked-in `history-v1.json` contains absent/earlier/zero-cursor requests, exact bytes and hashes, signed coordinate/day extremes and the maximum unsigned revision. JSON stores 64-bit values as decimal strings to avoid consumer precision loss.

The C# tests compare exact oracle bytes/hash and round trips; exercise inclusive time endpoints, millisecond boundaries, same-instant commits, stable sequence order with non-monotonic times, retry immutability, wrong branch/world/head scope, duplicate rejection before filtering, bounded enumeration, every truncated request prefix, malformed fields and trailing data. The tests and oracle are retained implementation evidence; the central integration checkpoint records actual executed results. This document alone does not mark any unrun qualification as passed.

Remaining work includes authenticated head acceptance, durable indexed history, branch ancestry, checkpoint/keyframe selection, partial-interval exposure and solver remainders, pinned interpreters/packages, complete reconstruction, preparation progress/backpressure, storage growth qualification and cross-restart/order-independent historical queries.
