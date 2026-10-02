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
restoration during pending care or excavation, confrontation/staging, the actor's committed encounter
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
the client must acknowledge readiness again after applying the playable view. Reconnecting does not
supply consent for care, acknowledge reading or assign a co-player.

The durable event API is host-only. No player wire request supplies an external event or elapsed time.
Older profiles have no controller facet and retain ordinary connection closure without new journal rows.

## Policy and presentation obligations

Human assignment is not evidence of a connected socket. Loading a saved allowance does not recreate
its process-local timestamp, infer offline elapsed time or automatically expire it. A restart policy
must explicitly reconcile outstanding allowance before a production host can rely on automatic handoff.
The default simulation still pauses when no gameplay-ready players are connected.

Autonomous assignment is permission for a separately qualified conservative selector to submit legal
survival actions through ordinary admission; expiry itself chooses none. A co-player requires explicit
policy and authorization, not mere presence in the world. Client presentation must distinguish a
pending control request from ordinary pause and bind its fresh readiness acknowledgement to the
restored observation. The boolean readiness wire contract alone does not prove that acknowledgement.
These policy, selector and client contracts are distinct from the live external-event binding.
