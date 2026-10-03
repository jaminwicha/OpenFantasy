# Authenticated player queries

## Authority and ownership

[PlayerPortQueries](../../src/OpenFantasy.Hosting/Networking/PlayerPortQueries.cs) dispatches queries
through the [authenticated player port](hosted-player-connections.md). Request values never select a
principal, session, actor, world or branch. The admission binding supplies those identities. Queries
return immutable presentation values and create no second authority, pricing algorithm or history store.

`SubmitTradeInquiry`, `CancelTradeInquiry` and `ReadTradeInquiry` operate on that shopper's transient
[trading inquiry](scheduled-trading.md). Submission names a market, commodity, buy/sell direction,
quantity and, for selling, the shopper's own shown lot. It promises no stock or price and creates no
durable receipt. A merchant decides whether to issue an actual quote under its existing opportunity
and resource rules. `Offered` identifies that quote; accepting it is a separate explicit world command.
Cancelling an inquiry does not withdraw a quote already issued. A new connection has no retained inquiry.

`InspectPaidQuote` and `InspectMaterialProposal` read one subject through the existing authenticated
participant inspection. An unknown subject and a subject belonging only to other participants both
return an absent value. Inspection preserves archived terms and closure information through verified
history; it does not recreate a live offer or revive its acceptance permission. Responses contain no
private delivery credentials, market pricing basis, world roots or private state hashes.

[Archived quote retrieval](../../src/OpenFantasy.Hosting/Worlds/WorldJournalRecovery.cs) uses one
complete replay. [WorldPaidQuoteReplaySelection](../../src/OpenFantasy.Hosting/Worlds/WorldPaidQuoteReplaySelection.cs)
retains at most one speculative root: the candidate whose revision actually publishes the selected
terminal outcome. The archive verifier separately authenticates the retrieved closing root. After
the whole history, head and retained tables validate, retrieval pairs the complete archived terminal
bytes with the captured publication. A later root carrying that outcome cannot replace its publishing
root. The original market record therefore remains available without another replay or a result cache.
Current control is checked before retrieval and participant disclosure afterward; unknown and foreign
subjects reveal no publishing root. Corruption after the requested event still refuses the entire read.

## Explicit actor-selection queries

[Selected local queries](../../src/OpenFantasy.Hosting/Simulation/LocalWorldSession.SelectedQueries.cs)
are a separate explicit interface. Each request captures the exact immutable selection: home actor,
operated actor, port identity, generation and assignment witness. The port supplies its actual trusted
delivery. Legacy query methods continue to address the home actor and are never relabeled as another
actor's result. [Selected network operations](selected-player-protocol.md) carry these explicit envelopes;
the existing legacy wire operations and their bytes remain unchanged.

| Selected operation | Required delegated capability | Information returned |
|---|---|---|
| Submit, cancel or read trade inquiry | Trade | The inquiry owned by that exact selection |
| Inspect open paid quote | Trade | One current open participant quote |
| Inspect open material proposal | Inventory | One current open participant proposal |
| Inspect paid quote or proposal history | PrivateHistory | One actual terminal participant outcome, live or archived |

PrivateHistory is independent of Trade and Inventory. It does not expose actionable open offers,
current stock or balances. Tactical advances can make an offer due while its book remains open;
exploration expiry closes that book through an actual terminal publication. Neither selected open
inspection nor terminal history exposes the intervening due-but-unclosed entry. Terminal proposal inspection omits the sender-presence pin used to prepare a live decision. Unknown,
foreign and wrong-lifecycle subjects return an absent value after authorization. Null-witness home
reads retain permanent entitlement; inquiry mutation also requires the port's available control.

The host checks the selection and capability at admission, after entering serialization and before
publishing the result. [Typed storage readers](../../src/OpenFantasy.Hosting/Worlds/SqliteWorldAuthority.SelectedInspections.cs)
independently check current authority under the writer gate before subject lookup and enforce the
open/terminal distinction. Open reads use only the current root. Archived reads retain the complete
verified replay, pinned head and exact quote publishing root described above. Neither path creates a
cache, new journal, new grant or synthetic owner delivery.

A [selected result](../../src/OpenFantasy.Hosting/Simulation/EffectiveControl/LocalSelectedQueryResult.cs)
retains the captured world, branch, selection, operation and subject. Its `ValidateFor` method compares
that context with the observation and request being presented. A transition that occurs after a lawful
read completes still invalidates application of its result. Returning to the same actor does not restore
the old generation. The result itself grants no execution authority; accepting an offer remains a
separate command checked against current world state. Consumers must apply this validation when
binding selected responses to presentation; the legacy network response is not a selected envelope.

Selected and legacy reads share each port's existing quote/proposal inspection slots. A selected key
includes operation, subject and the complete selection. Identical running work shares its task;
a different operation or generation is busy until that work finishes. Switching never clears an
active slot or abandons its worker. History work remains off the caller's thread and holds host
serialization, so normal host selection/revocation waits for it. Shutdown joins admitted work before
releasing storage. An admitted read still waiting for serialization is cancelled on shutdown, including
when semaphore acquisition wins the race with cancellation. It does not become a new-call disposal
error after acquiring the slot. New closed legacy calls remain disposal errors; an executing selected
read still validates current authority before disclosure. Expected stale or revoked requests are refusals, not host faults; damaged durable
history still fences authority and fails the session.

