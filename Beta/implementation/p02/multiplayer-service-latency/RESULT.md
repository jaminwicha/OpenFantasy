# Terrain projection reuse checkpoint

The actual four-player movement failure is captured: the third sample arrives during serialized
view publication, waits for its remaining projections and the next cutoff, then expires before its
first selection at 163.3869 ms. The original 150 ms lease correctly rejects it. The
[diagnosis](DIAGNOSIS.md) retains the exact trace and component measurements.

Worksite and liquid views now reuse their public occupancy for the exact immutable terrain-region
instance. A conditional weak table avoids retaining old regions; equal bounds/revisions do not alias
separate branches. Old views remain immutable, wire rehydration still copies input, and each player
still receives its own inventory and offers. No authority, save, canonical encoding, readiness,
movement, deadline, lease or parallelization contract changes.

The unchanged warmed 32-view workload allocates **1,217,344 bytes versus 16,003,392 before**, eliminating
14,786,048 bytes of repeated construction. Measured wall time is 3.385 ms versus 215.407 ms; these shared-
desktop observations are not a frame-rate, cold-start or production-latency guarantee.

## Verification

[Final checks](FINAL_CHECKS.json) pin source, actual binaries and retained reports. The build is
warning-free and **567 distinct managed cases pass**, including all 8 new cases:

- New projection ownership, lifetime, parity, privacy and allocation: 8.
- Existing terrain/publication, including real mining and released-water views: 14.
- Networking: 265; shared sessions: 26; managed client: 179.
- Normal managed Server: 77 pass, with one Windows-only ACL skip on macOS. This includes the unchanged
  four-player TLS movement journey that failed in the retained traced baseline.

The captured failure has a demonstrated expiry mechanism and a measured reduction of the work
blocking selection. The successful normal aggregate supports the repair's bounded integration;
it does not explain every historical timeout or qualify arbitrary production concurrency. Earlier
archived-query failures remain open even though this Networking selection passes. No failed report
is removed or rewritten.

All 701 older test/fixture files, 23 frozen specs and the exact unrelated 19-path predecessor staging
remain unchanged. Both affected architecture references are current; all 1,287 local reference links
resolve. The [independent source review](REVIEW.md) found no actionable defect and states its limits.
No external model request occurred; no Claude credit savings are claimed.

## Preservation and remaining work

The [initial checkpoint](PRESERVATION.json) preserves committed `272140ea` at
`codex/checkpoint-before-service-latency-20261002T192024Z`.
The [pre-optimization WIP snapshot](OPTIMIZATION_CHECKPOINT.json) preserves the new probe and all
baseline evidence at `codex/checkpoint-before-occupancy-reuse-20261002T192629Z` (`5fa698d8`).
It records the failing Server gate and is not an acceptance claim. The resulting verified increment
is preserved at `codex/milestone-terrain-projection-reuse-20261002` without switching the checkout.

Native profile 38 care decisions, complete-world pending/cut/disposition/re-cut, capacity/crash
boundaries, lived-save migration, hostile survival/restart/co-player policies and full campaign
implementation remain open. No new Core full-suite, physical-device, native, other-platform or
production scale acceptance is claimed. Next query work must preserve full history authentication
and measure the real service path rather than repeatedly seeking green timing runs.
