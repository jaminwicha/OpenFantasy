# Player gameplay message routing

[PlayerCommandSession](../../src/OpenFantasy.Hosting/Networking/PlayerCommandSession.cs) owns the
lifetime of one [authenticated player channel](player-admission.md). It routes readiness, fixed cardinal
movement, discrete world actions and [typed player queries](player-queries.md) through that channel's existing player port. It creates no clock,
world writer, NPC policy or gameplay permission. The host's ordinary selection, costs, holds, inventory
rules and committed receipts remain authoritative for both local and network callers.

## Inputs and exact outcomes

Gameplay request IDs are nonzero and strictly increasing within that gameplay stream. The handshake
has its separate request correlation; the first gameplay input starts a fresh sequence. Transport request
IDs correlate replies only. A world command ID and its canonical bytes survive lost replies, reconnects
and host reopening. Repeating an old transport request ID is a protocol error, not an idempotency mechanism.

[PlayerGameplayCodec](../../src/OpenFantasy.Hosting/Networking/PlayerGameplayCodec.cs) defines one-byte
readiness (`0` or `1`) and two big-endian signed 32-bit movement axes. Movement selects one cardinal
32-subunit sample or zero; the host's existing 150 ms lease and readiness policy determine actual use.
Receiving a sample does not prove movement committed. Readiness and movement carry no synthetic durable
acknowledgment. An empty Ping receives an empty Pong after preceding control messages were processed;
it does not imply that a pending action committed or the world advanced.

Commands carry the original `WorldActionCodec` payload with its 16 KiB bound. Decode must reproduce
exactly those canonical bytes on encode, preventing silent reinterpretation of a retry. The admitted
port authenticates submitted actor/principal/session/scope claims and routes lawful requests to the
existing authority. The adapter does not repair an invalid revision, invent a new command ID or recapture
a selected action's terms. A `Receipt` carries the exact `WorldReceiptCodecV1` bytes obtained after
commit. Its application or rejection status is a real durable result, retrievable by exact command retry.

A `Refusal` has one byte: `Busy` or `NotAdmitted`. It reports connection-local/preflight state without
serializing internal exception text. It has no commit sequence or durable side effects and cannot stand
in for a gameplay rejection. One outstanding action includes its asynchronous host wait. A duplicate
while it is pending gets Busy rather than adding receipt waiters; a retry after commit returns the
original durable result. The client retains its exact command whenever delivery remains uncertain.
The [protocol client](player-client.md) coalesces identical locally pending submissions into one task
and one envelope; the server's Busy response remains the boundary for actual duplicate wire requests.

## Scheduling and resource ownership

The receiver continues while an action or explicit query awaits the host. Each has one independent
in-flight slot. A player can therefore pause or resume readiness
without deadlocking behind that action's receipt. Host admission and cutoff policy still decide whether
an action was selected before a readiness change. No render or network timing grants an extra world step.

One sender drains at most eight queued responses. Response producers never wait for queue capacity;
exhaustion closes that connection. Each send has a five-second absolute deadline and checks elapsed
time after success. Its specific `PlayerResponseTimeoutException` identifies a connection-local
deadline failure without hiding unrelated backend timeouts. Up to 128 inputs are processed per one-second monotonic connection window. These
bounds limit transient work and memory; they are not simulated time or a timeout on an idle human's
Wait. A slow or flooding receiver cannot hold the host's serialization gate through socket output.

[PlayerCommandInput](../../src/OpenFantasy.Hosting/Networking/PlayerCommandInput.cs) decodes and
validates the complete request before any host callback. This separates malformed client bytes from
backend failures that happen to use the same exception type. Established EOF/Goodbye means peer
closure; invalid client framing, payload or rate means protocol failure; transport I/O, reassembly or
response deadlines and output saturation mean transport loss. The adapter records abort origin before
canceling sibling I/O, so whichever loop finishes first observes the same cause.

Goodbye, orderly peer closure, transport failure, cancellation and host completion terminate the session.
Termination closes its channel and port, cancels uncommitted action waiters and joins receiver, sender,
observation publisher, action and query work. A committed effect remains saved even when its response is lost. Other players and
host-owned autonomous work retain their own lifetimes. The command session runs once per owned channel.
Its two-token entrypoint distinguishes server shutdown from an established HTTP request abort. The
single-token entrypoint retains caller-shutdown semantics. Local channel/transport disposal and host
failure do not become confirmed peer loss. Only the host can journal an eligible controller transition;
ordinary teardown never grants actions or advances the world.

## Other adapter responsibilities

An empty observation input subscribes to [bounded view publication](player-observation-streams.md)
and requests full recovery. Its asynchronous output uses a separate canonical typed view and visible
revision, sharing the bounded sender without blocking input processing. The channel does not itself
make a Godot scene into a network client. [HTTP Upgrade/TLS and provisioning](player-http-host.md) bind around these components; native clients
and solo envelope parity have separate adapter contracts. The [controller binding](controller-lifecycle.md) journals confirmed loss for its explicit profile and
measures allowance independently of simulation time. Survival selection, restart reconciliation and
client return-to-control presentation remain separate contracts; a socket error cannot grant a human
decision or bypass the authority.
