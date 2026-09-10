# Durable composed local-world authority

The .NET 8 `OpenFantasy.Hosting.Worlds.SqliteWorldAuthority` implements the same in-process service boundary for the headless and embedded hosts. It uses the retained profile-2 storage adapter and the portable `WorldStepProcessor`. It does not create another authority or require IPC. The central task owns the fixed-cutoff scheduler, native client integration and their qualification.

## Public and scheduler boundaries

`CreateNew(path, genesis)` admits a canonical world with revision zero, advancing-step zero and no active committed dialogue, then initializes the database. Inventory revision is an independent counter. Creation does not infer genesis from an older counted save. `OpenExisting(path)` admits the exact physical profile and completes logical recovery before returning a usable service.

`Commit(batch)` is a privileged host operation. Authentication and selection at the fixed host cutoff happen before the batch is constructed. The service rechecks encoded action origin/control and movement control against current admitted bindings. Those local checks do not authenticate a network sender. Batch world, branch, revision, step count and held/advancing kind must match the current world. Fresh command and cause identities must remain available against the current root, the admitted genesis and the retained journal registry.

`FindReceipt(command, delivery)` first validates current world/branch, principal and actor control. It then returns a prior outcome only for the exact immutable command bytes. A historical origin session may remain part of an exact retry after current authorization; a request without a prior outcome must originate in the current delivery session. This algorithm is not an implemented controller-rebind workflow. The current game profile does not add a session-change command merely to exercise this future case.

`InspectForHost()` returns immutable state, native database diagnostics and journal usage. This is privileged host data, not a permission-filtered player or NPC knowledge projection.

The same-assembly scheduler has `ReadStateForHost()` without per-frame SQL diagnostics, `IsIdentityOccupiedForHost(id)` and `Preview(batch)`. Preview uses the same pure preparation, historical checks and recovery budgets without writing or reserving anything. Its candidate is private and must not be published or acknowledged. Commit revalidates the batch, so preview does not establish a lease across intervening operations. The serialized host loop owns selection order.

## Atomic commit and retry

Preparation produces the exact accepted batch, canonical aggregate outcomes, per-command exact request/receipt bytes, mapped identity roles and a canonical candidate. The service checks that each receipt joins its command, principal, world, branch, request hash and next commit sequence. Before/after world hashes bind the transition.

One immediate SQLite transaction writes the typed journal entry, every terminal command outcome, every new identity, the current head and any scheduled checkpoint. The in-memory root and outcome are published only after transaction commit succeeds. Disposing an uncommitted transaction rolls it back. A write/commit error or injected lost reply marks the instance as requiring recovery; every subsequent operation except disposal fails until the world is reopened.

There is no durable inbox. Pending input is not accepted merely because it entered a memory queue. After an uncertain operation, reopen and use the exact request to retrieve a committed receipt or determine that it has no terminal outcome. A stale/reused batch is a dispatch failure; submitting it does not execute it again. A gameplay rejection is a committed terminal actor outcome and consumes its own command/result identities.

`AuthorityDispatchException` represents invalid selections, current authorization or identity conflicts. `WorldRecoveryLimitException` is a distinct host stop condition. SQL, IO, recovery and budget failures must remain visible; they must not be rewritten as ordinary gameplay rejections or trigger an endless idle fallback loop.

## Logical recovery

Recovery decodes the canonical genesis, requires commit/step zero, and replays the full journal in contiguous sequence order. Every batch is decoded, current control claims are rechecked, and the pure processor is called with identities already occupied by preceding commits and genesis. Stored kind, cause, step count, exact batch/outcome bytes and before/after hashes must match replay.

The separate command-outcome table must contain exactly the replayed principal/command keys, exact request and receipt bytes, and their actual commit sequences. The occupied-identity table must match every expected identity, commit and role without extra or missing rows. The current head must equal the final replayed canonical state and counters. Physical checksums alone cannot establish these relationships.

Checkpoint zero equals genesis. Subsequent checkpoints occur after 6,000 advancing steps or 10,000 total commits since the last checkpoint, whichever boundary is reached first. Recovery checks each checkpoint against the exact replayed state at that boundary and rejects missing, extra, early or future checkpoints. Checkpoints do not yet bypass replay. This strict profile has no journal pruning, archive rotation or automatic migration.

The local file is not cryptographically authenticated. Replaying a coherent journal verifies internal consistency and deterministic behavior, not that an external editor or sender had original authority to produce every byte. Remote trust, signed imports and broader world-history lineage remain separate requirements.

## Development recovery envelope

The current explicit development limits are 250,000 commits, 65,536 terminal actor outcomes and 134,217,728 cumulative accepted-batch plus aggregate-outcome bytes. Both Commit/Preview and recovery enforce them. At an uninterrupted 20 Hz, the commit count permits approximately 3.47 active hours; holds and lower host activity change that relationship. This is not an acceptable production save lifespan and is not a promise that replaying the largest allowed trace is fast. A measured checkpoint-tail recovery and retention design must replace or revise these limits before production acceptance.

Recovery retains bounded expected identity and command records in memory while streaming journal entries and checkpoints. It does not retain every full world state. The service caches the committed state hash between operations; producing each new candidate still serializes a complete snapshot. Real farm, larger terrain/body populations and target-device measurements are required before setting performance budgets.

## Verification status

Initial execution is retained in `INITIAL_RUN.json` and `test-results/world-authority-debug.trx`: 63 of 67 Hosting cases passed. Two service assertions expected hand-over from a fixture missing its explicit Manipulation capability; the correct rejected outcomes exposed that fixture omission. Two content tests incorrectly read numeric legacy IDs as strings. Those test fixtures/selectors were corrected by their owners. A later green run will be recorded separately rather than replacing this evidence.

The second pinned Debug execution passed all 16 composed-service cases and 69 of 70 total Hosting cases. The remaining failure belongs to the new embedded-content packaging: a requested source-support text resource was absent. That owner is correcting the package; this service result does not label the entire integrated run as passed. Exact tested assemblies and case names are retained in `TESTED_ASSEMBLIES.json` with `test-results/world-authority-second-debug.trx`.

After the content packaging correction and scheduler integration, the independent session worker reported all 82 Hosting cases passed. This implementer then separately reran the 16 service cases with the 12 session/controller cases: all 28 passed. Final assembly hashes and that exact TRX are bound in `FINAL_TESTED_ASSEMBLIES.json` and the adjacent client-session review manifest. Earlier failed-run evidence remains intact.

The initial actual 6,000-commit test passed with its 5,199-byte synthetic genesis: approximately 1,549.935 ms for creation/commits/close, 518.101 ms for full reopen/replay/close, and a 2,949,120-byte closed database on this macOS host. The test also removed the scheduled checkpoint and verified reopen rejection. These are narrow measurements of a small synthetic scene, not export-platform or worst-case performance qualification.

Retained service tests additionally cover exact movement/time/custody receipt recovery, dialogue hold/close without retrospective time, preview privacy, partial source splits, committed gameplay rejections, stale/reused causes, current authorization, altered command bytes, branch mismatch, before/after-commit lost reply recovery, semantic table corruption, invalid genesis and budget/checkpoint boundaries. Simulated exceptions at commit boundaries are not process-kill or power-loss tests. The coordinator separately owns real process interruption, native Godot and complete playable-loop qualification.
