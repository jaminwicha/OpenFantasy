# Retained resource-history encoding optimization — Opus result

Status: implementation complete and locally verified (build, 104 focused cases, two complete
before/after replay measurements). Not native, deadline or production acceptance; Astra owns review,
broader regression, native gates and Git.

## Finding (investigated before code)

Every world encode (`WorldSnapshotCodecV25..V30`) calls `WorldResourceHistoryCodec.Encode`. For V2/V3
it built a structural twin `WorldResourceHistory` (re-running its constructor checks), then rebuilt the
whole inner V1 frame: basis `ProgressionCodecV1.Encode`, the actor history (which re-encodes and hashes
the basis again for `Basis.ContentHash`), a copy of every batch, every evidence item from scratch and
two or three enclosing frame copies. `WorldStepProcessor` carries the *same immutable* history object
into each validation, movement and final root of a step, and into later steps until the next resource
append. So the same object was fully re-serialized on most encodes: 2,409 encodes, but only 784
distinct histories are ever captured in the replay.

## Change (pure C#; no schema, rule, deadline, native or IPC change)

- `WorldResourceHistory.cs`: lazily owns its **structural V1 frame** (admission pin, basis sequence and
  progression, actor history, evidence; never an anchor). It is published with a volatile read and a
  compare-exchange. Concurrent first callers may duplicate the capture, but only one complete array is
  published. The frame is exposed only internally, as `ReadOnlyMemory<byte>`.
- `WorldResourceHistoryCodecV1.cs`: `Encode` keeps its anchored-value refusal and returns an
  independent copy. `EncodeStructure` is the original expression, unchanged. V1 decode compares the
  input against the owned frame.
- `WorldResourceHistoryCodec.cs`: V3 = frame(3, `EncodeBinding()`, owned frame); V2 = frame(2, archive
  reference, owned frame). No structural twin is built per encode. Decode and refusals are unchanged.
- The `ActorResourceHistory*.cs` files are unchanged: an owned frame there would be valid but not
  worth it. Each append creates a new actor history, so the copy would serve a single capture (784
  captures vs 789 actor encodes after the change) while doubling the retained bytes.
- Architecture: `Beta/Runtime/docs/architecture/actor-resources.md` (ordered continuity evidence).

## Verification

- Build accepted: `qualification/build-20260930T024439Z/report.json`.
- Focused run accepted, 104 passed (Core 55, Hosting 49), 511.6 s; 505 existing tests and 23 frozen
  specs preserved: `qualification/focused-20260930T024534Z/report.json`. Old tests and fixtures are
  unedited.
- New Core tests (`tests/OpenFantasy.Core.Tests/ResourceEncoding/WorldResourceHistoryEncodingTests.cs`,
  6 cases): known V1 bytes from an independent frame writer (reward/treatment/restorative-use),
  mutated public outputs, caller lists cleared before the first encode, equal command/evidence
  identities with different content, a successor that shares batches and evidence, and 96 concurrent
  cold callers across V1, codec and ContentHash.
- New Hosting tests (`tests/OpenFantasy.Hosting.Tests/ResourceEncoding/AnchoredResourceHistoryEncodingTests.cs`,
  2 cases), using a real archive anchor and a real joint anchor:
  - Expected outer V2/V3 bytes come from an independent writer.
  - Every refusal is exact: V1-encode of an anchored value, V2 without its anchor, V1 bytes with an
    anchor, V3 through plain decode, V1 bytes as joint, and a later joint anchor at both history and
    world level.
  - The world snapshot round-trips and contains the exact V3 frame.

## Complete retained replay (`measure.py`)

Setup:
- **Before:** the pinned baseline instrumented assemblies, as a hash-verified copy.
- **After:** a private copy of the same instrumented baseline tree. Only the 3 changed owned files are
  swapped in, with the baseline's exact anchors plus one after-only stage,
  `codec-world-resource-structure`.
- The script asserts that the two trees differ only in those files.
- Since the baseline pins, production has changed only in those 3 files plus 2 added `.md` files that
  are not compiled.
- The DB, the baseline report and assemblies, and the production source were unchanged by each run.

Every replay (8 of 8, ABBA order) completed: exit 0, `SourceHeadRevision=RecordedRevision=1739`,
`SourceHeadHash=StateHash=d4e869e3e9ce1a35625e8ce269596cf07c8ecfa5c9191213f00276704e3d99e4`, profile 30.

Authoritative run without overlap: `qualification/measure-20260930T025414Z/result.json`.

| Inclusive scope (replay thread) | before-1 → after-1 bytes | before-2 → after-2 bytes | Change |
|---|---:|---:|---:|
| full replay | 10,058,239,472 → 8,928,447,872 | 10,058,338,648 → 8,928,567,528 | −1.130 GB (−11.2%) |
| world encode (2,401 calls) | 5,178,222,992 → 4,054,243,560 | 5,178,187,552 → 4,054,250,864 | −21.7% |
| codec-world-resource (2,409) | 2,329,008,432 → 1,199,355,928 | 2,329,006,480 → 1,199,357,288 | −48.5% |
| cold structural captures | — → 784 calls, 793,725,608 | — → 784, 793,726,968 | |
| actor-history encodes | 2,419 → 789 calls; 550.1 → 207.4 MB | same | |

- These scopes overlap; do not add them.
- Allocation reproduces within 1 MB across all 8 replays, including the earlier run.
- GC collections at process exit: gen2 577→457 and 579→463; gen0 about −14%.
- Scoped thread CPU falls in every pair:
  - codec-world-resource: 967→404 and 837→402 ms.
  - world encode: 2,726→2,239 and 2,330→2,237 ms.
- Whole-replay thread CPU moves in opposite directions: 11,885→11,566 ms (−2.7%) in pair 1 and
  10,206→11,512 ms (+12.8%) in pair 2. That is run-to-run variance larger than the ~0.5 s scoped
  saving, so no whole-replay CPU or wall/latency claim is made.

The earlier run, `qualification/measure-20260930T024941Z/result.json`, is retained but not used for
CPU: all four of its replays overlapped this task's own focused Hosting run (after-2 took 72 s wall
with a 6.1 s GC pause). Its allocations match the run above.

## Invariants kept

- V1/V2/V3 bytes are bit-for-bit unchanged: an independent oracle checks them, and the real 1,739-entry
  replay reproduces the head.
- Every public array is fresh.
- No cache keyed by ID or hash, and no global cache.
- No predecessor retention: an appended history is a new cold owner.
- No memoized validation or admission: constructors, replay, anchor checks and full canonical
  comparisons all still run.

## Memory/lifetime tradeoff

One private array per *encoded* live history, about the size of its structural frame and bounded by
the V1 envelope. It lives exactly as long as that history, and it duplicates batch bytes that
`ResourceChangeBatch` already retains. Cumulative allocation is not peak or retained memory; the
retained size is unqualified.

## Residuals / dependencies

- The 784 cold captures still cost 0.79 GB:
  - They re-encode all evidence. Evidence-owned bytes would require editing `WorldResourceEvidence.cs`,
    which I don't own; this is reported as a dependency.
  - They encode the basis twice (directly, and inside `ActorResourceHistoryCodecV1` for
    `Basis.ContentHash`). Fixing that would need a basis-hash reuse design.
- Decode can build the structural frame twice (once for the inline body, once for the anchored value).
  This is rare in replay.
- About 4 GB of world-encode allocation is non-resource components plus the snapshot codec copying the
  returned array; these lie outside this ownership.
- Native opening failures are neither retested nor explained by this increment.
