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

## Live session contract

The [hosted connection boundary](hosted-player-connections.md) supplies authenticated ports and one
serialized host. Binding a transport to durable control requires explicit termination classification,
episode-bound host timers, provisional unready rejoin, stale-port exclusion and a conservative action
selector. Ordinary local disposal, failed admission and deliberate server shutdown are not confirmed
transport disconnects. A connected idle human retains unlimited Wait. Autonomous control authorizes
legal host-policy actions through ordinary action admission; the controller event itself chooses none.
A permitted co-player requires explicit policy and authorization, not mere presence in the same world.

The durable event API is a host authority surface, not a network request. Network timer measurement,
restart treatment, input suppression and survival selection are separate application bindings; the
existence of the Core facet does not imply those bindings. Owner restoration preserves accepted action
costs and debts and must refresh the reconnecting player's observation before new input.
