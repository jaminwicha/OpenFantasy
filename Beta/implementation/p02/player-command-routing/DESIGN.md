# Player command routing

This increment binds authenticated channels to existing local player readiness, fixed cardinal movement
and canonical world actions. It preserves the sole host scheduler/authority and original receipt codecs.
It does not yet encode observations, host HTTP/TLS or route the Godot client, and it does not implement
the required journaled disconnect/control policy. Those full-network dependencies remain mandatory.

A connection has one receive loop, one ordered sender, at most one pending action and eight queued
responses. Action completion does not block incoming readiness/movement. A response producer refuses
an exhausted queue instead of blocking a host commit or creating waiting tasks. Each send has a
five-second absolute deadline, including a successful-completion elapsed check. Input is limited to
128 messages in a one-second monotonic connection window; controls normally sample at host cadence.
These bounds govern transport resources, not world time or a human's tactical Wait.

All input request IDs are nonzero and strictly increasing within the gameplay connection. Command IDs
remain independent and retained across reconnects/retries. A pending duplicate gets transient Busy;
retry after commitment retrieves the original receipt. The adapter must not normalize noncanonical
command bytes, generate another command identity or turn a transient preflight refusal into a receipt.
Actual durable rejection and application receipts retain their original canonical bytes and identity.
Control inputs have no synthetic durable acknowledgment; a Pong only establishes that preceding control
messages were processed, not that a pending action committed or moved the actor.

Closing any loop closes its own port/transport, cancels uncommitted waiters and joins workers. Host
completion also terminates idle connections. Durable effects survive output loss and can be recovered
by exact command retry after a new authenticated connection or host reopening. Network disposal still
is not the saved 30-second disconnect/reconnect/handoff rule required by the full design.

The remaining client surface also includes bounded authenticated trade inquiries and quote/proposal
inspection; those local APIs are not automatically covered by the world Command envelope. Preserve
their separate cancellation/visibility semantics when adding messages. Projection transport must cover
actual regional views and those participant-specific results before native clients can replace their
current local API usage. No existing gameplay interaction is removed by this incremental wire surface.
