# Exact bounded terrain read dependencies

This increment implements immutable region references, complete rectangular read plans, admitted read
sets and a bounded Hosting archive loader. File-backed evidence loads two required regions, leaves
the third archive unread, and feeds the existing local geometry implementation. It is a real terrain
preparation boundary, not complete partitioned world simulation or a new save format.

## Implemented boundary

- Core `TerrainRegionReference` pins scope, bounds, revision and canonical hash. `TerrainReadPlan`
  selects only intersecting references, rejects gaps/overlap/mixed scopes, and enforces a declared
  loaded-region budget plus a finite candidate metadata scan. It orders reads deterministically.
- `ValidateCurrentReferences` detects changed required pins; unrelated region changes do not
  invalidate the read. `Resolve` requires every exact region and rejects extra, duplicate or substituted
  snapshots before returning immutable `TerrainReadSet`. Neither operation authenticates the directory
  or holds it stable through a future commit.
- Hosting `TerrainReadPreparation` passes the remaining bounded byte allowance to its supplied archive
  reader and independently checks returned data. It checks total decoded cells before I/O, then verifies
  each archive's exact metadata before realizing cells. Failure returns no partial admitted set.
- `TerrainRegionCodecV1` adds an expected-reference decoding overload. Existing format and ordinary
  decoding remain. The archive-size bound precedes hashing; scope/bounds/revision are checked before
  terrain generation. This prevents false metadata from understating the decoded-cell footprint.

The reference list covers only an explicitly chosen query rectangle. Automatic actor sweep/support
footprint derivation, other bodies/structures, rights, cargo and time dependencies remain necessary
for a complete action plan. Metadata is supplied by the caller; there is no authoritative paged
directory or mutation publication here. The ordinary Host still admits its complete world.

## Evidence

Final [Core run](check-02/core.json): **55 passing cases**, no failures/skips, 9.870-second invocation.
Final [Hosting run](check-02/hosting.json): **three passing cases**, no failures/skips, 15.950 seconds.
Both retain 180-second invocation bounds; builds report no warnings/errors. There are **58 distinct
final passes**, including 11 new cases. The initial 54+3 pass is retained in `check-01`, not added to
that total. Final reruns are justified by the subsequently identified header/budget issue and codec
change. Existing terrain canonical/codec/edit/generation and spatial movement tests remain in scope.

The actual comparisons and negative cases cover:

- Existing `LocalSpatialSnapshot.PrepareMove` through a region boundary, with complete versus selected
  terrain producing the same candidate; a real destination obstruction produces the same refusal.
  No alternate collision algorithm or movement authorization was added.
- A file-backed three-region fixture derived from the real world-service terrain rules and actor
  bodies. The reader is invoked for exactly two pinned archives; the third is never read or decoded
  by the loader. Both actual actors remain in the local geometry. The complete comparison basis and
  file preparation still exist in test memory: this is evidence about loader reads and its returned
  resident set, **not process high-water memory** or a global bounded-residency claim.
- Missing support/coverage, ambiguous metadata, loaded-region and metadata limits, missing/extra/
  duplicate/changed snapshots, mixed realm, false bounds/revision/hash and changed current references.
- Missing/corrupted/stale archive bytes, cumulative byte exhaustion after an earlier successful read,
  and insufficient decoded-cell budget before any reader call. Exact-budget reads succeed.
- False small bounds paired with the genuine hash of a larger archive. The expected-header check
  rejects before realization. This issue was found during lead review after the first passing run;
  it was corrected before acceptance and has a regression through the Hosting boundary.

The fixture loads 120 cells (40 in the left region,80 in the right) and excludes the distant80-cell
region from the result. Its request rectangle is12 cells including support; the budget deliberately
charges entire decoded regions rather than the smaller rectangle. These are diagnostic sizes. The
4,096-candidate bound is per preparation, not a cap on world regions, actor population or cities.
No throughput, cold/warm latency, heap-size, streaming stall or production minimum is qualified.

## Preserved scope and next work

The [checkpoint](CHECKPOINT.json), `codex/checkpoint-before-terrain-reads-20261017T221134Z`, preserves
`703fee6d648d3f9a65cccbf78e0b4296d557deeb` before this change. Accepted marker:
`codex/milestone-terrain-read-dependencies-20261017`. Unrelated predecessor staging, the October5
advisory, frozen specs and existing evidence remain untouched.

Next derive the full actual action footprint and combine terrain pins with actor/structure/cargo/time
dependencies under the existing authority. Then prove ownership crossing, absent-detail funded work,
atomic publication/crash/retry and original-history replay. Historical pinned reads cannot silently
become current write bases. These required PLAN-STATE outcomes, independent review and all LANG/city/
frontier/P02/native/human gates remain open. No incomplete `WorldSnapshot` admission is permitted.

## Documentation review

- **Plan/spec:** CURRENT_ROADMAP, AGENT_HANDOFF and PLAN_STATE_COMPARISON identify the concrete terrain
  preparation boundary and remaining complete-action/authority obligations. The original prototype and
  all full-scope gates remain required. No shipping partition format or migration is selected.
- **Reference:** World/geography documents reference identity, coverage, budgets, expected decode,
  read-set admission and limitations. Persistence/history cross-links the boundary while distinguishing
  pinned historical reads from authenticated current dependencies. The pages remain timeless.
- **Accuracy/completeness:** Lead reviewed bounds/intersection arithmetic, canonical ordering, immutable
  ownership, expected header checks before realization, failure behavior, budget paths and actual spatial
  consumers. Direct tests cover the discovered false-header hole. No claim of a live directory, action
  permission, absent NPC simulation or process-memory reduction is inferred from these read sets.
- **Checks:** Final 58-case results are in retained TRX/logs. Same-change documentation/local-link,
  authored whitespace, source/binary identity and protected-input checks are in `VERIFICATION.json`.
  No independent critic ran while orchestration is paused. Native tests are not substitutes for these
  Core/Hosting contracts and were not rerun for this unbound preparation API.
