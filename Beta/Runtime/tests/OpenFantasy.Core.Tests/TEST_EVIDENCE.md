# Portable primitive test evidence

Recorded 2026-09-08 on macOS arm64. Scope is the portable identity, calendar, checked arithmetic and SHA-256 counter-stream primitives. It is not evidence of an implemented world, production saves, collision detection, multiplayer, platform exports, visual quality or performance qualification.

## Executed checks

- Initial test-first run failed to compile because the declared `CounterRandomV1` API did not yet exist (`CS0246`). After implementation, all 43 original cases passed.
- Added a calendar JSON regression plus two RNG cases. The regression failed as expected: System.Text.Json returned day zero from a nonzero immutable struct. Adding `[JsonConstructor]` selected the validating constructor; the same test also verifies rejection of negative within-day milliseconds.
- Final retained suite: **46 passed, 0 failed, 0 skipped**, Release configuration, .NET SDK **10.0.401**, targeting **net8.0**, using the private toolchain with .NET runtime **8.0.31**. The runner reported 76 ms test duration; this is a correctness run, not a workload benchmark. Build warnings are treated as errors.
- Independent Python/hashlib vector generation reproduced `Fixtures/reference-v1.json` byte for byte. No production C# code participates in generating expected vectors.
- Retained source, project, documentation, fixture and lock files passed the requested project naming scan. Generated build paths and artifacts are outside that source naming check.

The final command, run with the pinned private `dotnet` executable from the runtime project root, is:

```sh
dotnet test tests/OpenFantasy.Core.Tests/OpenFantasy.Core.Tests.csproj --configuration Release -p:RestoreLockedMode=true --nologo --verbosity minimal
```

`dotnet test` uses the MSBuild property above for locked restore. The `--locked-mode` switch belongs to `dotnet restore`; an initial attempt to pass it directly to `dotnet test` was rejected before execution and corrected.

## Coverage of high-risk behavior

Canonical identity vectors include strict UTF-8, non-ASCII fields, separator-safe length framing, UInt64 ordinal limits, accepted-cause derivation and big-endian order. Time tests cover negative crossings, full Int64 day bounds, long millisecond deltas, independent BigInteger arithmetic, eon-scale differences and validated JSON roundtrip. Numeric tests cover signed basis-point truncation, Int128 intermediates, checked overflow and nonnegative ceiling division.

RNG checks cover independent fixed words and stream fingerprints, a forced rejected prefix, every key field, unrelated stream activity, mutated input seed arrays, exclusive integer bounds, JSON counter-state continuation, wrong-stream/version rejection, final-counter exhaustion, and rollback of a bounded request whose last candidate is rejected. They prove the declared primitive contract on this machine. Cross-process persistence, scheduler ordering, collision registries and complete cross-platform replay remain separate implementation work.
