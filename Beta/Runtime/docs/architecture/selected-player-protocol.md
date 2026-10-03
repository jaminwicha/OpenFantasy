# Connected character control

## Permanent participant and selected character

[ISelectedPlayerSession](../../src/OpenFantasy.Hosting/Simulation/ISelectedPlayerSession.cs) is the
optional application contract for consent and operated-character input. `IPlayerSession.ReadView`
continues to identify the authenticated home character. `ReadSelectedObservation` pairs that home view
with the exact port-bound selection, optional capability-scoped controlled view, home control status and
readiness. Missing controlled facets are withheld data; they are never replaced with empty inventory,
zero statistics or a synthetic full player view.

The [remote facade](../../src/OpenFantasy.Hosting/Networking/Client/RemotePlayerSession.Selection.cs)
implements the optional interface over the authenticated protocol client. Capability checks expose
consent only for its admitted permission/delegation profiles and selection only for its delegated-control
profile. The dedicated cargo host admits those explicit saved profiles through the existing source-bound
cargo/threat composition. Opening a world never adds permissions, converts existing NPCs to humans or
upgrades a lived save.

## Home-authorized consent

[LocalWorldSession.ControlPermissions](../../src/OpenFantasy.Hosting/Simulation/LocalWorldSession.ControlPermissions.cs)
serializes permission-board reads and permission commands with the sole durable authority. Administration
requires the current established port and its permanent home delivery, independently of the selected actor.
Offer and acceptance keep the Core Human requirement. Original-owner revocation remains possible while
human restoration is pending; it cannot cancel accepted gameplay obligations or manufacture readiness.
Permission bookkeeping advances the journal revision without advancing world time or charging an action.

The immutable [permission board](../../src/OpenFantasy.Hosting/Simulation/EffectiveControl/LocalControlPermissionBoard.cs)
contains world/branch/home scope, the home actor revision needed to prepare a command, the bounded human
roster and only slots in which that home actor is owner or grantee. Rows expose exact offered/accepted
terms, revisions and supported capability flags without private principals or session credentials.
Eligible incoming rows carry captured acquisition terms. `HeldWitness` describes this port's actual
assignment even when it selects home. Acquisition always rechecks present transport, original expired
owner episode, accepted permission and safe gameplay boundary; the board itself grants no authority.

Commands retain their caller-authored identity and actual authenticated delivery. Exact prior receipts
are returned before fresh lifecycle checks; different content under the same command identity is refused.
Delegated command witnesses never authorize onward permission administration.

## Negotiation and framing

The TLS WebSocket uses the established `openfantasy.v1` transport. After authenticated Welcome, the
supported client explicitly subscribes with `SelectionSubscribe` and payload version 1 before requesting
observations. The server accepts this once in the delegated-control profile. Its first correlated full
selected observation and its delivery proof complete negotiation. Legacy view/control codecs retain their original bytes and
home semantics; the selected stream does not reinterpret a legacy observation as another actor.

| Message kind | Role |
|---|---|
| SelectionSubscribe (15) | Initial authenticated feature subscription |
| SelectionObservation (16) | Full atomic home/selected frame; zero ID for publication, request ID for subscription/selection reply |
| SelectionInput (17) | Select, pause, Resume, movement, captured action or selected query |
| SelectionQueryResult (18) | Exact selection/operation/subject query response |
| ControlRequest (19), ControlResult (20) | Separately correlated home consent, board, acquisition and release |
| SelectionDelivered (21) | Unsolicited confirmation that the captured Resume basis completed its bounded send |

[Selected codecs](../../src/OpenFantasy.Hosting/Networking/Selection/PlayerSelectedObservationCodec.cs)
validate the complete typed value, including capability sections, actor identities, request preparation
pins and shared observation context. The selected body is canonical JSON with the shared bounded
compression format: the combined home and controlled frame fits the 900 KiB body and 16 MiB expanded limit,
inside the 1 MiB transport envelope. The decoder rejects missing, duplicate, unknown and contradictory
fields. Full frames have one stream ID and increasing revision; there is no selected delta fallback.

[Selected inputs](../../src/OpenFantasy.Hosting/Networking/Selection/PlayerSelectedInputCodec.cs) use
an explicit binary version and exact selection. Inner world commands retain their 16 KiB cap and canonical
bytes; witnesses retain their 256-byte cap. Administration and selected query replies use bounded 16 KiB
canonical bodies. Request IDs increase across the whole connection, regardless of operation family.
Parsing completes before host dispatch. A decoded request or response confers no authority.

## Delivered observation and deliberate Resume

