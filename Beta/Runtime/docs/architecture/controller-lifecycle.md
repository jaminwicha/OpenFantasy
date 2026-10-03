# Durable character control

Character ownership and temporary control assignment are distinct facts. A player's resident identity,
body, skills, possessions and original control entitlement remain owned by their existing facets.
[WorldControllerState](../../src/OpenFantasy.Core/Worlds/Controllers/WorldControllerState.cs) owns a
fixed roster of one to four human actors, each with an independent control revision, mode and optional
disconnect episode. It contains no socket, credential, timer or simulation clock.

The modes are Human, ReconnectAllowance and Autonomous. A confirmed Disconnected event requires Human
and records its unique commit cause as the disconnect episode. The allowance preserves required human
choices. AllowanceExpired names the same episode and control revision and requires a recorded host
elapsed duration of at least 30,000 milliseconds. Reconnected names that episode and restores Human
only at a permitted world boundary. Every accepted transition increments one actor's control revision.
Stale events, duplicate episodes and invalid transitions are refused without publishing a new root.

The elapsed duration is trusted host evidence, not proof of real elapsed time from a player and not
world exposure. The Core verifies its threshold and transition, while the live host must authenticate
the connection and measure the duration. Decoding a valid state proves structural consistency only;
replaying the authoritative journal authenticates its history. Reopening never automatically starts a
new allowance, expires one or returns control.

## World and storage boundary

[WorldControllerAdmission](../../src/OpenFantasy.Core/Worlds/WorldControllerAdmission.cs) explicitly
selects fresh profile 36 over an unplayed outcome-capable world. The admission hash binds the original
world and controller rules. Its roster must equal the human residents, each with one permanent control
entitlement. The sum of actor control revisions cannot exceed the world journal revision.
The encounter lifecycle's boundary revision remains exactly equal to the world revision, including
external events. Its retained action archive, phase, readiness debts and clocks do not change. Disconnect
identities participate in the world identity index and cannot alias entities or another admitted cause.
A lived save is never silently reinterpreted.

[WorldControllerProcessor](../../src/OpenFantasy.Core/Worlds/WorldControllerProcessor.cs) handles an
isolated external event batch with no movement, actor commands or receipts. It preserves every gameplay
owner's gameplay contents and advances the world/lifecycle bookkeeping revisions and controller facet. It can record a disconnect
while reading or while a gameplay obligation exists; doing so cannot release that obligation or trigger
an archive cut. Ordinary gameplay and coordinated archival copies retain the controller facet.

[WorldControllerBoundaries](../../src/OpenFantasy.Core/Worlds/WorldControllerBoundaries.cs) prevents
restoration during legacy pending care or excavation, confrontation/staging, the actor's committed encounter
choice or committed retreat. A reading hold is a stable boundary and can retain its dialogue while
control returns. Observing a safe boundary is neither authentication nor permission to publish an event.

[WorldSnapshotCodecV36](../../src/OpenFantasy.Core/Worlds/WorldSnapshotCodecV36.cs) adds one bounded
canonical controller field to the complete root. Its shared outcome-family encoder retains the exact
profile 35 fields and bytes for profile 35. Each decoder requires its explicit version, field count,
canonical order and limits. Controller states are bounded at 4 KiB; events at 512 bytes. The full retained
world and journal envelopes are unchanged.

[WorldDatabaseSchemaNine](../../src/OpenFantasy.Hosting/Worlds/WorldDatabaseSchemaNine.cs) admits the
new event kind while retaining the coordinated material/care archive layout. Previous storage schemas
keep their exact accepted kinds. [SqliteWorldAuthority](../../src/OpenFantasy.Hosting/Worlds/SqliteWorldAuthority.cs)
publishes event, state, cause reservation and journal row in its existing transaction. Recovery reproduces
the state by ordinary deterministic Core replay; no second writer or session-state database is involved.

## Live session binding

