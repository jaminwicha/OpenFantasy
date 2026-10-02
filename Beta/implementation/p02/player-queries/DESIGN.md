# Authenticated player queries

Expose the existing player port's five operations: submit/cancel/read a trade inquiry,
inspect a paid quote, and inspect a saved material proposal. Queries create no independent
authority, prices, receipt, resource transfer or consent. Actual offers remain merchant-authored;
acceptance remains an explicit ordinary world command. Inquiry state is transient and ends on
port closure. Historical reads retain current controller authentication and participant privacy.

Requests name only the inquiry's own public terms or a subject identity. They cannot choose an
actor, principal, session, world or branch. A typed result binds the authenticated port's
world/branch/actor plus operation and requested subject. Unknown and nonparticipant subjects
both return the same absent-value shape. Versioned binary request bodies and canonical typed
JSON results use exact bounds; results expose presentation values, never authoritative roots.

Each connection admits one query task independently of its one world-action task. A second
in-flight query gets Busy; readiness, movement and ping stay on the receive loop. No automatic
retries or history polling occur. The existing eight-response queue and five-second send limit
apply. Closure joins all admitted work before releasing authority dependencies. A history replay
still holds the existing host serialization gate: receiver responsiveness is not a claim that
history is free or cannot delay world cutoffs.

Validation uses actual SQLite authority, earned market stock/rewards, merchant selection and
saved/archived proposal outcomes. Prove transport parity, exact offered terms, archive lookup,
participant versus outsider/unknown responses, pending-query control responsiveness and closure,
malformed/oversized data and immutable prior regression/spec preservation. Full HTTP/TLS,
Godot network clients, recorded disconnect and production/scale/platform gates stay open.
