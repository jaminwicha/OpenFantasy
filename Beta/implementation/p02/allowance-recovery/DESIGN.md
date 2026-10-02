# Explicit recovery of measured disconnect allowance

Previous goal turn: progress. Commit `2950c00c` and its milestone qualified observed control return.
This unit addresses the next recorded dependency: outstanding allowance after host restart.
The before-change checkpoint and exact preserved files are in `PRESERVATION.json`.

## Decision and scope

Keep Core profiles 36–38 and their canonical bytes unchanged. The existing rule accepts an external
host-observed duration; it does not require gameplay to store process timestamps. Add explicit storage
schema 10 for durable operational observations under the same exclusive SQLite authority and gate.
This introduces no new writer, world clock or gameplay controller.

Each outstanding allowance has exactly one row bound to actor, control revision and disconnect episode.
It stores a measured lower bound. The live host starts a fenced monotonic segment from that subtotal;
each observation is the fixed subtotal plus elapsed time since the segment started, capped at the
30-second threshold. Never add successive rounded deltas. Opening, replay and inspection are pure;
only attaching a live session starts a segment. Downtime, UTC and uncommitted crash time earn nothing.
The crash tail is unknowable, may exceed the nominal cutoff interval under stalls, and repeated crashes
can extend effective grace. Do not claim exact preservation of every elapsed millisecond.

Disconnected inserts its zero row in the same transaction as its canonical event. Reconnected and
AllowanceExpired remove the exact row in that transaction. Schema 10 expiry requires sufficient durable
evidence first. Independent checkpoint transactions publish no world revision, receipt, time or action.
Missing, orphaned, mismatched, decreasing and partial evidence is refused. Write failures poison the
authority; stale segment generations cannot checkpoint after another attachment.

Graceful stop captures its timestamp before cancellation, joins the loop, checkpoints under serialization
and then joins inspections/disposes. Inspection cleanup does not consume grace. Ordinary shutdown still
creates no disconnect for saved Human assignments. Saved Autonomous remains autonomous; neither that mode
nor empty-world reopening grants gameplay time or a new choice.

Fresh creation explicitly selects recovery. Upgrade is create-only into a new database and retains the
original genesis and complete historical rows. Its immutable activation provenance names the original
source root and each outstanding episode with UnknownPriorElapsed/zero lower-bound evidence. This is an
explicit selection of recovery for a save lacking prior measurements, not an automatic reset on open.
Recovery validates that provenance against the actual replayed source root. Schema 9 remains unchanged.

## Ownership and proof

The storage worker owns schema/authority/migration and new low-level tests. The host-journey worker owns
new real timing/reopen tests. The lead owns session binding, operator commands, external process kills,
architecture and integration. A read-only critic challenges atomicity, provenance, stale segments,
shutdown races and rule fidelity. No provider request follows the recorded account spending limit;
these native workers use Astra capacity.

Focused tests cover actual established transport loss, partial measured grace, fresh clock on restart,
large UTC/downtime changes, before/at threshold, zero-ready expiry, held paid work, reconnect and replay.
Storage tests cover fresh/upgrade origin, exact earlier history, malformed/corrupt/stale evidence,
all event/row transaction boundaries and fail-closed recovery. External Host kills cover checkpoint and
disconnect/expiry/rejoin on both sides of commit. Run relevant existing controller/storage/network/session
regressions; preserve old tests, frozen specs and all deadlines. Native handback follows if dependencies
and fixtures support the same bounded proof. Full phase/campaign and physical/human gates remain open.
