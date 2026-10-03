# Movement delivery diagnosis and codec baseline

This checkpoint reproduces and localizes the existing four-player movement timeout. It adds a
repeatable codec-cost fixture and retains the failed trace; it changes no production runtime code.
The multiplayer timing gate remains open.

## Evidence

- [Failed sample diagnosis](DIAGNOSIS.md) and [machine-readable extraction](FAILED_SAMPLE.json):
  first host sampling at 151.9644ms, outside the unchanged 150ms lease; no selection. The 424.742125ms
  delay precedes tick serialization. This is distinct from the earlier projection-bound failure.
- [Original traced server run](qualification/server-compatibility-20261003T024354293307Z/report.json):
  75 passes, one original five-second movement timeout, one expected Windows-only skip, 67.389s.
  The predeclared sequence stopped at this first failure. No baseline repeats followed.
- [Final build](qualification/build-20261003T025831923110Z/report.json): warning-free solution build,
  12.494s, after adding actual application runtime reporting outside the measured workload.
- [Six focused cases on .NET10.0.12](qualification/codec-cost-net10-20261003T025903995718Z/report.json):
  pass in4.634s, with the actual application runtime checked in the log.
  Exact roundtrip wire/expanded/diagnostic bytes, own player inventory/balance/HP/MP scope and
  duplicate decoded property names (including escaped variants and nested objects) are checked.
- The [first build](qualification/build-20261003T025349739264Z/report.json) and
  [original six-case .NET8 probe](qualification/codec-cost-20261003T025535508267Z/report.json)
  remain retained. [Independent review](REVIEW.md) identified the runtime mismatch; it is corrected
  in the final measurement, without changing the project target or simulation rules.

## Fixed warmed workload

The [additive fixture](../../../Runtime/tests/OpenFantasy.Hosting.Tests/Networking/ObservationViews/ObservationCodecCostTests.cs)
uses the real production cargo-outcome world and four regional player views, with the same seed and
names as the existing projection-cost fixture. Setup, decompression for standalone Preflight,
assertions and reporting are outside the measurement. Each operation gets eight warmup calls and
32 measured calls (eight rounds over four views). Thread allocation is measured synchronously;
wall time is observational, with no newly invented performance threshold. The following results
are from the final .NET10.0.12 application, matching the failed server's runtime version. The
diagnostic-only `DOTNET_ROLL_FORWARD=LatestMajor` override must produce that exact logged runtime
or the checker refuses the result.

| Component | 32-call wall time | Current-thread allocated bytes |
|---|---:|---:|
| Encode, including complete production validation | 112.832ms | 24,197,200 |
| Decode, including preflight and canonical re-encoding | 178.880ms | 58,976,736 |
| Preflight alone on expanded JSON | 9.612ms | 3,968,768 |

The original .NET8.0.31 measurement reports122.923/217.649/9.912ms and
24,198,496/59,018,120/3,968,768 bytes respectively. These separate observations are not a qualified
runtime-version speed comparison. Encode and Decode already include Preflight; summing these
component measurements would double-count work.

All four bodies are uncompressed: 98,605, 97,106, 97,108 and 97,103 bytes. These are bounded component
measurements, not maximum-capacity, cold-start, concurrent, peak-memory or production-latency proof.
Preflight is a small part of this warmed workload; the data does not justify a new low-level JSON
parser. Neither the component averages nor the earlier allocation reductions explain why the live
timer/continuation sequence delayed host sampling. Keep that causal distinction explicit.

## Preservation and next boundary

Before-edit `codex/checkpoint-before-movement-delivery-20261003T024353Z` preserves
`b2bc319675afa738801a3965eab0285f7029f0a0`; [preservation pins](PRESERVATION.json) retain the original
754 tests/fixtures, 23 frozen specifications and unrelated 19-file predecessor staging. The working
branch stays `codex/runtime-foundation`. Raw reports pin actual source and binaries, including the
unchanged-source baseline before the new test existed. No old tests are edited.

Architecture impact: none. Runtime ownership, timing, codec format/bounds, privacy, canonical bytes,
controller custody and simulation rules are unchanged. This packet documents measured implementation
behavior without turning progress or diagnostic speculation into a timeless architectural contract.
No broad Core/storage/native evidence is relabelled as freshly rerun. Actor selection, platform,
native, human and full production gates remain required.

Next: isolate runtime pause/CPU/executor scheduling at the observation-to-host-tick boundary with a
bounded diagnostic that preserves live rules, then choose a measured repair. Do not renew movement,
extend the lease/deadline, add catch-up ticks or mark an isolated passing repeat as a fix. Full actor
selection continues on the accepted live-custody foundation after the timing dependency is addressed.