[LocalWorldSession.Controllers](../../src/OpenFantasy.Hosting/Simulation/LocalWorldSession.Controllers.cs)
serializes confirmed transport loss, allowance expiry and restoration with ordinary world commits.
Only the same current, established port may record loss. The journal transition precedes transient
port removal; a failed write stops the host. Late callbacks from an older port cannot detach its
replacement, restart its allowance or cancel its inquiry. Permanent actor entitlements are unchanged.

Each observed disconnect owns its unique episode ID and a monotonic host timestamp. The host checks
external events before its zero-ready gameplay return, recording elapsed whole milliseconds only when
at least 30 seconds have passed. Expiry neither selects an action nor earns simulation time. A connected
idle actor has no timer. Episode comparison prevents an old timer from expiring a newer disconnect.
Ordinary local disposal, unsuccessful admission, server shutdown and host failure do not start timers.

After successful Welcome and final admission checks, a returning port requests restoration. At an
unsafe boundary it remains provisionally attached and unable to make new choices or trade inquiries;
exact committed receipts and participant observations remain readable. The host retries the boundary
check at its existing cutoff, without extending the handshake deadline or erasing accepted work.
A sole returning player does not deadlock on already accepted work: the host may complete retained
care, staging, the actor's committed combat choice or retreat while that port awaits restoration. This
exception cannot choose a new human action or start ordinary exploration; other required human
decisions still hold the world. At a safe boundary it journals restoration and publishes the resulting
view before permitting new input. Early readiness requests are discarded. Restoration leaves the port unready, with no movement;
the client must explicitly Resume after applying the playable view. Reconnecting does not
supply consent for care, acknowledge reading or assign a co-player.

[PlayerControlStatus](../../src/OpenFantasy.Hosting/Simulation/PlayerControlStatus.cs) describes the
transient port boundary separately from durable assignment: Available, Pending or Restored. The port
has its own identity and return generation, while resident identity and canonical world bytes stay
with their existing owners. A pending return becomes Restored only after the world publishes its
safe boundary. A coherent port read captures the status beside its published view. Local direct Resume
checks the captured actor, port and generation under the input lock; network Resume additionally binds
the applied observation stream and restoration revision. An old pending request never becomes consent.
Accepted-work continuation depends on authority availability, not whether a restored player has resumed.

The durable event API is host-only. No player wire request supplies an external event or elapsed time.
Older profiles have no controller facet and retain ordinary connection closure without new journal rows.

## Conservative encounter selection

[LocalAutonomousEncounterController](../../src/OpenFantasy.Hosting/Simulation/LocalAutonomousEncounterController.cs)
selects choices only for due required-human actors whose durable assignment is Autonomous. It retains
their resident identity, timing role, original principal/session entitlement, body and possessions.
It never selects for Human or ReconnectAllowance. The host processes safe restoration before encounter
selection and requires at least one gameplay-ready port for fresh autonomous choices. The exception
for completing a returning player's accepted work cannot authorize new autonomous choices.

The selector first seeks a legal retreat through the ordinary route advisor and complete queue
eligibility checks. It otherwise queues ordinary Wait, which incurs the normal positive readiness
cost on resolution and confers no invented defensive benefit. Unsupported party retreat therefore
does not abandon companions. The ordinary authority still adjudicates each captured command; route
steps pay their normal costs, preserve custody and may be interrupted by changing world conditions.

Existing accepted decisions and standing retreat routes are preserved. Actors with another command
already selected at the same cutoff receive no second choice. Candidates are ordered by actor identity;
an already selected player or NPC retreat reserves the single route slot before autonomous selection.
Commands and receipts use the ordinary journal and deterministic replay. Selection is host policy,
not a second world authority or a change to canonical Core state.

The captured-choice projection treats pending autonomous choices like other pending host selections:
it withholds executable offers until those publications settle, because they change the shared timing
revision. No other actor's command or escape route is disclosed. Human choices still hold indefinitely.

The encounter selector submits neither attacks nor aftermath commands. Its companion aftermath policy
below retains distinct permission, acceptance, treatment and completion facts.

## Autonomous aftermath

