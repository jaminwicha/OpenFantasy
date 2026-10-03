# Player protocol client

[PlayerProtocolClient](../../src/OpenFantasy.Hosting/Networking/Client/PlayerProtocolClient.cs)
owns one authenticated participant connection, its ordered sender, reply correlation and immutable
[observation replica](player-observation-streams.md). It uses the same bounded message transport for
local channels and WebSockets. It owns neither a world authority nor a simulation clock. Its cached
`LocalWorldView` contains participant-visible state; it is not a mutable world or a prediction.

The delegated-control profile explicitly negotiates the [selected character protocol](selected-player-protocol.md)
with atomic home/controlled observations and delivered Resume proofs. Its input names an exact selected
actor, generation and witness; the ordinary view remains home-bound. The replica and legacy control
marker rules below describe connections using the original home observation stream.

## Connection and ownership

`ConnectAsync` takes ownership of an already-protected transport, including failed admission. It
sends Hello with the invitation's compatibility pins and secret, requires the matching Welcome for
request 1, and checks the intended actor and all compatibility fields. The host-issued delivery binding
comes from that validated Welcome. Startup subscribes to observations and requires the first complete
valid view within ten seconds. The returned player remains unready.

Controller-capable profiles also require their coherent control marker within that same startup bound.
The client validates its actor, port and applied observation stream/revision before publishing
[PlayerControlStatus](../../src/OpenFantasy.Hosting/Simulation/PlayerControlStatus.cs). Pending and newly
restored markers clear remembered readiness and movement. Neither marker automatically sends true.
`Resume(observed)` requires the captured restored proof and an already-applied view at or beyond its
minimum revision. The host's Available marker confirms acceptance; later ordinary pause/focus input
keeps that bound proof rather than reverting to legacy readiness. The marker is transient port state,
not part of canonical world or observation-body bytes.

[FarmGame.ControlReturn](../../client/scripts/FarmGame.ControlReturn.cs) forces local pause at a new
control boundary, clears input drafts and suppresses directions/actions held through that boundary.
It explains pending accepted work and enables deliberate Resume only when control is restored. Focus
return does not acknowledge it. Each presentation pass captures control before its view and carries
that same value to the controls; a newer permission marker cannot be paired with an older displayed
body. Resume consumes the presented value, while the protocol independently validates its applicability.

[PlayerWebSocketConnector](../../src/OpenFantasy.Hosting/Networking/Client/PlayerWebSocketConnector.cs)
creates the production socket with platform certificate-chain and hostname validation and requires
the `openfantasy.v1` subprotocol. It applies one ten-second deadline across TLS connection, admission
and first view. It installs no permissive certificate callback and requests no compression. A trusted
invitation does not authorize bypassing TLS validation. Tests may supply separately protected transports;
that transport injection is not a production trust override.

The startup token owns the connection attempt only. Canceling it after successful return does not
disconnect the player. The returned client has its own lifetime. `Completion` exposes terminal peer,
format, capacity and deadline failures. Explicit disposal cancels I/O, releases pending callers and
joins sender and receiver; it does not rethrow an already exposed terminal failure. Neither disposal
nor socket loss records a controller transfer or cancels an already committed effect.

## Input and pending work

One lock allocates strictly increasing gameplay request IDs and publishes ordered output. At most 32
messages wait behind the active send; at most 128 inputs are queued per monotonic one-second window.
Capacity exhaustion closes the connection instead of discarding a stop or accumulating waiters. Every
send has a five-second absolute deadline, checked again after successful completion.

Unchanged readiness is suppressed. Movement accepts the protocol's cardinal 32-unit samples or stop.
Repeated held input is limited to one fresh sample per 50 ms; changes and stop are queued immediately.
The client never renews movement without another caller sample. Readiness and movement do not create
durable receipts or establish that the world advanced. A Pong confirms processing of preceding control
messages, not completion of an outstanding world action.

The client keeps one action, one query and one Ping independently pending. An exact repeated canonical
command shares the existing action task and sends no duplicate envelope. Another simultaneous action,
query or Ping receives transient Busy feedback for its occupied slot. World actions retain the caller's
command identity, revisions and intent. There is no automatic retry, reconnect, revision repair or
synthetic receipt. Pending human/world decisions have no gameplay timeout.

## Reply validation and recovery

