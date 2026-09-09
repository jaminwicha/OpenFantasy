# Durable authority boundary — independent design recommendation

9 September 2026. Reviewer `p00_core`. Read-only inspection of the current Host/Core/Client projects, command/custody contracts, `Beta/AGENTS.md`, TDD §§1–4/5/production/history/transit, and P01 in the implementation plan. This is a design recommendation before the new store exists, not an implementation or durability pass.

## Retained assembly boundary

Add **OpenFantasy.Hosting**, targeting **net8.0**, as the actual shared application/storage library. Both the net10.0 CLI executable and the net8.0 Godot client reference it. Hosting references the portable Core; Core continues to reference neither Godot nor SQLite. The existing CLI `HostApplication` and Godot `Bootstrap` remain short entry points.

One concrete authority service owns a world's opened store and serialized work. SQLite storage, current session/control lookup, permission adjudication, command orchestration and recovery belong in Hosting. Keep deterministic transfer rules in the existing Core planner. The client submits intent and renders permitted results; it never independently applies the planner candidate. This uses one in-process authority for solo, headless and eventual hosted play, with no IPC or alternate inventory.

A useful minimal API is separate `CreateNew(path, acceptedGenesis)` and `OpenExisting(path)` operations, followed by `Submit(currentDeliverySession, canonicalCommandBytes)` and a permission-scoped inventory view operation. Names are illustrative. Do not introduce an interface hierarchy or one project per table. A concrete storage owner and a real crash-injection seam suffice for this increment.

## Minimum real durable scope

The first database can retain one finite branch-local custody world, its accepted genesis, pinned counted definitions and source intervals, current authority grants, command journal and receipts. Explicitly reject unsupported external custodians, transformations and cross-branch transit. A complete current snapshot for these custodians is practical under the retained 256-custodian / 4096-lot / 942,753-byte bounds. It is not the whole future world or its completed temporal archive.

Store the accepted genesis once. Opening an existing save must never load fresh sample wood, repair corruption by regenerating the seed, or reset a missing ledger. Distinguish a genuinely new path from an existing empty, corrupt or unknown-schema database. Fail closed on malformed stored bytes, wrong world/schema/definition pins or inconsistent journal state. Initial trusted creation is a host operation, not a command accepting an arbitrary client snapshot.

Retain canonical snapshot bytes and exact accepted command/receipt bytes. For this small proof, retaining an initial snapshot plus a content-addressed after-snapshot per state change and commit records referencing before/after hashes is sufficient to preserve its custody history. A rejected command may reference the same before/after state. This is a simple retained starting point for later deltas/checkpoints, not a claim that full scheduler/RNG/events/outbox/history reconstruction already exists.

## Transaction sequence

1. Bound and decode command input with `CommandCodecV1`; confirm canonical bytes if the service accepts raw input. Constructing an envelope proves no authority.
2. Resolve the current trusted delivery session. Validate its principal, world membership and receipt-disclosure scope before exposing private lookup results. Acquire the one writer's serialized transaction boundary.
3. Find an existing journal entry by **(world, principal, command ID)**. Session and branch are not part of that uniqueness key. Compare the original bytes exactly, in addition to any indexing hash. Identical bytes return the original committed receipt; different bytes conflict without replacing or revealing another result. This lookup precedes fresh gameplay revision validation: a successful retry naturally carries now-stale original expected revisions.
4. For a new submission, validate current actor control, source withdrawal and destination deposit permission, plus required known/accessible target scope. Then load the committed complete bounded custody snapshot and run the Core planner. Do not validate against an unlocked client-provided snapshot.
5. Inside one explicit write transaction, reserve the next checked world commit sequence and record exact command bytes, outcome/receipt, before/after snapshot identity, any new split identity reservation, and the new canonical custody snapshot when applied. Required SQL row-count and uniqueness failures roll back the whole transaction. An authenticated, admitted gameplay rejection is also recorded once; it leaves custody state revision and hash unchanged.
6. Commit durably. Only then publish a receipt, replace any memory view or create a client-facing projection. SQLite/I/O failure is not a gameplay rejection. If commit outcome becomes uncertain, reopen/reconcile by the same command identity rather than manufacturing a fresh result or retrying under a new identity.

Use an immediate, explicit write transaction to avoid a deferred read-to-write upgrade race. Microsoft documents that a deferred transaction which cannot upgrade must be retried as a whole. [Microsoft.Data.Sqlite transactions](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/transactions).

Keep **world commit sequence** separate from **custody state revision**. Receipt sequence orders committed outcomes, including rejections; the current planner increments custody revision only for a successful state change. Later branch-wide revisions must be integrated explicitly rather than treating either number as a clock or silently substituting one for the other.

## Session, control and retry boundaries

The present `CommandEnvelope` principal/session/actor fields are claims. A bool, a publicly constructible grant containing those same claims, or `controlledActor == sourceCustodian` is not an authority check. The first local host may issue opaque live session handles from explicit trusted local setup, backed by current actor and container grants. That is a real local capability boundary; remote identity verification remains later transport work. Only trusted administration may create/revoke grants.

`OriginSessionId` is part of immutable original command bytes. A reconnect must carry a separate current delivery session. For a new command require an authorized current origin; for an old command, an explicitly authorized same-principal recovery path may return the original receipt without rewriting its origin session. Do not key deduplication by the new session or merely accept a spoofed origin. Persist any session-origin ownership needed to verify this rule, or explicitly reject cross-session recovery until it exists. Old handles should not become live again merely because the process restarted.

Check permissions before entity lookup errors, retry conflict details or full snapshot data can reveal private state. Recheck mutable grants under the same serialized authority boundary as mutation. A permitted receipt lookup need not imply fresh permission to execute the original action. Decide and test this distinction explicitly. Native UI reads require a narrowly permitted view; the internal complete custody snapshot is not a general client packet.

