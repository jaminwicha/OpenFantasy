# Player HTTP/TLS hosting

[OpenFantasy.Server](../../src/OpenFantasy.Server/OpenFantasy.Server.csproj) binds HTTPS/WebSocket
requests to the existing [admission](player-admission.md), [gameplay routing](player-command-routing.md),
[observations](player-observation-streams.md) and [queries](player-queries.md). It depends on Hosting
and ASP.NET Core; Core, Hosting and the Godot client do not depend on this listener assembly. The
library is an in-process adapter, with no separate world writer, clock or IPC authority. The dedicated
composition uses the headless executable's .NET runtime. Embedding a listening host inside an exported
Godot process has its own runtime and distribution qualification requirements.

## Listener and request ownership

[PlayerWebSocketServerOptions](../../src/OpenFantasy.Server/PlayerWebSocketServerOptions.cs) requires
an explicit IP address, port and TLS certificate with private key. A wildcard bind requires an explicit
advertised hostname; a bound ephemeral port is reflected in the published `wss` endpoint. Certificate
ownership remains with the caller until listener disposal finishes. Clients validate the certificate
chain and server identity; credentials never compensate for an untrusted server.

[PlayerWebSocketApplication](../../src/OpenFantasy.Server/PlayerWebSocketApplication.cs) creates a
Kestrel listener with explicit configuration and no request logging providers. Only HTTP/1.1 TLS GET
upgrades at `/player` with the `openfantasy.v1` subprotocol are admitted. Query strings, browser Origin
headers, request bodies and transfer encoding are refused. WebSocket compression is not negotiated.
This is a native-client boundary; supporting browser clients requires a separate origin/authentication
contract, not removal of this check.

The listener allows 16 normal connections and eight upgraded connections. Request headers have a
five-second deadline, 8 KiB total and 32-header bound; request lines allow 1 KiB, bodies zero bytes, and
TLS negotiation five seconds. An independent eight-slot application request owner refuses excess work
without queuing it. The existing four-slot, ten-second world handshake and all message/session bounds
still apply. These connection limits do not increase the four-player world limit.

[PlayerWebSocketServer](../../src/OpenFantasy.Server/PlayerWebSocketServer.cs) awaits the complete
admission and command-session lifetime inside each HTTP request. A malformed or slow peer ends its
connection. A response that completes at or after its absolute send deadline raises the specific
`PlayerResponseTimeoutException`; unrelated backend timeouts are not classified as harmless peer errors.
Actual world-host faults take precedence, and an independent host-completion observer closes the listener
when the world owner terminates, even with no connected peers.

The listener registers no process-wide console or POSIX lifetime; its embedding application owns
those signals and invokes explicit stop. Stopping closes admission, cancels socket work and joins admitted requests, including an already-running
participant history read, before disposing the listener. Canceling a caller's shutdown wait does not
abandon cleanup. Listener shutdown leaves the world owner alive; the dedicated composition disposes that
owner after listener cleanup. Socket loss alone does not journal a disconnect, assign a replacement
controller, advance time or grant any outstanding human decision.

## Saved-world composition and invitations

[CargoPlayerHost](../../src/OpenFantasy.Hosting/Networking/CargoPlayerHost.cs) opens an existing verified
outcome-completion cargo world. It refuses missing/incompatible saves and admits one to four existing
human controllers. It never creates, upgrades or resets a world. The composition uses the same cargo
merchant configuration and actor-bound regional observers as local play, and explicitly closes its
initial local player port before remote admission. Players begin unready; no network connection advances
the world until the existing host readiness policy permits it.

[FarmPackageCatalog](../../src/OpenFantasy.Hosting/Content/FarmPackageCatalog.cs) supplies the retained,
validated farm manifest to the handshake. World/profile/admission pins identify the other admitted
composition contracts. The manifest does not stand in for a complete production content catalog.

[PlayerConnectionInvitation](../../src/OpenFantasy.Hosting/Networking/PlayerConnectionInvitation.cs)
contains a `wss` endpoint, compatibility pins, intended actor and a copied 32-byte bearer secret. Its
versioned canonical JSON is capped at 4 KiB; unknown/duplicate fields, alternate encodings and embedded
URL credentials are refused. The actor field helps the client check its intended destination; the host
selects actual control from its credential registry, never from a client actor claim.

[PlayerInvitationFile](../../src/OpenFantasy.Host/PlayerInvitationFile.cs) creates files exclusively:
existing invitations are never overwritten. Unix files are created with owner read/write permissions.
Windows files receive a protected current-user-only DACL at creation and verify that access before any
secret bytes are written, independently of inherited directory access. Administrators and an already
compromised owner account remain outside this local-user isolation boundary. Operators distribute the
files through a trusted channel and protect their private settings, certificate and password files.

[ServeCargoWorldCommand](../../src/OpenFantasy.Host/ServeCargoWorldCommand.cs) provisions these files
and announces only the endpoint and nonsecret status. Password input is a bounded local file. Certificate
loading uses ephemeral keys where supported and the platform's temporary PFX keychain on macOS; it does
not request permanent key persistence. Ctrl+C and POSIX SIGTERM initiate joined shutdown. Host restart
requires new credential provisioning; invitation lifetime limits new joins, not connected players' Wait.
See the [operator guide](../player-host.md) for configuration and ownership.
