# Dedicated player host

The headless executable can serve an existing outcome-completion cargo save over authenticated
HTTPS/WebSocket. It uses the same saved human control, NPC policies and world rules as local play.
It does not create a new save or upgrade an older profile. Stop the other owner of that save first;
SQLite world ownership remains exclusive. The Godot network-client and native multiplayer delivery
have their own acceptance requirements; starting this listener alone does not establish those features.

Build the runtime solution with its pinned SDK. The dedicated executable uses .NET 10 and requires
its matching ASP.NET Core shared runtime (or an explicitly qualified self-contained distribution).
The Server library targets .NET 8 to preserve the reusable adapter boundary. Clients do not need
ASP.NET Core merely to connect. Use a TLS server certificate whose identity matches the advertised
hostname and whose issuer the intended clients trust; never disable certificate validation to connect.

Create a fresh one-to-four-human cargo world before serving it, for example:

```sh
dotnet src/OpenFantasy.Host/bin/Debug/net10.0/OpenFantasy.Host.dll --create-cargo-roster saves/cargo-world.sqlite3 world-seed "Amber" "Birch" "Cedar" "Dawn"
```

Supply one through four ordered names. One name preserves the solo composition. Each additional
player receives an independently owned axe and sickle; existing NPCs, funds, quest rewards and care
supplies remain intact. Creation refuses an existing database. This operation does not add players
to a lived save. See [fresh roster architecture](architecture/player-rosters.md).

Create a local JSON settings file, for example:

```json
{
  "Database": "saves/cargo-world.sqlite3",
  "ListenAddress": "127.0.0.1",
  "Port": 9443,
  "PublicHost": "localhost",
  "CertificatePath": "private/server.pfx",
  "CertificatePasswordFile": "private/server-password.txt",
  "InvitationDirectory": "private/invitations-for-this-run",
  "CredentialLifetimeSeconds": 600
}
```

All paths resolve relative to the settings file. `CertificatePasswordFile` can be omitted for an
unprotected PFX. The password is read as UTF-8 with trailing line endings removed; it is never supplied
as a command-line argument. Protect the PFX, password and settings using the host account's access
controls. On macOS, .NET loads PFX keys through its temporary keychain; other desktop platforms use
`EphemeralKeySet`. The host does not request permanently retained keys.

`ListenAddress` is a numeric IPv4/IPv6 interface. Loopback permits local clients only. Choose an
appropriate interface, advertised DNS identity, certificate and firewall policy when deploying to
other machines. A wildcard address requires an explicit non-wildcard `PublicHost`. Port zero selects
an available port and the announced endpoint/invitations contain that actual port. The only route is
`wss://<PublicHost>:<Port>/player`, using WebSocket subprotocol `openfantasy.v1`.

From the runtime directory, start the already-built executable:

```sh
dotnet src/OpenFantasy.Host/bin/Debug/net10.0/OpenFantasy.Host.dll --serve-cargo-world /absolute/path/to/settings.json
```

The process announces its endpoint after provisioning one `<actor>.player.json` file per saved human
controller. These files contain bearer credentials. Their Unix mode is owner read/write; their Windows
DACL grants only the current user access and blocks inherited grants before credential bytes are written.
Do not paste their contents into chat, logs or public URLs. Distribute each player's file through an
appropriate trusted channel. Directory visibility alone is not credential access, but the operator
should still choose an account-owned directory to prevent interference with provisioning.

Invitations are create-only. Choose a fresh output directory for each host run, or deliberately remove
obsolete files after preserving anything needed. Existing files are refused without modification;
a partially failed start can leave files whose credentials are unusable after that host terminates.
A missing/incompatible world, invalid settings, certificate failure or provisioning failure stops the
command with a nonzero exit code and a nonsecret exception category. It never creates/replaces the save.

Credentials allow new joins for 30 seconds to 24 hours (ten minutes by default). Their expiry does not
disconnect a player already considering a Wait decision. Restarting the host creates a new transient
credential registry, so old invitation files cannot reconnect. Endpoint/compatibility/actor metadata
must be checked by the client, alongside normal TLS identity validation and the authenticated handshake.

Ctrl+C or POSIX SIGTERM cancels network work and joins admitted requests before releasing the world.
No players being ready means no simulated-time advancement. A connection close does not itself create
saved controller-handoff decisions. See [HTTP/TLS architecture](architecture/player-http-host.md) for
request limits, ownership and the separate recorded-disconnect contract.

## Enable collective withdrawal on a copy

Stop the source world's host before preparing or applying this offline operation. The source must be
an eligible effective-control world (profile40) in exploration, with no reading hold, active encounter
or pending excavation continuation. Use a new request path and a new destination save path:

```sh
dotnet src/OpenFantasy.Host/bin/Debug/net10.0/OpenFantasy.Host.dll --prepare-party-withdrawal saves/adventure.sqlite3 saves/withdrawal-request.bin
dotnet src/OpenFantasy.Host/bin/Debug/net10.0/OpenFantasy.Host.dll --enable-party-withdrawal saves/adventure.sqlite3 saves/adventure-withdrawal.sqlite3 saves/withdrawal-request.bin
```

Preparation reports the exact world, branch, source revision/head hash and request hash. It does not
advance the world or hold a lease between invocations. Application rechecks that exact request against
the source and publishes only a verified new copy; original admission, history, receipts and possessions
are retained. Existing request/destination paths are refused. If the source advances, the old request
fails; inspect the new state and explicitly prepare another request at a new path. No automatic retry
or silent repinning occurs. Invalid requests or ineligible sources return a nonzero exit status.

Point the listener settings' `Database` at the new copy to serve it. The original save remains intact;
ordinary opening never performs this transition. A local solo opener can also select a migrated copy
using its exact original seed/name identity; this does not automatically select a new default path.
The [withdrawal reference](architecture/party-withdrawal.md#explicit-saved-world-transition) defines
eligibility, original-history verification and collective departure semantics.
