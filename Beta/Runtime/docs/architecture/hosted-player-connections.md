# Hosted player connections

## Ownership

[LocalWorldSession](../../src/OpenFantasy.Hosting/Simulation/LocalWorldSession.cs) is one in-process
host: one durable authority, serialization gate, monotonic cutoff loop and set of autonomous
controllers. [LocalPlayerConnection](../../src/OpenFantasy.Hosting/Simulation/LocalPlayerConnection.cs)
is a player port into that host. It owns no writer or timer. The host's original local API forwards
to `Player`, so solo clients use the same path as additional ports.

`ConnectPlayerAsync` validates an already admitted human resident against its trusted principal/session
binding. It admits at most four live ports, with one port per actor. It creates no actor, control
assignment, network authentication or save migration. An incorrect region observer is refused because
its actor must match the port. Each port has its own immutable `LocalWorldView`, input lease, outstanding
command, readiness and history-inspection slots. Inventory, offers and outcome standing project for
that port's actor; port registration grants no access to another actor's private possessions or history.

The [player coordinator](../../src/OpenFantasy.Hosting/Simulation/LocalWorldSession.Players.cs) serializes
attachment/closure with commits. Input changes use the input lock; authority work stays under the host's
serialization gate. Views publish from the same committed root and are frame-safe reads. A view retained
by a closed port is its last observation; the port cannot submit or inspect through a later connection
for the same actor. The host owns authority disposal, independently of any player's lifetime.

## Readiness and scheduling

A new connection starts unready. Its client acknowledges readiness only after applying a playable
view. Any ready port permits the single host cutoff; zero ready ports pause ordinary world selection.
Already accepted physical consequence work keeps its existing completion policy. Connection alone,
reopening or missed wall time earns no simulation time. Readiness loss clears only that port's movement.
The existing 150 ms movement lease and 50 ms host deadline apply independently to each input stream.
All ready, fresh movement samples join the same advancing batch, so more clients do not multiply time.

`ReadRunState` describes the calling port's readiness and the branch's current hold. A paused port
does not imply a paused shared world when another port is ready. A selected commit may finish after
readiness changes. The authority's required decisions, reading holds, care and retained consequences
continue to determine which kind of boundary can advance; UI state never acknowledges another actor's
choice or substitutes for an unavailable required human.

Ordinary human requests use stable rotating actor-order service, bounded to one outstanding request
per port and one selected human request per ordinary cutoff. This gives every waiting ready port a
turn without promising simultaneous crafting or capacity reservation. Complete authority validation
still decides competing resources and exact captured revisions. Duplicate command identities across
waiting ports are refused before queueing; exact retries of a committed command retrieve its receipt.

Simultaneously queued combat choices share one decision batch and the same captured timing basis.
The authority orders and validates them; the host never recaptures their terms. An actor that is not
an eligible due participant, already has its decision, or has a retained retreat cannot queue another
combat choice. This prevents observer/repeated-choice rejection traffic from consuming resolution
cutoffs. A stale timing token from an otherwise eligible caller still receives the ordinary durable
rejection; required-player Wait is never replaced by an automatic timeout.

Supported lifecycle and care selections reach the authority unchanged. Unknown or historical episode
IDs are adjudicated there; host admission does not infer historical permission from only the latest
outcome record. A rejected selected request does not buy another pause: the already-due host cutoff
continues once from the new root, without retrying or recapturing that command. It may stage an
encounter, resolve queued combat, complete an already-read outcome or select ordinary exploration
under normal holds. A refused exploration request does not clear another player's movement lease.

A ready patient's already-queued care choice for the actual current Outcome takes priority over
other player traffic and completion. It names that patient's admitted agreement and exact current
episode. A stale revision or action hash is rejected without silently refreshing it; the patient's
genuine choice window remains until another cutoff, including when the patient has already read the
outcome. A request for a different episode or from an unrelated observer has no such hold. Once care
is accepted, its original patient can receive one separate stale-offer refusal before treatment: the
request must match the retained agreement and episode, and the world revision must still equal the
acknowledgment's recorded sequence. That published receipt advances the revision; subsequent cutoffs
prioritize treatment over waiting requests, including after reconnect or reload. A failed preflight
publishes no receipt and cannot defer the helper. With no matching request, treatment proceeds at its
normal later cutoff. The legacy initiator's premature release preserves its tested
Confrontation/Staging boundary only for that actor and episode, not for other players' rejected traffic.

Hostile opportunities are selected from admitted world residents and the scenario catalog, not live
ports. Closing a connection does not make its resident immune to an eligible declared threat while
other players keep the world active. NPC decision rules remain subject to all world holds and costs.

## Queries, closure and recovery

One shared [trading controller](../../src/OpenFantasy.Hosting/Simulation/LocalTradingController.cs)
keeps merchant/shopping scheduling single-owned. Inquiry publication is indexed by shopper identity;
ports can read/cancel only their own inquiry. Merchant answers keep their existing FIFO policy and
finite stock. Each port's explicit quote/proposal inspection uses its own authenticated actor and its
own bounded slot. Full history verification remains a host operation. The [network query adapter](player-queries.md)
exposes the same operations with one independent in-flight query per authenticated connection,
bounded typed results and explicit request/scope matching.

Closing a port clears its movement, cancels its uncommitted waiter and transient inquiry, and waits
for its admitted inspections. It neither disposes the authority nor cancels another port's input.
Durably reserved work remains world-owned and completes under its normal rule even if its waiter
leaves. A new authenticated port for the actor starts unready and gets a fresh projection. Published
receipts remain retrievable after reopening; connection closure grants no item, heal or lost time.

[Player admission](player-admission.md) attaches credential-authenticated transports through the same
connection gate, additionally checking actual world/profile/admission pins there. It creates no new
control assignment and does not replace an occupied local port.

In-process closure is distinct from a confirmed transport disconnect. The full design requires a
journaled disconnect decision, the stated reconnect allowance, permitted controller handoff and safe
reconnection. Those require the network/control adapter and are not inferred from disposing this
port. This shared host supports coordinated branch Wait; independent encounter pockets, network
transport and native multi-client delivery are separate bindings of the full design. The connection
limit is the first production player target, not an NPC population or world-resident limit.
