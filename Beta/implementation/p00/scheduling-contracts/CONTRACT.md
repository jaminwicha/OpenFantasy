# Scoped calendar due-work contracts

This P00 increment implements a small retained calendar queue and immutable selection/application candidates from [technical design §§3–5](../../../spec-v1_1/TECHNICAL_DESIGN.md). It establishes exact scope, ordering, bounded work selection and restoration bytes. It does **not** implement the world scheduler loop, wall-time pacing, tactical readiness, exposure accumulation, Wait admission/holds, owner callbacks, gameplay effects, persistence or transaction commit.

## Clock scope and ordering

`CalendarQueueScope` names exactly one world, branch and calendar clock by their stable identities. `CalendarClockCoordinate` combines that scope with an exact `WorldInstant`. Coordinates from different scopes are incomparable; selection rejects a cutoff whose world, branch or clock differs. `WorldInstant` retains signed 64-bit days and normalized milliseconds, including negative epochs and the full supported eon-scale range.

The queue contains **calendar** due instants only. Tactical readiness and participant-exposure values require their own typed clocks/queues; converting or comparing their raw integers to these days/milliseconds is forbidden. This increment does not select globally earliest work across independent clocks. A clock registry must eventually bind each scope to its actual calendar, epoch, rules and accepted authority state; constructing identifiers does not verify those relationships.

`ScheduledWorkEntry` retains event identity, due instant, phase, work-domain identity, actor identity, work-item identity and continuation ordinal. These immutable pointers identify work for its future owner; no string callback, delegate, arbitrary code or live execution state is stored in the queue. `WorkDomainId` is the responsible work domain's deterministic tie-break identity and is distinct from `ClockId`. Actor/work-item identities require actual owner validation; the presence of an ID does not grant actor control or imply a body.

The total order **within this scoped queue** is:

```text
due calendar instant
then numeric phase priority
then canonical work-domain ID bytes
then canonical actor ID bytes
then canonical stable event ID bytes
```

The four admitted phase priorities are **StateTransition 10**, **Observation 30**, **Planning 40** and **FollowUp 50**. Priority 20 is reserved for accepted commands, which belong to the host's durable ingress sequence and cannot enter this queue. One event identity can occur only once in pending work, even if due date, phase or continuation differ. Work-item ID and continuation ordinal are consequential payload fields bound by snapshot hashing; they do not choose a winner ahead of the stable event ID.

## Selection and explicit candidate application

`DueWorkQueueSnapshot` copies input, validates unique event IDs and sorts all entries canonically. Subsequent changes to caller-owned collections cannot change it; exposed lists are read-only and their entry values are immutable. Construction accepts zero through **1,024** entries and rejects a longer source at its 1,025th element.

`SelectDue(scopedCutoff, phase, maximumCount)` accepts **one explicit phase** and a budget from 1 through **256**. It selects that phase's first eligible entries with `DueAt <= cutoff`, in the queue's total order. It removes nothing. Older work of another phase remains pending; a caller draining state transitions does not accidentally consume an overdue planning job before command admission.

The resulting `DueWorkSelection` binds the exact scope, base revision and full SHA-256 canonical snapshot hash; it retains cutoff, phase, budget and a read-only list of selected entries. `PendingCount` includes **every unselected entry**, including other phases and future dates. `HasMoreEligibleWork` means additional work exists for this same phase/cutoff beyond the selected budget. It is not a statement that the entire queue is empty or safe to skip.

`ApplySelection` validates the plan against the exact immutable base snapshot and returns a **candidate replacement** with only selected entries removed. All other entries, including overdue other-phase jobs and future work, retain their exact identities, due instants and continuations. A nonempty application advances queue revision by one using checked arithmetic. Revision exhaustion rejects without mutation. An empty selection returns the same snapshot and consumes no revision.

A selection prepared against a different scope, revision or canonical content rejects, including changed content under the same claimed revision. A byte-identically restored snapshot accepts the same selection and produces the same replacement. Because these are immutable values, applying the same plan to the same original value twice can produce two equal candidates; it does **not** establish globally exactly-once execution. Applying the original plan against its returned/new revision rejects. Only the authority's future transaction can pair one candidate with its real owner effects and prevent an old base from being accepted again.

Owner work must actually complete before acknowledgement. An interrupted or rejected owner operation keeps its prior snapshot and pending work. P01 must atomically commit queue replacement, owner effects, RNG/continuation changes, receipt and outbox. Selection/application does not execute these effects and cannot be published as durable success.