The [server stream owner](../../src/OpenFantasy.Hosting/Networking/PlayerCommandSession.Selection.cs)
serializes selected-frame capture and enqueue order. A frame becomes the Resume basis only after its
transport send completes within the unchanged send deadline. Queuing a frame grants no readiness
authority. The pending-frame table is bounded by the response queue plus its single active send.
The server then sends a versioned, 25-byte stream/revision confirmation with request ID zero.
The first delivered observation of each selection/home-control lifetime remains its Resume basis;
ordinary later views do not rotate that proof.
The client tracks bounded pending confirmations, rejects unknown or duplicate proofs, and completes
subscription or actor-selection callers only when the current lifetime is confirmed. A delayed proof
for an earlier observed lifetime cannot confirm a newer selection. It requires this confirmation and
its currently presented selection/control before sending
Resume with the original stream/revision. The server matches the retained proof, then calls the ordinary
host Resume boundary with that actual observation. New selection and pending/restored control require
another deliberate Resume; held movement/readiness never crosses the transition.

Spontaneous selection/restoration frames publish promptly, even while their delivery confirmation is
pending. `SelectedInputUnavailableException` distinguishes an unavailable captured selection or Resume
proof from terminal connection failure. The application discards that input and presents feedback;
it neither redirects it to the new character nor marks the world failed. A later explicit choice can
use the current delivered observation.

Pause and movement also name their captured selection, including zero/stop input. Their acknowledgments
are correlated and bounded, and stale input cannot stop or resume a later selection. The original movement
lease and fresh-sample coalescing remain unchanged. The client refuses unscoped gameplay methods on a
selected protocol connection, so callers must use the optional interface from generation zero onward.

## Requests, history and cleanup

The server retains one gameplay action, one query, one consent/assignment administration and one actor
selection operation per connection. Selected and legacy queries share the same query slot; the host's
quote/proposal slots remain shared as described in [player queries](player-queries.md). Output queues,
input rates and send deadlines stay bounded. Cleanup joins admitted operations before releasing owners.
Administration can revoke while an already accepted paid action is still resolving; sharing its waiter
would incorrectly block that consent boundary.

An encounter decision's acceptance receipt releases the ingress action slot while its paid resolution
can remain pending in canonical encounter state. A grantee operating another human can accept that
character's decision, deliberately select and Resume home, and submit its own required decision. Each
other human supplies its own choice. Selection changes preserve accepted actions; neither delegated
control nor a network timeout supplies missing home-character decisions.

The [protocol client](../../src/OpenFantasy.Hosting/Networking/Client/PlayerProtocolClient.Selection.cs)
checks world/branch/home scope, stream/port/generation order, exact request correlation and original
command receipt hashes. Equal selection generations require the same entire selection, including its
witness. Control-port identity stays fixed and control generation cannot decrease. A late selected query is authenticated against its original request and then
refused for application if selection changed. Application code validates captured result envelopes again
against the observation being presented. The remote facade shares foreground query ownership with its
bounded participant-status refresh; it never polls history automatically.

An original command can retrieve its exact authenticated submitter receipt after switching, revocation
or reconnect. Its old selection remains attached to the request. The host checks an exact existing
receipt first and requires current selection and permission if no receipt exists. This is neither a
fresh command retry nor authority to act through an old witness.

## Native presentation

[FarmClientSession.ActorSelection](../../client/scripts/FarmClientSession.ActorSelection.cs) captures
selection and witness in each issued action offer alongside actual delivery. A separate typed offer builder
uses the controlled facets; the ordinary home offer path retains its original full home view. Submitting
an unaccepted stale offer is refused, while a previously submitted offer retains its original task/result.

[FarmGame.ActorSelection](../../client/scripts/FarmGame.ActorSelection.cs) mediates operated movement,
targeting, camera and supported action offers. Its Characters panel uses explicit initially unchecked
permission scopes, relevant board rows and supported acquisition/selection/release operations. Quote
review presents exact captured terms and confirmation submits those terms unchanged. Home human
obligations remain represented alongside the selected character; unavailable facets do not create inert
menus. Rendered usability, input and complete domain-screen coverage are separate acceptance obligations.

The [Characters scene](../../client/scenes/FarmControlPanel.tscn) owns layout, with a grant column and
scrollable permission/selection rows below the main header. Recipient choice is explicit; refresh and
reopening preserve its actor identity and checked-scope draft for the same home character. Those local
draft values confer no permission. Changing home identity clears the draft. Busy reads and mutations
disable competing panel operations while Close remains available.

Each panel opening has a distinct lifetime. An older board read, inquiry or completed mutation cannot
populate a closed or subsequently reopened panel. Row callbacks also retain their row generation and
must still belong to the visible tree. Closing, changing selection or editing the quantity discards
the applicable quote review; confirmation never silently replaces its captured terms. Accepted work
continues under the ordinary authority even when its originating panel closes.

The selected HUD separates the operated character's name and permitted health/magic resources into
readable lines. A withheld wallet creates no empty header column or zero-balance claim. Returning home
restores the home supplies display. Full text and sprite visibility are checked after layout/drawing;
an underlying value or an on-screen control rectangle alone does not establish readable presentation.
