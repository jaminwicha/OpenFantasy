# Player networking boundary

Status: implementation in progress. The existing shared-player milestone remains the accepted game
binding. Message transport alone is not authenticated multiplayer, native client delivery or release
acceptance. Preserve the complete P02/P03 and production requirements.

## Architecture and order

The [TDD adapter contract](../../../spec-v1_1/TECHNICAL_DESIGN.md#10-adapters-invariants-and-evidence-gates)
requires ordered WebSocket messages, remote TLS, protocol/package compatibility, authenticated actor
control, the same envelopes over local transport, bounded committed observations and exact retries.
The shared host supplies the only clock, writer and autonomous controllers. Network code must not
reimplement domain rules or serialize a complete private WorldSnapshot for clients.

1. Implement immutable versioned envelopes and bounded local/WebSocket complete-message adapters.
   Prove independent bytes, fragmentation, caps, queue pressure, duplex ownership and closure using
   actual loopback TCP streams. These wire tests do not exercise HTTP Upgrade or TLS.
2. Bind admission to explicit server-issued credentials for an already admitted actor/control binding,
   exact world/branch, protocol, saved profile/admission version and resolved package pins. A client
   cannot establish TrustedCommandDelivery by submitting principal/session IDs. Never log credentials
   or place them in query strings. Bound outstanding handshakes and request bodies before allocation.
3. Route readiness, leased movement and canonical WorldActionCodec commands to actual player ports.
   Serialize original WorldReceiptCodec receipts only after completion. Exact retries preserve original
   command bytes and durable outcomes. Admission/transport errors remain distinct from gameplay receipts.
   Keep the receiver responsive while an action awaits its host cutoff; otherwise a paused action can
   prevent its own readiness message. One outstanding action plus bounded response storage prevents
   duplicate requests from creating unbounded continuation tasks or memory.
4. Publish only participant-safe observations at no more than 10 Hz, with full recovery and revisioned
   deltas. A slow receiver must not hold the host's serialization gate or world clock. Projection
   revisions describe permitted observations, not private global commit activity. Never expose a root
   hash/private inventory just to simplify reconciliation. Regional presentation data needs its own
   explicit encoding; LocalWorldView currently excludes it from diagnostic JSON.
5. Add the HTTP/WebSocket listener to the headless executable. Its existing .NET 10 toolchain includes
   Microsoft.AspNetCore.App 10.0.12. Keep ASP.NET Core out of the net8 Hosting assembly and Godot export.
   Default listeners bind loopback; remote sessions require TLS without certificate-validation bypasses.
   Native windows then consume these same semantic envelopes, including solo local transport.
6. Implement the specified recorded disconnect policy: confirmed transport loss, journaled external
   event, 30-second host-wall-time reconnect allowance, permitted co-player/conservative-controller
   handoff and safe control return. A connected idle human keeps unlimited Wait. Socket disposal alone
   neither journals this decision nor grants an NPC turn. This is a distinct authority change requiring
   its own preserved checkpoint, rule/profile decision and recovery qualification.

## Implemented transport boundary

PlayerMessageCodec version 1 uses OFWP magic, big-endian version/kind/request ID/length, an exact
20-byte header and at most 1 MiB payload. Unknown versions/kinds, inconsistent lengths and trailing
bytes are refused. The request ID correlates transport work; it is not a world CommandId or receipt.
Commands retain their smaller canonical 16 KiB limit and semantic validation at their own decoder.

PlayerMessageTransport permits one active send and one active receive. It refuses competing operations
rather than creating an unbounded task queue. It captures outgoing bytes, limits complete messages and
cancels pending I/O on disposal. Disposal aborts the connection; it does not promise graceful message
flush or a world disconnect decision. The connection owner must finish/cancel its loops explicitly.
InMemoryPlayerTransport uses two ordered queues, each bounded to eight messages with backpressure.
WebSocketPlayerTransport reassembles binary fragments within the same cap, rejects text/oversize and
truncated message closure, and aborts after malformed input so no suffix becomes a second message.
It additionally bounds reassembly to 1,024 receive parts and five seconds after the first part,
checks successful reads against that absolute deadline, and makes socket cancellation terminal.
Wrapping an open socket establishes no authentication or TLS proof.

## Verification and preservation

The pre-change source is 6e8baec and the exact ref is in [CHECKPOINT.json](CHECKPOINT.json).
Protected existing tests/specs and unrelated predecessor staging are pinned in PRESERVATION.json.
New transport tests cover wire semantics only; shared host, authority, native, TLS, authentication,
disconnect and end-to-end multiplayer claims require their own affected evidence. The initial build
failed because new async tests omitted explicit xUnit cancellation tokens; the tests now pass the
runner token rather than disabling the analyzer. Preserve the original build log.

No Claude call is pending: its recorded spending-limit failure still holds. Astra implements directly.
Use one bounded native critic for the framing/admission boundary and integrate substantiated findings.

## API research

Microsoft's [WebSocket hosting documentation](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/websockets?view=aspnetcore-10.0)
confirms that the upgraded request pipeline must remain alive through socket use, and discusses
explicit keep-alive failure detection. Origin/CORS handling is not authentication. Compression stays
disabled for credential-bearing connections. These API constraints inform the listener; they do not
prove game compatibility, security or production capacity.
