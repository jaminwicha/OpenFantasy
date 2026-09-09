# Durable counted authority qualification

9 September 2026. Local macOS ARM64 development evidence. Full P00/P01/P02 exits and the first-playtest goal remain open.

## Measured result

**262 core tests and 19 hosting tests passed in both Debug and Release**, with no skipped/unexecuted cases. Debug's full verifier reproduced all 14 independent Python fixture sets, retained the existing 68 host inspection cases/source-packet checks, and passed pinned Godot import, managed binding and native SQLite execution. Release built with no warnings/errors, mapping the client to ExportRelease, and repeated both test suites and the actual dedicated-host crash/recovery qualification. No distributable desktop export was produced.

For each configuration, an external process was killed after all transaction writes but before commit, and another immediately after commit but before its reply. The first reopened at the original state/sequence; the second reopened at the committed state/sequence. Exact retries returned the same canonical receipt and one transfer in every case. A second process was refused while the first retained ownership. These are abrupt process-death tests, not power-loss or filesystem fault qualification.

The headless Godot process used the same actual hosting library and native SQLite 3.53.4 as the dedicated executable. It created a separate database, committed the six-unit fixture transfer, disposed/reopened the authority, and retried it. Its state hash and exact receipt bytes matched the dedicated process; the dedicated process subsequently reopened the Godot-created world. The store reported WAL, synchronous FULL, foreign_keys ON and exclusive locking in both processes. The original state contained eight wood units; transfer retained six at the receiving custodian and two at the source without overlapping source claims.

The new Core terrain tests cover overlap/order independence, negative and extreme coordinates, strict malformed restores, vertical layers and complete excavation retention. They do not demonstrate terrain being persisted by the SQLite world store yet. The current geometry emits no mined items and cannot place constructed/moved material.

## Evidence map

| Record | What it verifies |
|---|---|
| [verification.json](verification.json) | Full Debug commands/output, 14 Python oracles, 262+19 tests, 68 prior host inspections, actual runtime/native observations, crash cases and 274 final runtime input hashes |
| [core-debug.trx](core-debug.trx), [hosting-debug.trx](hosting-debug.trx) | Executed Debug case identities/results |
| [release-validation.json](release-validation.json), [core-release.trx](core-release.trx), [hosting-release.trx](hosting-release.trx) | Release build observation and all Release tests |
| [durability-release.json](durability-release.json) | Separate actual Release host process-kill/reopen/retry evidence |
| [preflight.json](preflight.json) | Earlier successful dedicated/Godot native probe; final qualification additionally tests competing processes |
| [evidence-integrity.json](evidence-integrity.json) | All 274 current runtime hashes and 32 authority / 18 terrain / 63 storage review entries matched |
| [preservation.json](preservation.json) | Original 19 staged predecessor paths byte-identical; all 43 predecessor compile items remain under its own src tree; nested editor exclusion remains |
| [ARCHITECTURE.md](ARCHITECTURE.md) | Shared adapter decision, transaction scope, source-backed native dependency/durability choices and growth limits |

## Independent review

The [authority critic](../authority-review/REVIEW.md) found a legal full 4096-lot state could throw on a partial split. An explicit capacity guard plus positive/negative regression now cleanly rejects partial split while allowing a whole-lot move. The [terrain critic](../terrain-review/REVIEW.md) independently reproduced the oracle and checked signed floor arithmetic, interpolation weight bounds, index inverses and sampled identities; it found no blocker in the declared substrate scope.

The [storage critic](../durable-authority-review/CODE_REVIEW.md) found that a modified SQLite schema could suppress required writes, and that schema inspection needed to share the exclusive ownership lifetime with recovery. Exact schema admission, affected-row assertions and locked metadata/schema validation close those findings. The critic executed nine separate adversarial cases, then reran all nine against final Debug binaries. Historical and final assembly hashes/TRX reports are retained separately.

## Limits and next work

The local operator's principal/session arguments are trusted context, not network authentication. The store currently has no session issuance/rebinding, control-change commands, player projection delivery, due-work/event/outbox persistence or full actor/location/time state. Full replay visits the complete retained journal; storage, checkpoint and recovery budgets are not yet qualified. Terrain bytes are strict standalone domain snapshots until admitted into the same world transaction. Whole-game import, the connected Wren loop, Wait combat, native art/motion/audio acceptance and 1–4-player playtests remain real work. The [current progress record](../../p00/PROGRESS.md) and [next-loop direction](../README.md) preserve these obligations.

## Commit checkpoints

- `5d8fa59`: portable counted authority, seeded stacked terrain, independent fixtures and cross-reviews.
- `f7e3670`: shared durable hosting, local operator/Godot integration, storage tests and reproducible verification.

The following documentation/evidence commit retains the measurements and current phase status. It does not change the runtime inputs that were tested.