## Wire values and correlation

[PlayerQuery](../../src/OpenFantasy.Hosting/Networking/PlayerQuery.cs) is a closed set of typed
operations. The [request codec](../../src/OpenFantasy.Hosting/Networking/PlayerQueryCodec.cs) uses
versioned, bounded binary fields and rejects unknown operations, truncated/trailing data and invalid
inquiry shapes. A request occupies a `Query` player envelope with its strictly increasing connection
request ID. These IDs correlate messages and confer no durable idempotency.

The first byte is payload version 1; the second is the operation (submit 1, cancel 2, read 3,
quote inspection 4, proposal inspection 5, presentation bootstrap 16, participant status 17).
Cancel/read/bootstrap/status are exactly two bytes. An inspection appends
one canonical 16-byte entity ID, totaling 18 bytes. Submission appends market ID, commodity ID,
32-byte commodity content hash, one-byte direction and unsigned big-endian 64-bit quantity.
A buy is 75 bytes. A sell appends its own shown lot's 16-byte ID, totaling the maximum 91 bytes.
Direction determines the shape; there are no optional trailing fields or caller-control claims.

[PlayerQueryResult](../../src/OpenFantasy.Hosting/Networking/PlayerQueryResult.cs) binds world, branch,
actor, operation and inspected subject, with only the relevant inquiry/quote/proposal value present.
`ValidateFor` matches a decoded result against the outstanding request and expected connection scope
before presentation. Clients also match the outer response request ID. A null inspection is an ordinary
typed result, not a transport refusal or a claim that the identifier does not exist elsewhere.

The [result codec](../../src/OpenFantasy.Hosting/Networking/PlayerQueryResultCodec.cs) encodes the original
five operations as version 1, with their exact eight-field result shape. Bootstrap and participant status
use only version 2, containing scope, kind, Presentation and ParticipantStatus; precisely the relevant
payload is present. Each version rejects the other version's operations. Both use canonical typed JSON
within 16 KiB. The codec reuses the explicit observation converters, including complete
hashes and immutable collections. Decode checks the byte bound before parsing, rejects unknown/missing/
duplicate fields through validation and exact canonical re-encoding, and enforces participant/subject
consistency. This is a presentation contract; constructing or decoding a result grants no authority.
The protocol performs no automatic retry, repricing or background history polling. The application
session may refresh participant status under the bounded ownership below.

## Presentation and participant status

`ReadPresentationBootstrap` returns an immutable binding captured by the trusted host composition from
the actual admitted world: companion/item identities, regional presentation choice and bounded source
labels/appearances for cargo stalls. The [bootstrap value](../../src/OpenFantasy.Hosting/Networking/PlayerPresentationBootstrap.cs)
copies its sorted collection and validates distinct roles, text and appearance bounds. It contains no
positions, stock, hidden schedules, prices or permission. Current visibility still comes from observations.
Admission attaches this binding to the actor's credential; a host without a supported binding refuses
the request. A native client never reconstructs the server's genesis to invent presentation metadata.

`ReadParticipantStatus` asynchronously enters the existing host serialization boundary and captures
the calling port's run state and its current/last inquiry under the input lock. It selects no step and
may wait behind admitted history work. Paused describes this port's readiness: other ready players may
continue. Waiting and resolving retain the host's actual hold interpretation. Status and world views
are separate publications and do not assert one shared observation revision.

The [remote application session](player-client.md#application-session-and-native-binding) caches status
for frame reads. Its bounded refresh carries null, Waiting, Offered and terminal inquiry values without
polling quote history or changing the merchant's decision. Explicit queries share that same query slot.

## Concurrency, pressure and lifetime

[PlayerCommandSession](../../src/OpenFantasy.Hosting/Networking/PlayerCommandSession.cs) admits one
query task per connection, independently of its one pending world action. Another query while that
task is pending receives `Busy`, including a duplicate subject. The receive loop remains available for
readiness, movement and ping. A host preflight refusal becomes `NotAdmitted` without exposing exception
details. Both refusals remain transient; neither is a committed world rejection.

Query results share the eight-response queue and five-second send deadline. Producers enqueue without
waiting for socket output; overflow terminates that connection. Completed query work can release its
slot while its reply is still queued, so total output pressure remains bounded separately. Slow output
does not hold the authority gate. A history replay itself holds the existing host serialization gate
and can delay world cutoffs; asynchronous execution does not imply free simulation throughput.

Connection closure cancels transport loops and joins admitted query work through port closure before
releasing its dependencies. Completed world effects remain durable; transient inquiries are forgotten.
An inspection already admitted may finish during closure, but a closed port admits no new read. The
adapter does not interrupt SQLite replay or dispose shared authority underneath a pending read.
