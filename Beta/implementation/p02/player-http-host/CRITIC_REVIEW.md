# HTTP host review

One bounded independent native critic reviewed the listener, shutdown, saved-world composition and
secret provisioning. This was an Astra-side review, not a Claude request or claimed credit saving.
Two concrete findings were retained and fixed:

1. A successful send completing at/after its five-second absolute deadline threw a generic timeout
   that the listener treated as a backend fault, disconnecting other peers. A specific
   `PlayerResponseTimeoutException` now identifies only this peer deadline. Actual host faults retain
   precedence; other backend timeouts remain fatal. New tests drive a real command session over a
   deterministic late-success transport, invoke the same production failure-disposition method and
   then authenticate/Ping over actual TLS. This is a composed boundary regression, not a claim that
   the test forced the operating system socket to finish at exactly five seconds. A contrasting generic
   timeout faults the listener while preserving the authority's independent lifetime.
2. Windows files originally inherited directory access, potentially exposing bearer secrets. They
   now receive a protected current-user-only DACL atomically at create-only file creation, verify the
   open handle's permissions before writing, and refuse unverifiable access. The Windows-only test
   constructs an inherited Users-readable parent, proves ordinary-file inheritance, and checks the
   invitation blocks it. This case is explicitly unrun on macOS, not counted as a pass.

The same critic performed a focused source recheck: both findings addressed, no new actionable defect
in touched paths. No tests were run by the critic. The lead owns execution and platform qualification.
The worker implemented only the typed timeout, production handler extraction and its two new tests;
the lead implemented bootstrap, protocol provisioning, permissions, tests, docs and integration.

Lead testing also exposed malformed conditional-skip metadata, a Windows analyzer warning in a test
lambda, macOS PFX loading incompatibility, an overly narrow assumption about when a refused upgrade
becomes visible to ClientWebSocket, and duplicate process signal ownership. All original failures remain
in qualification/. The pending-handshake rejection assertion still requires a WebSocket failure within
the same bound and a healthy listener; refusal can be observed during upgrade or the subsequent receive.
An explicit owner-driven IHostLifetime replaces the implicit console lifetime, so the library never
registers competing process signal handlers. The actual SIGTERM process test retains the zero-exit,
joined shutdown and unchanged-world reopening assertions.

Sources consulted: the .NET macOS PFX implementation rejects EphemeralKeySet and creates a temporary
keychain without PersistKeySet; the operator and architecture references state that platform distinction.
https://github.com/dotnet/runtime/blob/main/src/libraries/System.Security.Cryptography/src/System/Security/Cryptography/X509Certificates/X509CertificateLoader.macOS.cs
Windows file creation applies security at creation through the framework API, not a post-write chmod analogue.
https://learn.microsoft.com/en-us/dotnet/api/system.io.filesystemaclextensions.create

No acceptance threshold, existing test/specification or saved authority rule was weakened. Runtime
Windows/Linux, native Godot multiplayer, recorded disconnect, export/human and production qualification
remain separate obligations. Final executed results are indexed by FINAL_CHECKS.json once accepted.
