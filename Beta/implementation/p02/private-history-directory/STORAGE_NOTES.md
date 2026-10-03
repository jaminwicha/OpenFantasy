# Storage and selected-session implementation notes

The storage reader authorizes the current home entitlement or exact PrivateHistory witness under the
existing authority gate. It validates navigation before replay so malformed/future cursors are ordinary
refusals, then runs the existing complete authenticated journal replay through the pinned current head.
The directory observer projects actual terminal publications only and retains at most17 rows:16 results
and one lookahead. It does not retain roots, create an index, change canonical bytes, mutate world time,
or obtain permission from a cursor. Descending sequence and ascending kind/canonical identity order
preserve tied closures across pages. Later publications are excluded by the first page's inclusive cap;
later corruption still fails complete verification and fences authority.

Public immutable page values live in `Simulation/PrivateHistory/`. The page copies and bounds its rows,
validates terminal status/ordering/window/next-cursor shape and correlates the complete request. Core
`EntityId` explicitly allows every128-bit pattern, so these values introduce no zero-identity prohibition.
Malformed encoded identities remain the transport codec's concern. The selected result operation is
appended without renumbering prior operations; the generic envelope remains unchanged. Consumers validate
both its selection envelope and the page's request. Exact subject inspection remains separate.

The local port's dedicated directory slot keys the complete request plus selection and operation.
Identical running requests share a task; different requests are busy. Admission, serialized entry and
publication recheck the selection/current authority. Both host shutdown and individual port cleanup join
the directory task before storage ownership ends. No existing legacy reader is rebound to another actor.

Added tests in `Hosting.Tests/Simulation/PrivateHistoryDirectory/` cover a genuine19-outcome same-step
expiry (three quotes and16 proposals), real material closure pressure, archived original-genesis reopen,
complete multi-page discovery, participant filtering, stable append, PrivateHistory-only authority,
revocation, invalid/future cursors, corruption after the requested cap, due-but-unclosed tactical offers,
actor-cycle result invalidation, pending owner entitlement, exact task sharing/pressure and both host and
port cleanup. Page tests exercise copying, ordering, duplicates, bounds, status and request correlation.
The fixture checks the directory while the real offers remain open, before their common expiry.

No build or test was run by this worker; the integration lead owns terminal qualification. A lightweight
`git diff --check` passed. Tests reuse existing lawful fixtures and original five-second synchronization
limits; their source is new and old tests/Core bytes/specifications remain unchanged. Complete replay per
page remains a cost, not a production latency claim. Native browser/physical input and the retained
unexplained historical timing failures remain outside this increment. The source proof is not a pass
until the integration lead records executed results.
