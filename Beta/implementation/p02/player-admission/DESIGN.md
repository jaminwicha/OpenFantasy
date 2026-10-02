# Authenticated player admission

This increment binds bounded transport to existing player ports. It does not claim an HTTP/TLS
listener, native network client, complete public-view encoding, gameplay message dispatch or durable
disconnect policy. Those remain explicit next dependencies of the full player-networking design.

The operator supplies already admitted manifests; their complete dependency-resolved versions produce
one compatibility fingerprint. It is a package compatibility claim, not payload validation or private
world-state disclosure. The host derives world, branch, profile and admission pins from its actual root.
A client cannot select an actor by submitting principal/session claims. A privileged issuer verifies
an existing human binding and gives its trusted recipient an opaque random 256-bit bearer credential.
Registry entries retain only its SHA-256 digest, are limited to four actors, expire in monotonic host
time and are not saved. Reissue replaces an inactive credential; revocation prevents future admission,
not a hidden change to an already connected actor's control. Restart requires fresh credentials.

Hello has an exact 130-byte payload: world/branch IDs, profile, admission hash, package fingerprint
and credential. Welcome contains the same compatibility pins and the server-selected actor/principal/
session identities. Envelopes retain version 1; request IDs must be nonzero. No credentials occur in
Welcome, exceptions, logs or URLs. TLS and trusted out-of-band provisioning remain listener/operator
obligations; the adapter itself does not prove transport protection or server identity.

One service admits four pending handshakes maximum, each with one ten-second absolute deadline from
entry through first Hello, actual port admission and Welcome. Reassembly has the separate smaller
transport limit. Credential slots reserve exclusively during admission; failures close new ports and
release reservations. The host compares compatibility and current control while holding its existing
serialization gate. The local original port is never stolen: a dedicated server must explicitly close
it before allowing a remote claim of that same actor. All admitted ports start unready. Connection
closure retains the existing in-process behavior and cannot satisfy the journaled disconnect contract.

Acceptance requires malformed/foreign pins and stolen-identity contrasts, expiry/revocation/rotation,
duplicate/racing admission, bounded pending work and failed-Welcome cleanup. Use real LocalWorldSession
and SQLite authority for these tests. Preserve old transport/shared-player tests and run them after
the shared connection method refactor. New tests must prove no world mutation on refused admission.
