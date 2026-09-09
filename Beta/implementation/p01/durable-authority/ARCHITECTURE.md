# Shared durable counted authority decision

9 September 2026. Retained implementation decision for the first P01 authority proof.

## Assembly boundary

Keep the dependency-free C# Core. Add one net8.0 `OpenFantasy.Hosting` adapter library because both the Godot process and dedicated executable consume the same actual SQLite-backed authority. This is a real two-consumer boundary, not a per-feature assembly, IPC bridge or second authority. The net10.0 executable stays a thin local operator/router. Godot owns presentation and moves synchronous IO off its frame callbacks. The source `TrustedCommandDelivery` type is trusted input from a future authentication/session adapter; passing IDs does not authenticate a network connection.

The reviewed single-writer architecture remains unchanged in direction. One connection owns one local world for its lifetime; in-memory state is installed only after a successful transaction. Restart reconciles accepted genesis, all journal entries, head and retained identities before returning an authority. Failures never regenerate the world or erase evidence. Exact schema admission and one-row write assertions prevent a modified schema from silently suppressing a required commit component.

The current schema is explicitly counted-domain v1. It commits command/receipt/state/identity rows because those are the implemented participants. Events, due work, RNG draws, outbox and terrain are not written as empty placeholders or claimed to be durable already. Their integration must extend the same atomic authority with an explicit tested schema migration. Current control/source admission is immutable after genesis; replacing a connection/session grant is future real work. The initial recovery path performs a full replay with retained identity history; production-sized checkpoint, memory, compaction and recovery-time budgets remain unqualified.

## Dependency and durability research

Microsoft's provider exposes explicit transactions and documents deferred transaction behavior; this host starts an immediate write transaction to serialize acceptance. [Microsoft transaction guidance](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/transactions)

SQLite operations exposed as asynchronous methods by Microsoft.Data.Sqlite execute synchronously, so an async method name cannot make frame-thread DB work safe. The Godot qualification therefore owns its store lifecycle in one background task. A live host will need its serialized command work loop, bounded queue and shutdown semantics. [Microsoft async guidance](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/async)

WAL with `synchronous=FULL` is the selected local durability policy; its guarantees depend on the filesystem and storage honoring sync. We measure actual process death before/after commit here, not a power cut. [SQLite synchronous policy](https://sqlite.org/pragma.html#pragma_synchronous)

The SQLite WAL-reset correction is a native-engine concern. A managed provider version alone cannot demonstrate that the corrected native engine loaded. The provider pins are `Microsoft.Data.Sqlite.Core 10.0.12`, `SQLitePCLRaw.bundle_e_sqlite3 3.0.5`, and `SQLite 3.53.4`; the v3 bundle depends on the separate SQLite native package. Restore locks retain the resolved graph and content hashes. Actual native version/source ID and WAL/FULL/FK/locking settings are queried in both dedicated and Godot processes. [SQLite WAL-reset history](https://www.sqlite.org/wal.html#walreset), [bundle 3.0.5 dependencies](https://www.nuget.org/packages/SQLitePCLRaw.bundle_e_sqlite3/3.0.5), [SQLitePCLRaw v3 transition](https://github.com/ericsink/SQLitePCL.raw/blob/main/v3.md)

The passing local native version is 3.53.4, source ID `2026-07-24 19:02:57 bf7c7f30031888f4e796e429ab3978879485813aaca6f641c7b33e4e09459bcc`. This macOS ARM64 result does not establish Windows/Linux Godot export packaging. Those runs must reproduce actual loaded native versions and save behavior on their supported targets.
