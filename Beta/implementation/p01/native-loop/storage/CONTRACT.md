# Local world storage profile 2

Implemented and tested on 9 September 2026 in the local time zone (10 September UTC). This is the concrete SQLite storage adapter for the composed local world. The central task owns canonical world interpretation, the public authority service, recovery, input scheduling and presentation. This document does not qualify those consumers before their own integration tests pass.

## Retained boundary

`OpenFantasy.Hosting.Worlds.WorldDatabase` is an internal adapter in the existing .NET 8 hosting library. Both the headless executable and embedded Godot host can use that library in process. The adapter does not introduce another authority, an IPC layer, an event bus or a second simulation.

`SqliteConnectionLease` owns the shared native connection setup. The existing counted authority continues to use schema version 1 and its original SQL table definitions, canonical bytes and public APIs. The composed local world uses the same application identifier, `0x4f464157`, with explicit schema version 2. Each adapter rejects the other's files. There is no automatic migration, invented genesis, overwrite or fallback reset.

Creation reserves a new file with `FileMode.CreateNew`; reopening requires an existing file. A failed or interrupted creation can leave a partial file, which remains visible and is rejected on normal reopen. Recovery or removal of that file is an operator decision.

## Connection admission and ownership

The shared lease uses a private, unpooled SQLite connection with WAL, synchronous FULL, foreign keys enabled and lifetime EXCLUSIVE locking. An immediate transaction establishes ownership before authoritative data admission. Existing application and schema identifiers are checked before setup and repeated under ownership. The exact retained table definitions and expected autoindexes are checked under ownership; extra triggers, views, columns or other schema objects are rejected. The adapter checks SQLite integrity and requires a native version at least 3.51.3. Profile 2 additionally runs `foreign_key_check` before returning an existing database.

The current package pins remain Microsoft.Data.Sqlite.Core 10.0.12, SQLitePCLRaw.bundle_e_sqlite3 3.0.5 and SQLite 3.53.4. `Inspect()` exposes the same native version/source and connection diagnostics as the counted adapter. Package selection is not a claim that every export target has loaded or qualified that native binary.

The owning authority serializes callers, holds the lease for its lifetime, and consumes lazy read enumerations without a competing operation on the same connection. All modifying adapter operations require an explicit live transaction from that lease. Each required insert or head update must affect exactly one row. A failed candidate transaction must be disposed without commit; catching a failed statement and committing other writes is not a supported authority workflow.

## Typed stored records

| Table | Retained contents |
| --- | --- |
| `world_genesis` | One initial canonical state and its SHA-256 checksum. |
| `world_head` | One current commit sequence, advancing-step count, state and checksum. |
| `world_commits` | Sequence; advancing step or held interaction; unique cause ID; advancing-step count; exact accepted batch and outcome bytes; before/after state hashes. No full after-state copy per journal entry. |
| `world_command_outcomes` | Principal plus command ID key, exact command bytes, exact receipt bytes and a reference to its committed journal row. |
| `world_occupied_identity` | Reserved identity, commit reference and fixed role: commit cause, command, result or spawned entity. |
| `world_checkpoints` | Sequence, advancing-step count, complete state and checksum. Genesis supplies checkpoint zero. |

These rows are internal byte-boundary DTOs. They are not permission-filtered player projections, authenticated commands or self-validating world snapshots. The service maps the database enums to the core's separate typed batch enums and performs canonical decoding, cross-module validation and deterministic replay.

All counters are exactly eight big-endian bytes, preserving the entire unsigned 64-bit range and SQLite binary ordering. Journal, command-outcome and occupied-identity commit sequences are positive. Genesis/head/checkpoint counters can be zero. Identity fields are exactly 16 bytes; content hashes are exactly 32 bytes. Every variable binary field is nonempty and bounded:

| Field | Maximum bytes |
| --- | ---: |
| State, including genesis/head/checkpoint | 16,777,216 |
| Accepted batch | 262,144 |
| Commit outcomes | 262,144 |
| Individual command | 16,384 |
| Individual receipt | 16,384 |

Both SQL constraints and adapter writes apply these limits. Reads inspect binary type and length before materializing a managed byte array. State reads also compare the stored checksum. Checksums detect mismatches; they are not signatures and do not establish external authenticity.

## Transaction and recovery responsibilities

The service must validate the current root, permissions, expected revisions, command identity and collisions before creating a candidate. It then writes the typed commit, each terminal command outcome, all required identity reservations, the new head and any checkpoint in one transaction. Only a successful commit permits publishing the new in-memory root or acknowledging acceptance. There is no durable inbox in this increment: pending movement or interaction requests are not acknowledged as accepted merely because they entered a memory queue.

The principal/command key prevents replacing a terminal outcome. The service must compare exact command bytes for retry equality, revalidate current session/actor permissions as required, and distinguish an identical retry from key reuse with different bytes. SQL uniqueness alone does not implement retry authorization, cross-table identity roles or world-wide collision detection.

The service owns contiguous sequence validation, step arithmetic, cause/input/receipt joins, complete identity reservations, checkpoint correspondence and replay to the stored head. Adapter `Open()` means the physical format is admitted, not that semantic recovery has finished. Storage tests that write high counter values use arbitrary bounded bytes and deliberately do not represent a valid gameplay trace.

The selected integration policy requests a checkpoint after 6,000 advancing steps or 10,000 total commits since the relevant checkpoint. The adapter exposes checkpoint writes; it does not schedule them. A 20 Hz step clock produces 72,000 journal rows per active hour. Compact commits avoid repeating a complete world state in every row, but journal/outcome/identity/checkpoint retention still grows. This increment provides no pruning, segment rotation, archive policy, bounded replay-time proof or storage-budget qualification. The authority must measure those costs using real batches before claiming production readiness.

## Executed verification

The pinned SDK built the hosting test project and passed all 32 cases: 19 retained counted-authority and independent-review cases, plus 13 new world-storage cases. The new tests cover actual creation/reopen; all-table commit and rollback-on-disposal; exact batch/receipt retention; principal-separated lookup; unsigned counters above the signed range through `ulong.MaxValue`; bidirectional profile mismatch; overwrite refusal and competing ownership; unexpected triggers/views/schema columns; hash and oversized-state rejection; immutable unique keys; orphan foreign-key rejection; and invalid kinds, sequences and byte limits.

The new tests were authored before the production implementation, but no executed red run was recorded because build access was coordinated centrally. Their first executed build passed. The rollback test disposes an uncommitted transaction; it is not a process-kill test. No v2 replay, scene rendering, load/performance, cross-platform export, power-loss or process-kill qualification is claimed by these storage tests. Those are separate integration responsibilities.

Evidence: `test-results/world-storage-debug.trx`, `TESTED_ASSEMBLIES.json` and `VALIDATION.json`. Historical v1 review evidence remains unchanged; rebuilding or refactoring its source does not retroactively change the binaries that those earlier reviews tested.
