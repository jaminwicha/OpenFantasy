# Independent scheduling contract review

Review date: 9 September 2026. Reviewer: the history-contract worker, independently reviewing the scheduling worker's implementation. Outcome: **no blocking correctness findings in the bounded P00 scheduling contract**.

## Actual review scope

I read the nine scheduling application/codec files, three C# test/support files, independent Python oracle and retained JSON fixture listed below. The review covered:

- Explicit world/branch/calendar scope, including rejection of comparisons, selections and applications across independent clocks.
- Deterministic ordering by exact due instant, phase, work domain, actor and event identity; duplicate pending event identities reject even when their time or continuation differs.
- Selection of one declared phase at an inclusive cutoff, preserving older due work in other phases and all future work. The owner still must enforce global phase ordering.
- Immutable copied snapshots and selections; no caller-owned array/list or input/output codec buffer can mutate a retained snapshot.
- Candidate replacement binding to scope, revision and canonical snapshot hash, including different content under the same revision. A changed snapshot rejects stale selections; an empty selection is a no-op; revision overflow cannot mutate the input.
- Bounded enumeration (1,024 retained entries per snapshot; 256 selected items per operation), backlog retention and explicit rejection of excess input. No world work is silently truncated.
- Canonical framing, contract versions, fixed primitive widths, normalized milliseconds, allowed phases, strict ordering/unique identities, nested and outer trailing-data checks, and byte/count bounds before allocation from untrusted field lengths.
- Tests for insertion-order permutations, snapshots restored from bytes, backlog batches, scope mismatches, defensive copies, duplicate identities, invalid budgets/phases, revision exhaustion, every truncated fixture prefix and independent malformed vectors.

The optional review note about a redundant self-equality assertion in the backlog test was addressed by the owner before the hashes below were captured. It did not affect runtime behavior. No scheduling source edits were made by this reviewer.

## Executed evidence and limits

I executed the Python standard-library oracle and compared its stdout **byte for byte** with the retained fixture: equal. The oracle imports no application code. It defines valid snapshot, selection, replacement and empty-queue expectations plus 20 malformed snapshot vectors. The reviewed files had identical hashes before and after that execution.

I did **not** run a .NET build, C# test suite, Godot session, durable scheduler or actual owner work. The coordinator owns final integrated build/test evidence. Reading the tests is not a claim that those tests passed at these hashes.

This contract proposes a queue replacement; it does not execute work or durably commit owner effects with queue acknowledgement. It does not enforce the complete host step order, merge clocks, prove fairness, qualify full-world scheduling capacity or implement paged persistence. The 1,024-entry limit bounds one retained snapshot operation and cannot serve as a production cap on total world work. Work execution, transactional acknowledgement, capacity expansion and global phase enforcement remain explicit integration work.

The scheduling contract document was still being finalized and is outside this hash scope. The evidence below covers only the named code, tests and oracle artifacts.

## Exact reviewed files

Paths are relative to `Runtime/`. SHA-256 values cover the complete file bytes.

| File | SHA-256 |
| --- | --- |
| `src/OpenFantasy.Core/Scheduling/CalendarClockCoordinate.cs` | `e4a720723256cc911f4dcb26c57db4f4bd952bacd0c0280bfdfd3865d9c4eaae` |
| `src/OpenFantasy.Core/Scheduling/CalendarQueueCodecV1.cs` | `81fed23698dfb77c914bc39336eec39e74570b7d36e620095897acb50437a9d6` |
| `src/OpenFantasy.Core/Scheduling/CalendarQueueScope.cs` | `9765665140c3860f4b00373aa0d42adc6ef244940646fb5b04cee1002127b82b` |
| `src/OpenFantasy.Core/Scheduling/CanonicalSchedulingReader.cs` | `559b254e431be821eb11b9ac0a84dbd4ec0ddd6b1c723d84f74f275f807d147b` |
| `src/OpenFantasy.Core/Scheduling/DueWorkQueueSnapshot.cs` | `b7ee309e06180929dcd51af3da7cec6f0bbcb6e0994e74f98e1be23924efbd35` |
| `src/OpenFantasy.Core/Scheduling/DueWorkSelection.cs` | `1b36a4d53a898e7ec2d0a3eb03f8490a370d1914f527bcc27145b337251f9f9c` |
| `src/OpenFantasy.Core/Scheduling/ScheduledWorkEntry.cs` | `728c0d261cb050a5f38a3c96eead165cbfab7d2ce92c00897b96172e6d74d2e8` |
| `src/OpenFantasy.Core/Scheduling/ScheduledWorkPhase.cs` | `9045560615529977517f4111397f5eacc7ee0622eef18aae2aab6b9d6a7a565f` |
| `src/OpenFantasy.Core/Scheduling/SchedulingFormatException.cs` | `1a7efb918436d7ab39f6e3c4761c582c66f858af1ff2647c6a6fc7bfddf66556` |
| `tests/OpenFantasy.Core.Tests/Fixtures/generate_scheduling_vectors.py` | `316e6a5c03f8f527b01c7d8268db6d77f87b101953293c9c299b3dc99302ceb1` |
| `tests/OpenFantasy.Core.Tests/Fixtures/scheduling-v1.json` | `50a21cc1b7ebbe94eb3f6a9b6f825251a3d2652c0031a277271d9b5449a2eb1b` |
| `tests/OpenFantasy.Core.Tests/Scheduling/CalendarQueueCodecTests.cs` | `3065a24b8542ef91154ea75d0add818c529b86395c679931331407f8ced14b7f` |
| `tests/OpenFantasy.Core.Tests/Scheduling/DueWorkQueueTests.cs` | `626a803bb0877bb994b4274033f4b6ed154f6d69c2c52e2c3e888104c23faf95` |
| `tests/OpenFantasy.Core.Tests/Scheduling/SchedulingFixture.cs` | `91758d7511d477db3d11564899c4e7d56059877f42ec1ac935c8e88c180fddc6` |

Reviewed set: 14 files. Aggregate SHA-256: `fc116bd17c0db715cc48cc05872bc0198295a9f64fdaba3aedbaa46fe877ce2e`.

The aggregate hashes the UTF-8 concatenation of each sorted runtime-relative path, a NUL byte, its lowercase hexadecimal SHA-256, and a newline. It identifies this exact review scope; later changed files require an appropriate review refresh.
