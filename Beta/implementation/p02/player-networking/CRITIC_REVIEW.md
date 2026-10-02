# Transport critic and disposition

The independent native `shared_sessions_critic` reviewed framing, transport lifetime and boundedness
read-only, with a five-minute scope. No external provider request, build or edit was assigned.

Finding: a byte cap alone permits arbitrarily many zero-length nonfinal WebSocket continuations.
Bound receive work and absolute reassembly time. Bound the initial Hello separately; fragments must
not renew admission time. Cancellation after receiving a prefix must not resume at its suffix.

Disposition: the socket adapter enforces 1,024 receive parts and a five-second absolute deadline
from its first fragment, checks elapsed time even after successful reads, and aborts on socket
cancellation. Two actual loopback tests exercise empty continuations and a stalled partial message;
the latter also proves further receive is refused. All 18 transport cases pass after this correction.
The critic found no other substantiated issue in this transport scope. Auth/TLS/game binding remain
separate work; this review does not qualify those boundaries.