[LocalAutonomousAftermathController](../../src/OpenFantasy.Hosting/Simulation/LocalAutonomousAftermathController.cs)
operates only at an outcome for unacknowledged Autonomous required participants, without reading,
excavation or care obligations already holding the boundary. Safe human restoration precedes selection.
[LocalWorldSession.Aftermath](../../src/OpenFantasy.Hosting/Simulation/LocalWorldSession.Aftermath.cs)
requires a gameplay-ready port; the returning-player accepted-work exception grants no new choice.
Queued human patient-care requests and pending helper treatment retain their earlier priority.

An incapacitated character can request a currently legal treatment under an existing care agreement.
The helper must be host controlled. Exact-defeat admission checks the patient's captured episode and
archive, prior permission, capable neutral helper, reach, readiness and an actual owned dose. Selection
creates no agreement or supplies. The patient's acknowledgment and the helper's revalidated, finite
treatment occupy separate cutoffs and receipts. A pending obligation cannot be discarded or reassigned
by this policy. Treatment resumes the same episode and clears its obsolete outcome acknowledgments.

Care candidates are ordered by actor and agreement identity. Stable selection identities bind the
world, branch, patient, agreement and exact incapacitating command. Occupied selection identities are
checked across that patient's agreements before present eligibility, so a later unavailable helper
cannot silently trigger a retry or a switch to another agreement. A different actual defeat permits a
new selection. A previously accepted outcome declines automatic care; a human can still explicitly
request care under the unchanged authority law before finalization.

Living Autonomous participants may acknowledge the exact outcome using their original entitlement.
Under the explicit [general recovery law](general-recovery.md), incapacitated participants can also
acknowledge: completion preserves their separate ordinary treatment eligibility, without promising
rescue or changing body state. Without that law the selector retains zero-HP outcomes for human disposition.
Those stable command identities bind the world, branch, actor, episode and action-archive hash.
Remaining Human or ReconnectAllowance participants still hold. The existing actionless completion
boundary remains separate and grants no time, health, reward or movement. Another player's queued
request keeps its own command and waiter; an automatic command never supplies that player's receipt.

In the legacy controller interpretation an incapacitated character without eligible automatic care
remains at the outcome for human disposition.
Accepting would close aftermath care permanently under this interpretation; that is not a conservative
survival choice. Held outcomes also prohibit ordinary helper movement, replenishment and tactical
recovery time. A general rescue/recovery law is a separate domain requirement, not permission for this
selector to invent supplies, expire obligations or automatically choose a terminal defeat. The explicit
recovery law permits later ordinary assistance; unmatched aftermath obligations still hold completion.

Controller-world outcome presentation reports recorded character acceptance, never proof of personal
reading or who submitted a historical request. Its presentation mode depends on the controller facet's
presence, so reconnecting to Human does not rewrite the meaning of prior acknowledgments. Care wording
likewise states that care was requested rather than attributing it to the returning person.

## Policy and presentation obligations

Human assignment is not evidence of a connected socket. Loading a saved allowance does not recreate
its process-local timestamp, infer offline elapsed time or automatically expire it. The explicit
[allowance recovery policy](controller-recovery.md) preserves durably measured lower bounds under
storage schema ten and starts a new fenced monotonic segment only when a live host attaches.
Schema nine retains its original behavior and requires explicit copy-and-activate migration to select
that policy. The default simulation still pauses when no gameplay-ready players are connected.

Autonomous assignment permits the conservative encounter selector above; expiry itself chooses none.
Aftermath behavior follows the bounded policy above; general rescue and other survival behavior need
their own domain laws. A co-player requires explicit
[permission](control-permissions.md) and effective-control authorization, not mere presence in the
world. A permission offer or acceptance changes no controller assignment. The [player client](player-client.md)
distinguishes pending control from ordinary pause and binds explicit Resume to the restored observation.
Returning network ports reject unbound readiness even after their first accepted Resume; subsequent
pause/focus changes retain the same connection proof. This input contract is distinct from restart,
co-player policy, external-event recording and encounter selection.

Under the explicit [care-disposition law](care-dispositions.md), pending care alone permits restoration
for its patient or named helper. All other accepted-work restrictions remain. Control return leaves
the request and HP unchanged and starts the returning port unready, so cancellation/decline requires
a fresh deliberate choice.
