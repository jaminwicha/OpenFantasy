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

## Wire values and correlation

[PlayerQuery](../../src/OpenFantasy.Hosting/Networking/PlayerQuery.cs) is a closed set of five typed
operations. The [request codec](../../src/OpenFantasy.Hosting/Networking/PlayerQueryCodec.cs) uses
versioned, bounded binary fields and rejects unknown operations, truncated/trailing data and invalid
inquiry shapes. A request occupies a `Query` player envelope with its strictly increasing connection
request ID. These IDs correlate messages and confer no durable idempotency.

The first byte is payload version 1; the second is the operation (submit 1, cancel 2, read 3,
quote inspection 4, proposal inspection 5). Cancel/read are exactly two bytes. An inspection appends
one canonical 16-byte entity ID, totaling 18 bytes. Submission appends market ID, commodity ID,
32-byte commodity content hash, one-byte direction and unsigned big-endian 64-bit quantity.
A buy is 75 bytes. A sell appends its own shown lot's 16-byte ID, totaling the maximum 91 bytes.
Direction determines the shape; there are no optional trailing fields or caller-control claims.

[PlayerQueryResult](../../src/OpenFantasy.Hosting/Networking/PlayerQueryResult.cs) binds world, branch,
actor, operation and inspected subject, with only the relevant inquiry/quote/proposal value present.
`ValidateFor` matches a decoded result against the outstanding request and expected connection scope
before presentation. Clients also match the outer response request ID. A null inspection is an ordinary
typed result, not a transport refusal or a claim that the identifier does not exist elsewhere.

The [result codec](../../src/OpenFantasy.Hosting/Networking/PlayerQueryResultCodec.cs) encodes version 1
as canonical typed JSON within 16 KiB. It reuses the explicit observation converters, including complete
hashes and immutable collections. Decode checks the byte bound before parsing, rejects unknown/missing/
duplicate fields through validation and exact canonical re-encoding, and enforces participant/subject
consistency. This is a presentation contract; constructing or decoding a result grants no authority.
There is no automatic query retry, repricing, refresh or background history polling.

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
