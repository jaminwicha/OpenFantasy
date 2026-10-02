# Player observation streams

## Ownership and publication

[PlayerCommandSession](../../src/OpenFantasy.Hosting/Networking/PlayerCommandSession.cs) publishes
the authenticated channel's immutable `LocalWorldView`. That root already contains one player's
inventory, participant offers and the host-selected regional projection. Observation encoding runs
outside the host commit path. It grants no control, advances no simulation clock and never sends the
private `WorldSnapshot` or a private world commit counter.

An empty `Observation` input subscribes the connection and requests a full view. It follows the
same nonzero, strictly increasing request IDs and input-rate bound as other gameplay input. A request
cannot supply a view, observer identity, region selection or revision. The trusted credential issuer
may bind a `LocalRegionObserver` for that same actor; admission passes it into the existing player
port. `IsRegional` distinguishes a nonregional session from a regional observer lacking a supported
standing floor. Both modes use the [typed observation value codec](player-observation-views.md).

Generation and enqueue run at most once per 100 ms of monotonic connection time. This bounds work;
it does not promise equally spaced socket delivery after transport delay. Repeated full requests coalesce.
One reservation covers the queued or in-flight observation until its send completes. While that
reservation is occupied, publication performs no further view encoding. A single bounded response
queue and sender carry observations, receipts and control responses. A slow observation can delay
responses on its own ordered connection, but cannot block host commits, another player's port or the
receiver's processing of readiness and cancellation. The existing five-second send deadline closes
the connection on stalled output. Teardown cancels and joins the publisher along with the other loops.

The publisher captures the port's transient control state beside its view under the input lock.
A control transition forces a full observation through that same bounded publisher, even when the
canonical view bytes are unchanged. Its `ControlStatus` marker follows that full frame on the ordered
sender and names the stream and minimum applied revision. The first restored marker remains stable
while later views advance: Resume proves observation of the safe return, not observation of every
subsequent action. These markers do not alter the canonical view body or durable history. The
[readiness contract](player-command-routing.md) validates them before permitting input.

## Visible history and delta format

[PlayerObservationStream](../../src/OpenFantasy.Hosting/Networking/PlayerObservationStream.cs) owns
one producer's last canonical view bytes and connection-local revision. A reference-identical view
needs no repeated encoding. Different immutable roots are compared by their complete permitted bytes;
identical bytes produce no delta and no revision change. A private world revision alone cannot cause
traffic. The first visible state has revision one. Changed visible bytes increment that revision;
a requested full copy of unchanged bytes keeps its existing revision.

The stream ID is a random 128-bit connection label, not a simulation seed or authentication credential.
[PlayerObservationFrameCodec](../../src/OpenFantasy.Hosting/Networking/PlayerObservationFrameCodec.cs)
defines a 56-byte big-endian header followed by replacement bytes:

| Offset | Field |
| --- | --- |
| 0 | Four-byte `OFVO` magic |
| 4 | Unsigned 16-bit format version |
| 6 | Unsigned 16-bit kind: zero full, one delta |
| 8 | 16-byte stream identity |
| 24 | Unsigned 64-bit visible revision |
| 32 | Unsigned 64-bit base revision; zero denotes full |
| 40 | Signed 32-bit resulting body length |
| 44 | Signed 32-bit retained prefix length |
| 48 | Signed 32-bit retained suffix length |
| 52 | Signed 32-bit replacement length |

A full frame contains the entire canonical view, with zero prefix and suffix. A delta keeps the
longest common prefix and nonoverlapping common suffix of the previous emitted view, replacing the
middle. Its revision is exactly its base revision plus one. The resulting length equals prefix,
replacement and suffix lengths combined. Negative lengths, overflow relationships, revision zero,
oversized output, unsupported formats and trailing bytes are refused. The encoded view body remains
bounded at 900 KiB, within the one-MiB player envelope payload limit. Larger canonical value encodings
use the separately bounded lossless form described by the value codec; deltas operate on the
originating server's encoded bytes. Frames are sent as `Observation` output
with request ID zero; coalesced recovery has no one-reply-per-input correlation.

## Replica and recovery

[PlayerObservationReplica](../../src/OpenFantasy.Hosting/Networking/PlayerObservationReplica.cs)
belongs to one authenticated world, branch and actor. One receiver reconstructs a complete body,
validates its typed canonical values and expected scope, then atomically publishes one immutable
`PlayerObservationSnapshot` containing the view, stream identity and revision. Readers requiring
both view and revision use `ReadSnapshot()` once; separate calls may observe separate publications.
Previous snapshots remain immutable and usable by presentation work.

A delta without the exact current basis returns `RequiresFull` and keeps the last good snapshot.
The caller sends another empty observation request. Full recovery may skip visible revisions, since
it contains an entire current view. A repeated full frame at the current revision must have identical
bytes and returns `Unchanged`. Stale, foreign-stream, conflicting or invalid updates cannot replace
the last good view. Reconnection creates a new replica instead of admitting a new stream into an old
connection. Transport ordering, frame validation and typed value validation are separate boundaries.

## Adapter boundaries

These components carry presentation values and selected-action preparation terms. Authenticated trade
inquiries and historical inspections require their own bounded request/reply contracts. A game client
also owns session metadata, exact uncertain commands, input readiness and recovery requests; the replica
alone is not that client session. HTTP Upgrade/TLS, native Godot session binding, solo envelope parity,
and recorded disconnect grace/controller handoff surround this transport boundary. An observation
stream cannot manufacture those policies or replace the authority's saved history.