## Host dispatch and capacity obligations

The host must implement per-step **due state transitions/expiry → accepted commands in ingress order → observations → planning → follow-ups**. It can drain each selected phase explicitly, but a single local queue cannot enforce this across all owners/clocks. If a bounded transition batch leaves `HasMoreEligibleWork`, the host must continue under a qualified continuation/backpressure policy before declaring that prerequisite phase complete. It must not skip transitions, change priority based on worker wall-time completion or silently drop NPC work to maintain frame rate.

The **1,024-entry snapshot and 256-entry batch limits are qualification limits for this bounded operation**, not production world population or total pending-work caps. Exceeding capacity rejects; no input is truncated. The complete simulation still needs persisted continuation/paging or another explicitly qualified capacity profile, stable routing between queues and measurements of backlog/dispatch costs. A full world cannot be implemented by retaining only its first 1,024 jobs. The current constructors deliberately provide no silent spill, eviction or overwrite behavior.

## Canonical snapshot format

The codec reuses the existing primitive framing: `OFC` plus NUL, UInt32 big-endian framing version **1**, UInt32 big-endian field count including tag, then UInt32 length and bytes per field. Numeric fields below are eight bytes: signed two's-complement big-endian for days and unsigned big-endian for every other number. Each ID is exactly 16 canonical bytes. Tags are exact UTF-8 bytes with no normalization.

| Tag | Ordered fields after tag |
|---|---|
| `scheduler.calendar-queue` | contract version `1`; world ID; branch ID; clock ID; queue revision; zero or more complete calendar-work frames in canonical order |
| `scheduler.calendar-work` | contract version `1`; event ID; signed due day; normalized milliseconds within day; phase priority; work-domain ID; actor ID; work-item ID; continuation ordinal |

The outer byte limit is **262,144**, each work frame is at most **256 bytes**, and tags at most **64 bytes**. The outer field count is at most 1,030 (six fixed fields including tag, plus 1,024 entries). The writer returns fresh bytes. The decoder checks limits before field allocation/copying, requires already-sorted unique entries, and rejects unknown versions/tags/phases, unsupported command phase 20, invalid widths, non-normalized milliseconds, truncated fields, duplicate IDs, extra entry fields and trailing bytes. It never normalizes corrupt wire input or restores a partial queue.

`ComputeSnapshotHash` is full SHA-256 of all canonical snapshot bytes, including scope, revision and every entry's continuation. It is a content identity, not an authentication credential or proof that a snapshot was durably stored. A selection is an in-memory candidate and is not given a separate persisted format in this increment.

## Retained evidence and limits

[generate_scheduling_vectors.py](../../../Runtime/tests/OpenFantasy.Core.Tests/Fixtures/generate_scheduling_vectors.py) independently spells the format using Python standard-library `struct` and `hashlib` and prints JSON to stdout. Verification compares that output with the frozen [scheduling-v1.json](../../../Runtime/tests/OpenFantasy.Core.Tests/Fixtures/scheduling-v1.json); it never regenerates expected values in place during a test. The fixture covers exact queue/empty/replacement bytes, full hashes, a mixed-phase queue spanning minimum/maximum signed days, domain/actor/event ties, maximum continuation ordinal, a selected subset and **20 malformed frames**.

Retained C# tests cover all insertion rotations/reversals, exact oracle equality, all incomplete snapshot prefixes, malformed/oversized inputs, copied data, duplicate event IDs at different times, exact 1,024/1,025 capacity boundaries and early enumeration termination, wrong scopes, stale same-revision content, restoration continuation, phase isolation, empty/no-op selection and revision exhaustion. A backlog scenario explicitly selects **900 due entries in four bounded batches** while retaining **124 future entries** intact. The fixed-priority host loop, callbacks, global clocks, storage, Wait and gameplay effects are not covered by these structural tests.

The coordinator reported the full pinned Debug integration passing **149 tests with no skips**, including **20 Scheduling tests** and **13 History tests**, after these application files and fixtures stabilized. This is coordinator-executed integration evidence; the scheduling worker did not run a competing build. No unrun scheduler qualification or production throughput is marked as passing by this document. Remaining work includes real work-owner resolution, coupled phase dependencies, atomic completion/recovery, complete pending-work capacity and replay across multiple clocks under adopted Wait rules.
