# Participant history discovery

## Fact ownership and disclosure

The directory discovers completed paid exchanges and material proposals belonging to one selected
character. The durable world journal owns these facts. The directory introduces no event store,
material owner, permission, cached answer or gameplay action. It returns identifiers and terminal
summaries; [individual history inspection](player-queries.md#explicit-actor-selection-queries) supplies
exact recorded terms through its existing authenticated reader.

A [directory entry](../../src/OpenFantasy.Hosting/Simulation/PrivateHistory/LocalPrivateHistoryEntry.cs)
contains its kind, subject identity, counterparty identity, terminal publication sequence and matching
terminal status. A quote status cannot be substituted for a proposal status. Open entries and entries
whose expiry is due but whose book has not actually closed are excluded. The directory exposes no
live stock, balance, sender-presence pin, delivery credentials, private hashes or world snapshots.
Participant filtering precedes page selection and lookahead, so foreign records do not affect the
visible count or next-page indicator.

`PrivateHistory` is independent of Trade and Inventory. Current delegated authority must authorize
that capability; current home entitlement remains with the permanently authenticated character.
The request never names a replacement principal, actor, world or branch. The exact actor selection
supplies port, home actor, operated actor, generation and immutable assignment witness. Current
selection and permission are checked at admission, after entering session serialization, under the
storage gate before retrieval and before publishing the result. A previously disclosed page grants
no authority to execute or inspect an open offer.

## Stable bounded navigation

A [page request](../../src/OpenFantasy.Hosting/Simulation/PrivateHistory/LocalPrivateHistoryPageRequest.cs)
has an optional continuation cursor and no configurable page size. A
[page](../../src/OpenFantasy.Hosting/Simulation/PrivateHistory/LocalPrivateHistoryPage.cs) copies at
most 16 entries and echoes the exact request. The first read captures an inclusive journal revision
ceiling. Continuation retains that ceiling while new world publications may advance the actual head.

Records are ordered by descending terminal sequence, then ascending record kind and canonical
subject identity. A cursor contains the ceiling and the last emitted complete ordering tuple; the
next page begins strictly after it. Several records can close in the same publication, so a sequence
alone cannot identify a page boundary. Later records beyond the ceiling are absent from that traversal.
An explicit new first-page request obtains a new ceiling.

A cursor is a bounded filter, not a credential or proof that a record exists. Invalid kinds, malformed
identity encodings, impossible sequence relationships and ceilings beyond the current authorized head
are refused. Subject identities retain the full canonical 128-bit domain, including zero; navigation
does not introduce a narrower identity law. The page validates ordering, duplicate subjects, matching terminal statuses, cap, exact
request and exact final-row cursor. A next cursor is emitted only when another eligible participant
record exists. No total count or foreign-record metadata is disclosed.

## Complete replay before return

One complete journal replay visits actual terminal publications, retaining only the best page plus
one lookahead record. It observes a terminal outcome at its publication sequence, rather than
recounting every subsequent snapshot that still carries it. The complete replay verifies retrieved
archives, checkpoints, command and identity tables, controller recovery and the final head before
any collected summaries leave storage. The authority compares that recovered head with its pinned
live state. Corruption after the requested records or after the navigation ceiling still refuses the
read and fences damaged authority.

Discovery requires no retained publishing snapshot per row. Exact paid-quote detail inspection retains
its own publishing-root proof because it returns terms that need that root. Summary discovery does not
replace or weaken that proof. No new persistent index, schema interpretation or save migration is
implied by a directory request.

Replay runs off the calling thread within the existing host serialization and storage boundaries.
It selects no simulation step but can delay world cutoffs and other queries while holding those
boundaries. Fixed output and collector size do not establish constant replay cost or production
latency. Any future indexed or incremental reader must preserve authenticated completeness and
current permission; a faster unverified table scan cannot replace the contract.

## Query lifetime and consumers

Each player port owns one directory inspection slot. Identical outstanding requests share their task;
a different request is busy until admitted work completes. The key includes the complete paging
request, selection and operation. Actor changes do not clear a running task or abandon its worker.
Both port disposal and session shutdown join admitted reads before releasing storage. Network reads
also share the connection's existing single query slot, bounded response queue and send deadline.

A selected result binds world, branch, selection and `ReadPrivateHistory`; the page additionally binds
the complete paging request. Consumers validate both before applying it. Switching away and back
creates a new generation; a late earlier page cannot populate that new selection. Revocation, owner
return and replacement similarly invalidate the old presentation context. Presentation must also
fence its own panel opening, page request and inspected-row lifetime.

The optional history application contract exposes `SupportsPrivateHistoryDirectory` and
`ReadPrivateHistoryObservation()` alongside the explicitly captured page query and the typed terminal
quote/proposal inspection methods. The owned local facade
captures its actual home port without requiring the broader character-control interface. The remote
facade captures its negotiated selected observation and reports support only for the selected protocol
profile. Support describes API availability, not a current delegated PrivateHistory grant. Both facades
retain the same request authority and selection rules; consumers never invent a port or generation.
The remote facade uses its existing foreground query owner and never polls history automatically. A history-only interface entry point must remain independent
of Pack's Inventory permission and Market's Trade permission. Characters remains responsible for
permission and control management, rather than duplicating domain screens.

## Native presentation ownership

[`FarmHistoryModel`](../../client/scripts/FarmHistoryModel.cs) owns one transient directory page,
one explicitly selected row and one typed historical detail. Opening and the explicit Latest control start a
first-page request. Next submits only the returned continuation cursor, retaining the directory's
revision ceiling. Latest deliberately begins a new traversal; the client retains no unbounded page
cache or inferred archive total. There is no automatic history polling or retry.

Every request captures the actual history observation and the current opening and page generation.
Detail inspection also captures the selected row generation. Completion verifies current entitlement,
world, branch, port, selection and witness, the complete requested page, and the presentation capture.
A detail must match its displayed record's identity, participant, terminal status and publication
sequence. Historical terms are inspection data and cannot become an executable offer.

Closing, lost focus or changed selection clears the private displayed state. Reopening never reuses
an outstanding request as that new visit's answer. Admitted work remains owned until its success or
failure has been observed; shutdown drains it alongside session disposal. A pending old request can
make a new visit busy, requiring explicit Latest after completion instead of an automatic retry.

The scene-owned [`FarmHistoryPanel`](../../client/scripts/FarmHistoryPanel.cs) presents the selected
character, bounded rows, typed details and explicit navigation. Its input layer neutralizes walking
through the ordinary client path while preserving host timing. Its controls and opening do not require
Inventory or Trade; lack of PrivateHistory remains a disclosure boundary rather than a reason to
substitute the home character's records. Timing text distinguishes local pause from a shared world
that other ready players can continue advancing.
