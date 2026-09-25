# Encounter timing and exposure

## Portable domain owner

[EncounterTimingSnapshot](../../src/OpenFantasy.Core/Encounters/EncounterTimingSnapshot.cs) represents
one bounded tactical clock. Its [scope](../../src/OpenFantasy.Core/Encounters/EncounterTimingScope.cs)
names a world, history branch and encounter with distinct identities. It owns retained actor readiness
and pending decision intents. It does not own participant admission, control grants, actor health,
resources, effects, positions or world calendar publication.

Each [actor record](../../src/OpenFantasy.Core/Encounters/EncounterActorTiming.cs) retains its identity,
positive normalized speed, remaining delay and required-human/automatic decision policy. Remaining
delay is incurred debt, not a recomputed absolute calendar deadline. Importing a record, restoring an
archive or changing an external controller cannot implicitly reset that debt.

| Operation | Preconditions and result | Experienced duration |
| --- | --- | --- |
| `QueueDecision` | Exact expected revision; uniquely named due actor and pending decision identity; pins an external action definition/payload | Zero |
| `TryAdvance` | No due actor; increments revision and elapsed units, subtracts five from delays with zero saturation | Five tactical units, 50 ms |
| `TryConfirmNext` | Every due required-human actor has a retained choice; first due actor in identity order also has a choice | Zero |
| Blocked request | Returns the identical snapshot and explicit missing-choice/action-boundary status | Zero |

Confirmation removes that actor's pending choice and charges `ceil(10000 / normalizedSpeed)` units.
Both ordinary action and explicit Wait choices incur positive debt. All due actors must resolve their
boundaries before another tick advances. A missing automatic choice blocks the first due automatic
actor; it cannot be skipped to reach a later actor. A ready human choice blocks automatic confirmation
even when the automatic actor sorts earlier. The host decides whether a proposal is accepted and
atomically publishes its consequences. Confirmation is never an HP, material or permission grant.

Every accepted queue, confirmation or tick increments the timing revision. Elapsed units contain only
whole five-unit ticks and cannot exceed five times the revision. Exact expected revisions reject stale
requests. Arithmetic exhaustion throws before publishing a new immutable snapshot. The component has
no wall-clock input, timeout or accumulated missed-step queue.

## Bounds and canonical recovery

A clock contains 1–128 complete actors and at most one pending decision per due actor. Actor identities
cannot alias scope identities. Pending decision identities are unique and cannot alias any actor or
scope identity. Previously consumed decision IDs require the outer world's receipt/history ledger;
the bounded pending set is not an unlimited deduplication journal. The 128-actor limit rejects excessive
input before unbounded enumeration. It does not implement the design's complete shared-pause fallback.

[EncounterTimingCodecV1](../../src/OpenFantasy.Core/Encounters/EncounterTimingCodecV1.cs) uses the
canonical OFC version-one frame, big-endian unsigned numbers and exact identity/hash widths. Decode
requires an independently supplied expected scope. Its maximum archive is 16 KiB.

| Frame | Ordered fields after the tag |
| --- | --- |
| `encounter.timing.v1` | Format version 1; world16; branch16; encounter16; revision64; elapsed-units 64; actors frame; decisions frame |
| `encounter.actors.v1` | One packed field: rows of actor16, normalized-speed64, remaining-delay64, control64 |
| `encounter.decisions.v1` | One packed field: rows of actor16, decision16, kind64, action-pin32 |

The numeric suffixes denote bits for numbers and bytes for identity/hash fields. Actor rows are 40 bytes;
decision rows are 72 bytes. Both row collections sort by actor identity. Controls are required-human 1
and automatic 2; kinds are Wait 1 and Action 2. Normalized speed must fit a positive 32-bit integer despite
its 64-bit wire field. Unknown values, duplicates, invalid relationships, partial rows, noncanonical order,
wrong scope, trailing content and oversized frames reject. Decode re-encodes to verify exact canonical
bytes. Pending choices and nonzero debt survive recovery unchanged.

## Exposure binding

[EncounterExposureBinding](../../src/OpenFantasy.Core/Encounters/EncounterExposureBinding.cs) associates
an existing [ExposureProgress](../../src/OpenFantasy.Core/Exposure/ExposureProgress.cs) with one clock.
`Begin` retains that subject's prior duration, output and fractional remainder; it starts at the supplied
tactical boundary and grants no exposure from earlier encounter time. The subject may be a coupled
process rather than a participant actor; membership admission belongs to the world owner.

`ObserveAcceptedTiming` requires the same scope, monotonic revision/units, and an unchanged hash for a
repeated revision. It rejects a duration increase larger than five units per intervening revision.
Exact repeated snapshots return the same binding. Later zero-duration choices update the observed pin
without changing progress. Increasing cumulative tactical units contribute 10 ms per unit through the
existing integer rate engine. Observing several accepted ticks at once yields the same output/carry as
observing each one. No work, liquid transfer, healing, damage or other effect is issued by this arithmetic.

The caller must authenticate all intervening timing history before observing it. These shape and hash
checks cannot prove ancestry: a fabricated later snapshot can be arithmetically plausible despite an
earlier required-choice hold. Likewise a plausible encoded binding origin is not proof of original
admission. The world owns the original binding/subject, accepted intervals, deduplication and atomic
publication. Neither canonical decoding nor content-hash equality replaces those checks.

[EncounterExposureCodecV1](../../src/OpenFantasy.Core/Encounters/EncounterExposureCodecV1.cs) retains at
most 1 KiB using tag `encounter.exposure.v1`, followed by format version 1, scope identities, bound-at
tactical units, initial exposure days/milliseconds, observed revision/units, observed timing hash and
the existing `exposure.progress.v1` frame. Restore requires the exact expected current clock, subject
and rate. Bound-at units must be a whole tick no later than observed units. The retained duration must
equal initial exposure plus elapsed units since binding; `ExposureProgress.Restore` independently
recomputes completed output and normalized remainder. Wide integer arithmetic preserves large clocks
and rejects capacity exhaustion without mutating the prior state.

## Application and diagnostic boundaries

[EncounterTimingOperator](../../src/OpenFantasy.Host/EncounterTimingOperator.cs) exposes
`--inspect-encounter-timing <timing.bin> <world-id> <branch-id> <encounter-id>`. It bounds the input before
allocation, validates canonical state, and describes the independent next-tick and next-confirmation
proposals with exact archive bytes/hashes. These are alternative proposals from the same initial state,
not two successive operations. Counter exhaustion reports an unavailable proposal. Inspection never
writes the input, opens a world database or publishes consequences. Caller-supplied scope and action
pins are explicitly unauthenticated.

The [local world pacing binding](time-and-encounters.md#runtime-binding-local-session-pacing) retains
its exploration 1800 ms interval and dialogue hold semantics. Routing tactical 50 ms ticks through that
ordinary advancing path would mischarge time and process allowances. A live encounter world binding
requires versioned interval ownership for residents, work, regrowth, liquids and effects, retained
fractional schedules, complete identity admission, authenticated commands and atomic history. A hold's
wall-clock duration cannot be replayed as catch-up work on release.

The [Wait design contract](time-and-encounters.md#design-contract-wait-encounters) owns encounter stages,
legal staging, complete dependency closure, controller handoff, joining/merging, coordinated versus
independent co-op, full shared-pause continuation and safe release. A timing component does not establish
those contracts or executable damage/lifecycle rules. In-place camera, choreography, animation and audio
present accepted world state through the ordinary observation boundary.
