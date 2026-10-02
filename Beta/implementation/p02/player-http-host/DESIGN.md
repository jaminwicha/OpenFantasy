# TLS player hosting

Bind real HTTPS/WebSocket requests to existing admission, command/query routing and observations.
A separate OpenFantasy.Server adapter library keeps ASP.NET out of Core, Hosting and Godot. It
supports an in-process owner; the dedicated composition runs on the existing .NET10 Host runtime.
This is a library boundary, not a new authority or IPC bridge. Godot embedding/export of a listening
host remains a separate qualification, while its network client needs no ASP.NET dependency.

Use explicit TLS certificate/key, HTTP1.1 /player, required openfantasy.v1, no URL credentials,
no browser origins and no WebSocket compression. Bound normal/upgraded connections, parsing,
TLS and world handshake times, plus eight immediate-admission application slots. The HTTP request
stays alive until its admitted session finishes. Stop cancels transport work, joins sessions and
preserves pending authority reads. HTTP owns neither the world clock nor authority disposal.

A dedicated CargoPlayerHost opens an existing verified profile35 save, configures the same real
cargo merchants and regional observers, closes its initial local player port, and issues invitations
only for saved human controllers. Missing/incompatible files fail; opening never creates or upgrades.
Use the real validated retained package manifest, with world/admission pins for the other composition
contracts. This is not a claim that every production content family already has a package manifest.

Operator settings and invitation files are local, bounded, explicit and never overwriting. Invitations
contain endpoint, compatibility, actor and secret; secrets are excluded from URLs, stdout and ordinary
logs. Credential lifetime is enforced by existing monotonic admission. Passwords are read from a
local file, never required in CLI arguments. Real TLS tests use a narrow self-signed test certificate pin, never
an accept-all certificate callback. Production clients use normal certificate validation.

Reference review: Microsoft WebSockets documentation confirms request lifetime and disabling
compression for sensitive data; Kestrel documentation defines normal/upgraded connection limits.
https://learn.microsoft.com/en-us/aspnet/core/fundamentals/websockets?view=aspnetcore-10.0
https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/options?view=aspnetcore-10.0

Validate actual TLS/HTTP upgrade, certificate refusal, routing/observations over ClientWebSocket,
connection pressure and teardown, malformed requests, package authentication, secret file boundaries,
production bootstrap and process startup/shutdown. No new desktop/human/production scale acceptance
or journaled disconnect policy is inferred. The full spec and remaining dependencies remain open.
