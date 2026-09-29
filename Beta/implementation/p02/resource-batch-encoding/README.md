# Immutable resource-batch encoding

The Core retains each immutable resource batch's canonical V1 bytes on that exact batch object.
Repeated history encodes copy those bytes into independent public frames. This removes repeated
serialization of old effects while preserving complete replay, causal admission, canonical decoding
and archive authentication. No rule, schema, save migration, gameplay behavior or deadline changes.

Only `ResourceChangeBatch` and `ActorResourceHistoryCodecV1` change in production. Lazy publication
uses a volatile read and atomic compare/exchange; concurrent duplicate computation is safe and a
loser adopts the published array. There is no cache keyed by command identity, global owner,
predecessor-history reference or memoized validation result. The
[architecture reference](../../../Runtime/docs/architecture/actor-resources.md#ordered-continuity-evidence)
explains deep immutability, context-independent capture, copied output and retained-byte lifetime.

## Qualification

- Clean final solution build, zero warnings/errors; 224 affected Core cases and 27 affected Hosting
  cases pass without skips. Core was rerun after critic fixes; Hosting preceded the final test/comment
  refinements, with unchanged production logic. Counts describe 251 distinct cases, not repeated runs.
- Four focused tests cover output mutation, equal-command distinct batches, copied input and history
  sharing, and concurrent cold callers. The existing six-effect test now asserts the independently
  preserved pre-change SHA during normal test execution. Qualification also reproduces all three
  original basis/current/history files byte-for-byte.
- The unmodified rendered coordinated withdrawal and separate reopen both pass. Reports take
  20.513s and 28.530s respectively; process lifetimes are 24.064s and 36.874s. The original opening 30s,
  probe 150s and process 180s limits remain unchanged. Background injection makes no focus requests
  and is not physical-device, human or art acceptance.
- The actual native database passes independent full-ledger authentication, including the original
  520-dose source prefix, actual recorded pre/post states, permissions, stock/resources/time, three
  material/care archives and corruption refusal through both `OpenExisting` overloads. It ran one
  substantive case without skips in 246.157s; no easier replacement world was used.
- All five prior care routes pass their ten rendered active/reopen processes, followed by their
  actual-ledger authentication case with no skips. These cover raw pointer/keyboard self-use,
  crafted controller self-use, helper assistance and permission withdrawal. Together with the
  coordinated authentication this increment executes 253 distinct substantive managed cases.

Evidence: [final build](qualification/build-final.json), [Core](qualification/core-final.json),
[Hosting](qualification/hosting-01.json), [native journey](qualification/native-coordinated-01.json),
[ledger authentication](../coordinated-native/qualification/resource-batch-native-auth-01-command.json),
[legacy native controls](qualification/native-legacy-01.json) and
[legacy authentication](../coordinated-native/qualification/resource-batch-legacy-auth-01-command.json).
Broader prior full-suite and 53-file compatibility evidence remains preserved, but is not represented
as rerun by this narrowly scoped optimization.

## Complete allocation comparison

[Full-workload results](qualification/full-replay-01/result.json) compare already-built disposable
instrumented Host binaries on private copies of the same actual failed-native save. They differ in
only the two intended production files. Both finish the complete 1,581-entry replay, retain identical
stage-call counts and recover authoritative head
`63c0f4517e92fbc0c0da8e9c5fe4651b0203b87e427c6f602f0469b886e037da`.
Original database, copied databases, instrumented binaries and source remain pinned and unchanged.

| Inclusive scope | Before allocated bytes | After allocated bytes | Reduction |
|---|---:|---:|---:|
| Complete replay | 11,274,188,800 | 9,760,279,512 | 13.43% |
| World encoding | 6,487,891,184 | 4,986,060,824 | 23.15% |
| Commit preparation | 11,082,658,992 | 9,584,742,824 | 13.52% |

These scopes overlap; do not add them. Cumulative allocation is neither retained heap nor peak memory.
The tradeoff is one private byte array retained for each encoded live batch. Exact production-scale
memory and lifetime remain unqualified. Both processes actually run .NET 10.0.12; a net8 client target
alone does not identify Godot's loaded runtime. The diagnostic's 90s Host bound does not replace any
native bound. Wall durations 70.845s and 24.036s are single sequential instrumented runs under varying
shared-machine load, not a causal latency speedup claim. The after build predates only a source comment
in the final production implementation; final production binaries are separately built and exercised.

Earlier fine-profile anchor failure and partial native runs remain in the
[startup packet](../native-startup-profile/README.md). In particular, the instrumented baseline and
after probes exceeded 30s before completing replay. They cannot support a complete allocation
comparison or be converted to passes. A later successful uninstrumented run does not explain away
the historical timing failure or qualify production latency.

## Review and preservation

[Fable's independent review](CRITIC_REVIEW.md) found no blocking defect. The
[disposition](CRITIC_DISPOSITION.md) records the golden-byte, copied-input, concurrency-claim and
purity refinements. Requested `fable[1m]`/Max returned verified `claude-fable-5-1` with 1M context;
282.679s, client cost estimate $3.28909425, no denied tools. This is not a net credit-savings claim.
Fable reviewed the pre-refinement code; Astra reviewed the subsequent small edits.

The owner permits Fable 5.1 and optional Opus 5.5, both 1M/Max. The Opus test assignment was
[previewed only](OPUS_PREVIEW.json), never dispatched and incurred no model request. Project and
maintained ecosystem doctrine record this clarification; ecosystem commit `e8ec7cb` and milestone
`codex/milestone-project-routing-clarification` include the verified installed-entrypoint sync.

Recoverable pre-change snapshots are recorded in [preservation](PRESERVATION.json) and the
[routing checkpoint](ROUTING_CHECKPOINT.json). The corrected
[milestone inventory](MILESTONE_INVENTORY.json) records 77 existing refs; the earlier diagnostic's
empty prefix-query result was insufficient to support its claimed inventory, and its README now
states that limitation. Its original evidence remains unmodified. All 23 frozen design files and the
19 unrelated staged predecessor files remain outside this increment.

The original post-cut personal-dose acquisition/use objective remains open: recipient custody forbids
the helper gift and the stress world's exact potion ingredient has no gathering source. Do not
fabricate medicine, relabel definitions or silently migrate the lived world. This increment does
not close production latency/scale, physical platforms/devices, multiplayer, art/audio, human
acceptance or any full phase of the campaign.

Integration checks and artifact pins are retained in [final verification](VERIFICATION.json).
Commit and qualified scope are preserved by `codex/milestone-resource-batch-encoding`; development
continues on `codex/runtime-foundation`. No predecessor edits, push or merge are included.
