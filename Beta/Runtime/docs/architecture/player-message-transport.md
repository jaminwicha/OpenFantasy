# Player message transport

## Envelope and ownership

[PlayerMessage](../../src/OpenFantasy.Hosting/Networking/PlayerMessage.cs) is an immutable transport
envelope. Its request ID correlates message exchanges; it is not a world command ID, permission or
durable receipt. [PlayerMessageCodec](../../src/OpenFantasy.Hosting/Networking/PlayerMessageCodec.cs)
encodes version 1 with `OFWP` magic and a 20-byte header: four magic bytes, two-byte version, two-byte
kind, eight-byte request ID and four-byte payload length. Integers are big-endian. The maximum payload
is 1 MiB. Unknown versions/kinds, truncation and trailing bytes are refused. Semantic payload codecs
enforce their own smaller bounds and authority rules. Envelope validity authenticates nobody.

These types are standalone I/O components. [Player admission](player-admission.md) binds a compatible credential-bearing handshake to one
existing world port. Transport alone does not encode a complete client view, authenticate a player or
establish TLS; the connection owner routes admitted work through the shared host.

## Bounded full-duplex I/O

[PlayerMessageTransport](../../src/OpenFantasy.Hosting/Networking/PlayerMessageTransport.cs) permits
one active send and one active receive concurrently. Another operation in the same direction fails
instead of accumulating waiters. Outgoing bytes are copied before asynchronous delivery. The owner
must bound its own producers and finish or cancel its loops. Disposing the transport cancels pending
I/O and aborts; it does not promise a graceful flush or a journaled controller-disconnect decision.

[InMemoryPlayerTransport](../../src/OpenFantasy.Hosting/Networking/InMemoryPlayerTransport.cs) uses
two ordered channels with eight messages per direction. A full queue exerts backpressure. Cancellation
releases an operation's slot; closure completes the queues. This local adapter shares framing with
the socket adapter without creating a separate authority or process bridge.

[WebSocketPlayerTransport](../../src/OpenFantasy.Hosting/Networking/WebSocketPlayerTransport.cs)
wraps an already-open socket. It accepts binary messages only, reassembles within the byte limit,
allows at most 1,024 receive parts and imposes an absolute five-second deadline after the first part.
Successful reads also check elapsed time. Zero-length continuation fragments consume the part budget;
they cannot renew the deadline. The connection owner separately bounds the initial handshake wait.
Text, oversized messages and closure inside a fragmented message abort the transport. Socket-operation
cancellation is terminal because a discarded prefix cannot safely resume as a new message. This differs
from reusable cancellation of a local channel read.

HTTP Upgrade, certificate validation, credentials, protocol/content compatibility and disconnect policy
belong to connection admission and hosting. A WebSocket object alone proves none of them. Remote
sessions require TLS; compression is disabled for credential-bearing messages. The HTTP request must
remain alive for the entire upgraded socket lifetime. Clients receive only permitted projections,
never privileged world roots or hashes as a shortcut to observation synchronization.

The [player protocol client](player-client.md) supplies participant-side startup, bounded output,
reply correlation and replica ownership. Its production connector validates TLS before credentials
enter the transport; an already-open socket supplied directly retains the caller's protection obligation.
