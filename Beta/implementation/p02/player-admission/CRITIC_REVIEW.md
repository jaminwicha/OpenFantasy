# Independent admission critic and corrections

A native independent critic reviewed only the admission sources, shared host connection join and
new tests, read-only with a six-minute limit. It ran no tests or provider requests. Astra integrated
the findings; no repeat general critic was commissioned.

1. Transport disposal failure skipped real port cleanup while releasing its credential reservation.
   A fault-injected CloseTransport exception reproduced this: the old port remained Paused rather
   than Unavailable. Channel cleanup now closes the player in a nested finally before releasing its
   reservation. Transport lifetime disposal is likewise retained even when its close hook throws.
2. A stopped host could complete Welcome and return an unavailable player. An eight-message output
   queue held Welcome while the actual host stopped; draining it produced the erroneous success.
   Admission now checks the actual port/host run state after Welcome and refuses an unavailable port.
   Shutdown after that admission point is ordinary channel lifetime, not a promise of perpetual access.

The original failing run is qualification/admission-20261002T075241545454Z: 19 passes and exactly
these two failures. The unchanged regression cases remain in the final selection. Earlier build
failures (inaccessible Core encoding helper, then new-test analyzer violations) are retained separately;
no analyzer, old assertion or gate was disabled. A local explicit package fingerprint removes the
improper dependency on an internal Core serializer.

The critic found credential-selected identity and serialization-gated control/world-pin checks sound
within this scope. This is not a security certification, HTTP/TLS or native multiplayer qualification.
Package payload admission still belongs to the trusted operator/content loader and is stated explicitly.
