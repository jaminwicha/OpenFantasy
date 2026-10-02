# Authenticated client protocol owner

The next dependency is a reusable pure-C# client for the actual retained server protocol. It must
validate Welcome against the supplied invitation, await the first complete view while remaining
unready, maintain one replica and correlate exact durable receipts and participant queries. Local
in-memory transport and real TLS carry the same messages. A caller cancellation during startup does
not own an established session. Default production TLS validates both certificate chain and hostname;
no callback accepting arbitrary certificates enters the connector.

One bounded ordered sender owns request identity allocation and output. Deduplicate unchanged readiness;
limit repeated held movement to50ms renewals without autonomous movement renewal. Changes/stop remain
ordered immediately with discrete actions. Output saturation terminates the connection, preventing an
unbounded waiter queue or silently dropped stop. One action, one query and one Ping have independent
slots. Pending exact command bytes share their task; no automatic wire retry, action replacement,
reconnect or synthetic receipt is allowed. Waiting on a human/world decision has no network timeout.

Every reply must match a known slot and its kind, scope and exact command identity/hash where relevant.
Unknown or malformed replies terminate the connection while retaining the last valid replica. A missing
delta basis requests bounded full recovery. Teardown cancels transport work, joins loops and terminates
all pending caller tasks; the host/authority is owned separately. New tests exercise real host receipts,
paused admission, backpressure, history work, spoofed welcomes and structurally valid substituted replies.

Native integration is the next composition, not a claim made by this protocol owner. Read-only client
mapping identified three explicit gaps: authoritative run status, content-bound presentation bootstrap
(companion/item IDs, cargo stall appearances), and bounded refresh of a Waiting trade inquiry. Current
public views do not provide all these. Do not guess global RunState from own readiness or reconstruct a
server genesis to manufacture presentation metadata. FarmClientSession retains immutable offer issuance
and exact retry identity beneath a narrow session abstraction. Probe-only route advice and full history
replay remain privileged local facilities; a network client never owns the server SQLite file.

This coherent client dependency precedes native1–4-player acceptance, solo envelope parity, recorded
disconnect/controller handoff and desktop production qualification. Full design/requirements remain.