[Reply routing](../../src/OpenFantasy.Hosting/Networking/Client/PlayerProtocolClient.Receive.cs)
requires a matching pending request and reply kind. Receipts additionally match command identity,
principal, world, branch and the hash of the exact submitted canonical bytes. Query results validate
their kind, subject and authenticated actor/world scope against the original request. A transient
`PlayerRequestRefusedException` remains distinct from a committed rejection receipt. Unknown, duplicate
or mismatched responses terminate the connection.

Unsolicited observations use request ID zero. The replica validates before publishing; malformed input
cannot replace its last good view. A missing delta basis queues one full-recovery request, coalescing
further requests until a valid full frame arrives. Initial subscription is distinct from recovery so a
missing initial basis can still request a complete frame. Reading the cache does not imply a new server
barrier, global readiness, or any access to private world state. Application presentation and host run
status require explicit contracts above this protocol owner.

## Application session and native binding

[IPlayerSession](../../src/OpenFantasy.Hosting/Simulation/IPlayerSession.cs) exposes participant input,
views, run status and permitted queries to the game. [OwnedLocalPlayerSession](../../src/OpenFantasy.Hosting/Simulation/OwnedLocalPlayerSession.cs)
preserves the solo composition's host ownership. [RemotePlayerSession](../../src/OpenFantasy.Hosting/Networking/Client/RemotePlayerSession.cs)
owns only its connection. Its additional ten-second startup obtains the supported presentation binding
and first participant status while the connection remains unready. Startup failure disposes the connection.

The remote adapter caches immutable status for synchronous frame reads. Every 250 ms it may issue one
status refresh; at most one foreground query waits behind an active refresh. A pending foreground request
takes priority over starting another refresh, while another foreground request receives Busy. Submit and
cancel replies immediately replace cached inquiry; subsequent status refreshes carry merchant outcomes.
An admitted history read can delay status, but never blocks the render thread. Shutdown cancels refresh,
releases pending callers and joins both refresh and foreground work before disposing their coordination.
The foreground owner tracks the exact typed task returned to the caller, including result projection;
completion of a lower protocol task does not establish completion of that caller task. Foreground
admission and refresh priority use the tracked task's completion under the session gate. Invalid,
closed and busy query calls return faulted tasks while preserving the asynchronous interface contract.

[FarmClientSession](../../client/scripts/FarmClientSession.cs) builds the same immutable offers for either
adapter, retaining per-session offer registration and exact command retry identity. Wheel opening queues
stop and captures the latest published view; it adds no remote barrier or promise that terms cannot become
stale. Saved-history verification and route advice remain local privileged facilities, outside IPlayerSession.

Receipt delivery and observation publication are independent. One player's accepted choice does not
mean another player's cached view already contains it. A displayed combat offer retains its captured
timing revision; another participant's commit can make it stale, and the authority rejects that action
without silently replacing its terms. A coordinated native test waits for the specific prior choice or
phase to be observed before preparing its next deliberate input; a frame, Pong or fixed delay alone is
not an observation barrier.

[NativePlayerEntry](../../client/scripts/Networking/NativePlayerEntry.cs) opens the explicit `--join`
invitation through platform TLS validation. [FarmGame](../../client/scripts/FarmGame.cs) enables readiness
only after first view and binding. Ordinary window focus, input cancellation, wheels and inventory use the
same controls. Remote pause labels describe participant input and warn that other players may continue;
they never promise frozen prices or deadlines. The disposable loopback probe's exact test-certificate pin
does not alter production trust, accepts no arbitrary host and retains certificate-name checks.

The explicit disposable roster probe coordinates independent native processes through bounded local
mailboxes. Each process owns one authenticated player connection. Mailbox instructions inject ordinary
pointer/keyboard/controller events and capture that player's immutable view and exact submitted command/receipt;
they cannot write world state, grant resources or choose for another actor. Source-based route planning
belongs to the test coordinator and does not expose privileged route advice through the player session.
Background capture sets no-focus before window creation in a disposable project configuration and
records any native focus acquisition. This fixture uses controlled input/focus; normal `--join` keeps
the production window-focus and readiness policy.

Selected-character probe actions use the same visible consent controls and action wheel as play.
Readonly selection/board observations and semantic control metadata identify the displayed subject;
they provide no gameplay mutation interface. Before confirming a captured action, the probe compares
its selection, operated actor revision and relevant encounter pins with both the live observation and
the presented frame. Consent commands and their returned receipt tasks are observed passively at the
ordinary panel submission boundary, then checked against durable history by the coordinator.
