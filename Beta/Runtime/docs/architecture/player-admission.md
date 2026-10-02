# Player connection admission

## Trust and compatibility

[PlayerAdmissionService](../../src/OpenFantasy.Hosting/Networking/PlayerAdmissionService.cs) joins
one protected message transport to one existing [player port](hosted-player-connections.md). It owns
transient connection credentials and admission slots; it owns no world, clock, actor creation, control
assignment or account database. The host operator supplies already admitted package manifests and
securely provisions credentials to the intended players. Resolution checks namespace/version/dependency
consistency; it does not validate payload semantics or prove that the operator selected the right files.

[PlayerWorldCompatibility](../../src/OpenFantasy.Hosting/Networking/PlayerWorldCompatibility.cs)
binds world ID, branch ID, saved profile version, admission version and the complete resolved package
set. The host derives the first four from its actual immutable root. The package fingerprint hashes
`OFPP`, version bytes `00 01`, a big-endian two-byte count and each 32-byte manifest version in deterministic
dependency-first order. A manifest version binds its namespace, payload and dependencies. The fingerprint
is public compatibility metadata, never a private world-state hash or proof of player authority.

The privileged `Issue` method verifies an already admitted human actor and its current principal/session
control. It creates a random 256-bit bearer credential and stores only its SHA-256 digest in a bounded
registry. A Hello names no actor or principal; lookup selects the host's original binding. Knowledge
of an actor ID, serialized command, or `TrustedCommandDelivery` constructor grants no connection access.
The host rechecks current control and compatibility under the same serialization gate used for commits
and player attachment. It never steals the original local port. A dedicated host explicitly closes that
port before admitting its actor remotely, or reserves it for an actual local player.

The issuer may attach a `LocalRegionObserver` for that same actor. The service checks its identity
before issuing the credential and passes it to the admitted port. A client cannot select another
observer through Hello or gameplay input. The resulting regional geometry belongs to each published
view's own root and crosses the [observation value boundary](player-observation-views.md) explicitly.

## Secret lifetime and bounded work

The registry holds at most four actor credentials. Each has an explicit positive lifetime of at most
24 hours, measured by monotonic host time. Reissue invalidates the inactive actor's earlier credential;
an active credential cannot be replaced until its channel closes. Revocation refuses future admission
without secretly kicking an existing player or changing saved control. Tokens expire for new joins;
expiry does not impose a timeout on an already connected human's Wait. The registry is transient, so
host restart requires new provisioning. Rotation, expiry and revocation are separate from world identity.

[PlayerJoinCredential](../../src/OpenFantasy.Hosting/Networking/PlayerJoinCredential.cs) exports its
secret only through an explicit copy; ordinary string representation is redacted. Credentials never
belong in URLs, logs, Welcome messages or public projections. Remote transport uses TLS with server
certificate validation. A transport object does not prove that protection; the listener and client own
it. Local in-memory transport relies on the trusted in-process boundary.

At most four handshakes may be pending in a service. One absolute ten-second deadline covers initial
silence, Hello, port admission and Welcome; progress does not reset it. Successful awaits also check
elapsed time. WebSocket fragments retain the separate [transport work/deadline limits](player-message-transport.md).
Reservations prevent concurrent use of a credential. Admission rechecks actual host/port availability
after Welcome so a host stopped during output backpressure cannot report a successful admission.
Failure closes the supplied transport, closes any
new port and releases the reservation. Neither refusal nor Welcome is a durable gameplay receipt.

## Wire contract and channel ownership

[PlayerHandshakeCodec](../../src/OpenFantasy.Hosting/Networking/PlayerHandshakeCodec.cs) uses the
versioned player envelope with a nonzero request ID. Hello contains exactly 130 bytes: 16-byte world,
16-byte branch, two-byte profile, 32-byte admission digest, 32-byte package digest and 32-byte credential.
Welcome echoes compatibility and provides the server-selected actor, principal and session IDs, for
146 payload bytes. IDs and integers use the existing big-endian representation. Reading Welcome alone
does not authenticate the server; its protected transport and request correlation are client obligations.

[AuthenticatedPlayerChannel](../../src/OpenFantasy.Hosting/Networking/AuthenticatedPlayerChannel.cs)
owns the admitted port and transport. The port starts unready until the client applies a playable view.
Disposal is idempotent, cancels transport work, closes that port and releases its credential reservation.
Player cleanup still runs if transport disposal fails; a close error cannot strand an occupied actor port.
It leaves other players and the world owner alive. Closing this channel invokes in-process port closure;
it is not the full design's journaled disconnect, reconnect allowance or controller handoff.

[Player command routing](player-command-routing.md) consumes the admitted channel for readiness,
movement and exact action/receipt exchange. The [HTTP/TLS listener](player-http-host.md) and native view adapter have
separate responsibilities, preserving bounded output and participant-safe projections.
[Observation streams](player-observation-streams.md) encode regional data explicitly and use visible
connection-local revisions without exposing hidden world counters as synchronization shortcuts.
