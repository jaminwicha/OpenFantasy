# Canonical world frames

This increment reuses canonical serialization for an exact immutable prepared candidate. The
[assignment](ASSIGNMENT.md), [worker result](RESULT.md), [delegation record](DELEGATION.json) and
[independent review](REVIEW.json) preserve scope and evidence. Runtime remains portable C#.

The original encoder, canonical profiles, schema, complete-world admission, archive retrieval and
whole-journal recovery stay authoritative. Frame capture replaces existing encoding at its original
refusal boundary; it never moves a per-action failure outside rollback. Old unframed candidates keep
the Host fallback. Exported bytes are defensive copies. This trades transient retention of a frame
plus durable copy for removal of repeated serialization; it does not establish lifetime scalability.

## Qualification

A clean integration build and full regression pass: **1,577 Core + 896 Hosting = 2,473 distinct
tests**, with no failures or skips. The 19 focused integration cases are included in that total.
All 49 retained legacy frames and four actual coordinated journey roots/command files reproduce
exactly. [Verification](qualification/verification.json) pins the source, execution and original
reports; [canonical comparison](qualification/canonical-comparison.json) records all 53 files.
Astra extended the worker's seven tests with export mutation before hashing, legacy durable fallback,
later rejected movement, and exact candidate frames in actual liquid/production journeys. The
independent critic's eight runtime hashes still match the final source. All 829 architecture file
links resolve; this check does not validate section fragments or external URLs.

The unchanged external recovery test **failed** on normal publication at 30.021 seconds. No planned
BeforeCommit/AfterCommit kill was reached. Inputs and execution hashes were unchanged, and no repeated
acceptance attempt was launched. The original failures and this result remain in
[the failure records](FAILURES.md). No deadline, journal bound, archive check or acceptance gate changed.

A disposable same-ledger diagnostic completed with the exact same inspection output. It records:

| Measurement | Before | After |
| --- | ---: | ---: |
| World encodes | 3,659 | 2,090 |
| World-root/resource admission calls | 2,092 | 2,092 |
| Cumulative replay thread allocation | 16,441,177,456 bytes | 12,460,832,584 bytes |
| Instrumented full replay | 29.645 s | 28.503 s |

The allocation reduction is 24.2%. Inclusive timings overlap; separate measurements on a shared
machine do not isolate wall-time causation, and the 90-second diagnostic bound is not the 30-second
acceptance gate. Allocation is cumulative, not live/peak heap. See the
[comparison](qualification/recovery-comparison.json) and [next investigation](RECOVERY_FOLLOWUP.md).
A second, more detailed disposable profile measured repeated immutable care-version hashes and
identifies owner-local lazy digests as the next candidate. Its separate 26.749-second completion
does not qualify the external crash matrix or erase the retained failure.

Fable returned successfully in 833.615 seconds with no permission denials and verified context
postflight. The returned model was `claude-fable-5-1` with a 1,000,000-token context; the request pinned
`fable[1m]` and Max effort. Its client estimate was $7.77281375 within the $12/1,200-second bounds.
No subscription credit savings are claimed. The stale sync-manifest preflight was repaired before any
paid call; the active ecosystem manifest and maintenance hook are committed upstream at `7d50112`.

## Preservation and remaining work

The pre-change checkpoint is `codex/checkpoint-before-world-canonical-frame-20260928T221903Z` at
`477c264dbff11e48e2ca1899de690f0a5477c532`. Its [manifest](preservation-before.json) records 73 existing
milestone refs, 23 frozen specifications and the unrelated 19-file predecessor index. No prior pointer
is moved. The reviewed increment is preserved on `codex/milestone-world-canonical-frame`; resolve
that immutable branch for its exact commit. Its managed acceptance excludes the failed recovery gate. [Failures](FAILURES.md) retain the rejected ref inventory and corrected delegation manifest.

Normal fresh-game selection remains profile 26. Actual post-cut native use and separate-process
reopening remain pending, as outlined in [the native follow-up](NATIVE_FOLLOWUP.md). No production
latency, lifetime, devices, full phase, art or human acceptance gate is closed by serialization reuse.