## Provenance, identity and source budgets

`CustodySnapshot` currently verifies local nonoverlap and same-source counted-definition consistency; it does not prove that a source existed, that all current claims were included, or that a client was entitled to create them. `SourceInterval` deliberately grants no export rights.

For the first transfer-only scope, accepted genesis must pin each finite source claim and counted definition. All later transfers must preserve the exact union of those claims. A six-unit source cannot admit a seventh unit or another overlapping origin merely because its lot ID is new. No extraction, production or branch import endpoint should bypass this boundary. Broader source reservoirs, renewable cycles and transformed provenance require their own real source ledger when introduced.

The database needs a retained occupied-identity check for split children beyond the current snapshot. The planner already rejects collisions against visible source/definition/reservation/lot/storage/actor/principal/session/command identities. Persist relevant world-known identities and insertion roles, including consumed command identities, so a later child cannot reuse a formerly present entity ID. Do not interpret matching 128-bit derived IDs or content hashes as permission to overwrite different bytes. Reserve a split child's identity in the same successful transaction.

A snapshot that includes only selected custodians cannot prove free capacity or exclusivity outside that working set. The initial store can explicitly own the complete small admitted domain. Before multiple independent custody partitions are introduced, retain a partition identity and global completeness/identity/source ownership rules. Do not retrofit a world-scale claim onto the present bounded value object.

## SQLite connection, encoding and readiness

Verify `journal_mode=WAL`, `synchronous=FULL` and foreign keys on the actual opened connection, and retain the observed native SQLite version/source ID. SQLite WAL supports one writer at a time; its transaction atomicity does not span multiple attached databases, and it is unsuitable for a shared network filesystem. Keep all eventual transit branches in this one local database. Backups require a consistent SQLite backup/snapshot operation. [SQLite WAL documentation](https://www.sqlite.org/wal.html).

`BEGIN IMMEDIATE` serializes a transaction; it does not by itself prevent two independently opened authority services. Use one lifetime owner and test a second host open is refused. For the first single-connection service, SQLite exclusive locking and disabled connection pooling are a concrete option. Route reads through the same owner; do not let an inspection connection silently become a second writer. SQLite documents its exclusive-WAL behavior and lock lifetime. [SQLite exclusive WAL](https://www.sqlite.org/wal.html#use_of_wal_without_shared_memory).

The native dependency must include the WAL-reset fix: SQLite 3.51.3 or later, or a verified fixed backport such as 3.50.7/3.44.6. A managed package version alone does not establish the native version actually loaded. Qualify each desktop export's bundled native asset. [SQLite WAL-reset fix](https://www.sqlite.org/wal.html#walreset).

Use 16-byte canonical IDs, 32-byte hashes and fixed-width canonical unsigned encodings where SQLite's signed integer range cannot represent the retained UInt64 domain. Do not cast overflowing revisions or counters into signed values or encode them as floating point. Enforce byte-length/type/uniqueness constraints, strict application decoding and checked increment on restore and commit. Bound reads before allocating the entire hostile blob: existing command, receipt and custody limits are 16,384, 1,024 and 942,753 bytes respectively.

Opening becomes ready only after schema, world identity, pinned definitions, current state, journal/receipt consistency and initial source invariants validate. Disk-full/busy/corruption cannot fall through into a successful gameplay receipt. Configure bounded backpressure rather than silently dropping accepted work or history. Distinguish recoverable storage errors from invalid gameplay.

Microsoft.Data.Sqlite's async ADO.NET methods execute synchronously. Run storage on the serialized host work loop, with a bounded queue when integrating interactive Godot input, rather than blocking its rendering callback behind an apparently asynchronous method. This is still one in-process owner. [Microsoft.Data.Sqlite async limitations](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/async).

## Required concrete proofs

| Case | Required observation |
|---|---|
| Create, transfer, dispose and reopen | Same world/actor/lot/source IDs; correct quantity and revision; original receipt bytes retained |
| Exact retry after success and after gameplay rejection | Original receipt/commit returned, no new mutation, sequence, split or reward |
| Same ID with changed quantity, actor, branch or origin bytes | Conflict; original record untouched |
| Current delivery session and permissions | Spoofed principal/session, revoked handle, wrong actor, unauthorized source/destination and foreign receipt lookup fail without leaking private state |
| Equivalent player/NPC authority path | Both submit through the same authority/planner/storage operation under their actual permissions |
| Competing transfers of the last units | One state change and one durable conflict outcome; no negative stock, overlap or extra capacity |
| Real process death before transaction commit | Reopen finds neither partial snapshot nor receipt/identity reservation |
| Real process death after commit but before acknowledgement | Reopen and exact retry return the one committed outcome |
| Mid-write injected fault and failed commit | Rollback/recovery leaves coherent old or committed state; operational errors are not recorded as gameplay rejections |
| Corrupt/unknown/oversized bytes and inconsistent rows | Open or read fails closed; no reinitialization with fresh sample content |
| Global IDs and source claims | Historical-ID collision, overlapping/out-of-budget source, wrong definition and unsupported branch cargo reject |
| Numeric boundaries and second owner | UInt64 storage is exact, overflow rejects atomically, second host fails to acquire ownership, lock releases after process death |
| Real native client and CLI | Both consume the same Hosting service; native SQLite loads from the actual packaged dependency |

Exception-based fault injection is useful but is not evidence of operating-system process death. Retain both; do not label orderly disposal as a crash test. Power-loss durability remains dependent on SQLite/VFS/OS/storage behavior beyond a process-kill fixture. The complete P01 export, full world-state, outbox, schedule/history and population workloads remain separate qualification obligations.
