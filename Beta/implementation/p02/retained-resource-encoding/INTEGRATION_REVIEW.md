# Resource encoding integration

The change retains pure C#, identical canonical bytes and complete recovery. Each immutable
`WorldResourceHistory` owns its structural V1 bytes lazily; the public encoder returns a copy,
and V2/V3 wrap those bytes with the actual anchor on every call. No authority, validation,
admission, predecessor chain, global cache, rule version or deadline changes.

## Review and verification

Astra reviewed all three changed production files, input/evidence immutability, concurrent
publication, fresh public arrays and anchor refusals. Successive histories own independent
encodings. The 96-call parallel test exercises concurrency without promising a particular race.
The eight new tests include independent byte construction and actual V2/V3 anchors. All 104
focused cases pass; the broader Core run passes 1,698 cases with no skips. These overlap and
must not be added as distinct cases. The accepted combined milestone is `codex/milestone-retained-native-gifts-20260930`;
`qualification/FINAL_CHECKS.json` links the 1,985 distinct passing integrated cases and exact build
compatibility, including the completed native saved-journal check and 237 session regressions.

The worker preserved all 505 pinned prior test/fixture files. Astra subsequently repairs one of
the newly delivered retained-result tests in the adjoining packet; all 501 files that predate
both assignments and all 23 frozen specs remain exact. That test repair does not change runtime
source or production assemblies. Architecture ownership is updated in `actor-resources.md`.

## Measured result and limits

`qualification/measure-20260930T025414Z/result.json` records four complete ABBA replays of the
same 1,739-entry world after the task's focused tests finished. Both builds reproduce the exact
head `d4e869e3e9ce1a35625e8ce269596cf07c8ecfa5c9191213f00276704e3d99e4`.
Full-replay cumulative allocation falls from about 10.058 GB to 8.928 GB in both pairs, about
11.2%. Inclusive resource/world encoding measurements overlap and must not be summed.
Whole-replay thread CPU moves in opposite directions; no reliable total-speed or production
latency improvement is claimed. Peak and retained heap size remain unqualified: the owned array
lives as long as its history. The actor-history layer intentionally receives no additional cache.

The earlier measurement overlapped this task's own Hosting tests. Its failure of isolation is
preserved in `qualification/OBSERVED_CHECK_OVERLAP.json`; the later measurement supplies the
non-overlapping comparison. External desktop load is still uncontrolled. The native opening
failures and later pass remain in the [retained native packet](../retained-gift-native/INTEGRATION_REVIEW.md).

Astra found that the original measurement script compared changed-path sets instead of full
source hash maps when asserting unchanged production. The three measured replacement hashes
and shared build configuration were independently checked against the integrated source; that
corroboration does not retroactively improve the old script. `measure.py` now records/compares
full source maps, rejects unexpected compile-input drift and shares a heavy-check execution lock.
The worker's phrase “2 added .md files” describes its incomplete baseline filter: those files
already existed and were excluded from the source pins. Neither is an added production file.

## Delegation and preservation

The exact `claude-opus-5-5[1m]` route returned verified Opus 5.5/1M metadata with requested Max,
successful adapter/provider status, verified pre/post context and no denials. Duration was
1,419.851 seconds; $10.9619998 is a client estimate, not billing or proved net credit savings.
No model retry occurred. The [dispatch](DISPATCH.json), [worker result](OPUS_RESULT.md),
pre-change checkpoint and delivery WIP checkpoint preserve their original scope and claims.
Astra owns integration and Git. Ecosystem commit `ad0c2e2` and the shared project instructions
record the reusable serialization/source-pinning lesson; all 71 alignment entries verified.

This optimization does not close production latency, scale, memory, platform, phase or campaign
gates. The retained native increment has its own behavior, provenance and regression evidence.
