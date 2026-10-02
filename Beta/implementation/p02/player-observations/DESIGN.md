# Player observations and replica

The existing LocalWorldView is the client presentation/action contract, but default diagnostic JSON
intentionally excludes regional geometry and private preparation captures; one command hash also needs
an explicit wire conversion. Add a versioned bounded typed codec with value-only reconstruction seams,
keeping old diagnostic bytes and full gameplay/action-preparation parity. Never reconstruct a private
WorldSnapshot on the client. Host projection retains its declared sight-complete profile; this work
does not claim general fog-of-war or silently filter required fields.

A connection-local observation stream compares exact permitted projection bytes, not the host's private
root hash/revision. Its random stream ID is cosmetic transport identity. A changed public view increments
its observation revision; private changes with identical projections emit nothing. Frames are full
snapshots or a bounded prefix/suffix replacement delta against the previous emitted projection. A full
recovery request uses the current revision when content is unchanged. The client only publishes a
completely reconstructed, validated typed view matching its expected world, branch and player.

Observation requests explicitly subscribe a gameplay channel, preserving the prior request-only wire
surface. Generate/enqueue at most 10 Hz (not a guarantee of equally spaced delayed socket delivery),
reserve at most one queued/in-flight observation, keep control/receipt
processing independent, and coalesce repeated recovery requests. A missing delta basis keeps the last
coherent client view and requests a full frame. Reordered/stale/conflicting frames are refused; no
private commit counter is used for synchronization. Local and socket clients use identical codecs.

Regional observers are provided per actor by the trusted host issuer and checked against that actor.
The frame carries IsRegional even when the observer currently has no supported floor; unsupported does
not mean nonregional. All constituent fields derive from the same already published LocalWorldView.

Remaining full scope: authenticated trade inquiry and historical quote/proposal inspection replies,
HTTP Upgrade/TLS, actual Godot client session boundary and scene metadata, solo envelope parity,
native 1–4-player proofs, recorded disconnect grace/controller handoff and all prior campaign gates.

Capacity findings are retained in CRITIC_REVIEW.md. Occupancy uses lossless packed bits. Large canonical
JSON bodies use a bounded lossless wrapper, preserving the 900 KiB encoded message-body limit and all
fields; expanded JSON has a separate explicit 16 MiB budget. Decoders validate declared length before
allocation, exact decompression consumption/length, and canonical expanded values. This prevents the
known admitted 4,096-lot world from becoming an accidental gameplay restriction. It does not qualify
every combined maximum facet, host allocation/latency or production scale; those gates remain open.
