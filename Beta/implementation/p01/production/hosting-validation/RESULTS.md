# Hosting production upgrade verification

10 September 2026 UTC. Author-owned implementation verification; this is not an independent review of the Hosting implementation.

**Current qualification:** the independent review subsequently found an orphan destination-sidecar problem in the implementation tested below. It is fixed and the corrected suite passed **157/157**. The original 143-case record remains historical evidence. See [the sidecar correction manifest](SIDECAR_VALIDATION.json), [14 targeted cases](test-results/production-sidecars-debug.trx) and [157-case full regression](test-results/production-hosting-debug-sidecars-final.trx).

The independent critic reproduced SQLite recovering unrelated history from an orphan destination WAL. The corrected shared `SqliteFileSet` guard rejects main, WAL, SHM and rollback-journal paths, including directories, before new database or backup creation and at both upgrade entry and immediately before publication. Existing paths are preserved. The retained native test independently reproduced the unsafe control on **SQLite 3.53.4**, then verified rejection of that exact WAL injected at `BeforePublish`. Source files remained byte-identical. This fix qualifies known file-set admission checks; it is not authentication against arbitrary external filesystem modification after the final check.

The corrected manifest binds 320 input files, 17 tested output artifacts and the packaged macOS arm64 native library. No input changed after the run finished when evidence was captured. One initial nullable test-output compiler error (`CS8604`) was fixed before executing the 14 cases; the production files compiled during that failed invocation. All final compilation and test runs passed. The corrected full-suite tiny-upgrade measurement was 1,117.113 ms under concurrent tests, with the same 103 prior commits and 110,592/172,032-byte source/destination sizes. The timing remains a small-fixture observation.

## Initial implementation run

The pinned Debug build and complete Hosting test suite passed **143/143**, with zero failed or skipped tests. This includes all 108 previous Hosting cases, 21 new migration/storage cases and 14 content cases authored by the content worker. Core and Hosting compilation emitted no warning or error. The run finished at 03:20:12 UTC and reported a nine-second test duration.

The retained [TRX](test-results/production-hosting-debug-initial.trx) and [manifest](VALIDATION.json) bind 318 source/content/test/configuration inputs and 17 tested output artifacts. No recorded input had a modification time after this run ended when evidence was captured. Additional native dependency hashes identify the packaged macOS arm64 SQLite artifacts; the tests executed actual SQLite operations but did not separately print the loaded library path/version. Later integrated builds may replace outputs; these hashes remain the historical tested artifacts.

The new cases prove:

- Exact preservation of a gathered/depleted world, partial transfer and held dialogue, all old request/receipt bytes, retained genesis and historical identity rows, with one migration commit and zero elapsed steps.
- A real post-upgrade craft, partial raw-lot consumption, actual transformed outputs, unchanged raw source allowances and exact old/new command retries after restart.
- Six injected interruption boundaries spanning backup, schema change, COMMIT and publication. The original stayed byte-identical; a destination appeared only after publication and recovered the one accepted migration.
- Eight altered-history/schema cases rejected before readiness, including changed migration input/outcome, lost definition occupancy, wrong identity role, lost old receipt, unscheduled checkpoint, legacy head substitution and extra schema objects.
- Competing ownership, stale heads, reused historical cause, same-path destination and existing destination cannot overwrite progress.
- Actual native SQLite table rebuilding preserved unsigned counters above signed 64-bit range and foreign keys; a failed schema transaction rolled back both tables and metadata and restored foreign-key enforcement. This substrate-only high-counter fixture is deliberately not a valid contiguous gameplay journal.
- A fresh profile-3 genesis uses exact schema 3 without inventing a migration event; earlier schema/profile fixtures continue under their original contracts.

The tiny gathered-save migration used 103 prior commits/steps, including the final held dialogue. Backup, validation, migration, replay, publication and returned authority opening took **965.946 ms** in the concurrently executing test suite. Source main file was **110,592 bytes**; destination was **172,032 bytes**. This is a small-fixture measurement, not a production performance budget or isolated benchmark.

No native client rendering, native three-process gameplay, real process-kill migration, power-loss durability, export-platform acceptance, checkpoint-tail recovery or large production-history capacity is claimed by this test run. Those remain coordinator or later qualification work. Failed private staging copies remain available for diagnosis and are never automatically selected.
