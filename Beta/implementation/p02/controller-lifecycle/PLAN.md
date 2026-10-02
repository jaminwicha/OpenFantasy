# Durable controller lifecycle and full disconnect integration

Source checkpoint: `codex/checkpoint-before-controller-lifecycle-20261002T134420Z`,
HEAD `5e4a0e47a64ba0adae1bff768f98f4b09e1879f5`. Relevant source was clean;
662 older test/fixture files, 23 frozen specs and unrelated predecessor staging are pinned.
The prior turn answered the owner's economics question; implementation resumes here.

The complete target remains TDD section 93: confirmed transport disconnect, journaled 30-second
host-wall allowance, permitted co-player or conservative survival control after expiry, safe
reconnection, unlimited connected Wait, deterministic replay. This packet does not close that target
until the actual transport, timer, scheduling and native journeys prove it.

## Durable dependency

Introduce explicit fresh world profile 36 and storage schema 9, preserving all earlier profiles and
schemas. A fixed human roster has its own small controller facet; resident identity and permanent
inventory control entitlements remain unchanged. Disconnected/AllowanceExpired/Reconnected are
host-authenticated external events, not player commands or elapsed simulation time. One existing
world transaction records event bytes, new state, identity reservation and history. Replay applies
those bytes without consulting a clock. A safe restore cannot erase accepted choices, retreat or care.

Qualification covers finite transition/codec law, complete-world binding, actual journal reopening,
faults around commit, no gameplay changes, stale events, reading holds, queued paid combat actions,
archive rollover and older compatibility. Native/network policy is a separate dependent proof.

## Required host binding

The read-only host investigation found these hooks; implement with the same authority and scheduler:

- Classify termination in `PlayerCommandSession.RunAsync`. Explicit EOF/Goodbye and established
  transport failures differ from local disposal, failed Welcome, observer failure and server shutdown.
  Preserve `RequestAborted` versus server cancellation; checking completed host state alone races.
- Add explicit confirmed-disconnect handling separate from ordinary channel/port `DisposeAsync`.
  Under `LocalWorldSession._serialization`, require reference equality with the current port, commit
  the external event, clear/remove its transient input, then publish. A late callback must never close
  its replacement. A failed durable event stops the host rather than leaving transient-only authority.
- Process due external decisions in `TickAsync` before its zero-ready early return. Use the existing
  injected TimeProvider, retain an exact episode ID, and record observed elapsed milliseconds. No
  disconnect timeout for a connected idle port. Timer expiry earns no gameplay time.
- Authenticate a rejoin against permanent entitlement; attach provisionally unready. After successful
  Welcome and final credential validation, serialize restoration when safe and refresh observation.
  Failed handshake/disposal cannot restore control. No network sends under world serialization.
- Safe restoration retains committed decisions/costs, pending care and retreat. Introduce a distinct
  conservative player-side selector using ordinary paid legal actions; the general NPC attack selector
  is unsuitable. Preserve reading/consent semantics and specify permitted co-player policy explicitly.
- Define a recorded restart policy for outstanding allowance. Monotonic timestamps do not survive a
  restart. Do not silently restart grace, infer offline simulation or consult UTC during replay.

Lock order: world `_serialization`, briefly `_inputs`, authority's own gate. Admission releases its
own gate before awaiting attachment. Join inspections outside serialization. Failed durable writes
and cleanup exceptions must preserve existing fail-closed recovery guarantees.

Native follow-up requires exact confirmed disconnect, before/at/after30s tests with zero ready ports,
replacement-channel races, accepted action/receipt retention, reconnect-before-expiry and after-handoff,
conservative survival legality and same-history recovery. Distinguish these from manual event injection.
No production operator selects the new profile until those bindings are implemented and qualified.
