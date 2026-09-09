# OpenFantasy runtime

This is the retained development runtime: a portable C# core, shared durable hosting library, headless executable, Godot C# adapter and executable tests. A finite counted inventory now commits real transfers, persists receipts and recovers from abrupt process death. Deterministic stacked terrain can preserve excavation in its own canonical snapshot. The connected playable world, multiplayer sessions and playable source import remain pending. The [durability record](../implementation/p01/README.md) and [preparation packet](../implementation/p00/README.md) distinguish measured increments from full phase exits.

## Build and verify

Use Python 3, the exact .NET SDK in `global.json`, and the .NET edition of Godot pinned in `toolchain.json`. The SDK executable must have access to the required managed runtimes. A standard non-.NET Godot editor cannot run this project.

From this directory:

```sh
python3 tools/verify.py --dotnet /absolute/path/to/dotnet --godot /absolute/path/to/Godot
```

The verifier checks the actual tool versions, independently regenerates all retained independent numeric, command, package, custody, scheduling, history, exposure, anatomy, production, observation, language, authority and terrain fixtures, restores locked dependencies, builds Debug, requires every discovered core and hosting test to execute and pass, exercises host contract inspection against valid/malformed inputs and retained source-packet hashes, imports the client, and runs a headless Godot smoke check. It compares host/client primitive results and records the managed runtime observed inside each process. It also kills the dedicated host before/after an actual SQLite commit, reopens and retries without duplication, refuses a competing process, and checks matching durable results and actual native SQLite settings inside Godot. A zero process exit does not excuse missing tests or Godot error output. Results and raw process output appear in `artifacts/verification.json`, with a fresh TRX report for each run. Optional `--forbidden-token TEXT` checks text input contents and all input paths for a prohibited name; binary assets are hashed and reported separately; the report states whether that scan was requested. This remains local binding evidence, not a performance, rendering, networking or desktop-export qualification.

The portable test suite can run independently:

```sh
dotnet restore tests/OpenFantasy.Core.Tests/OpenFantasy.Core.Tests.csproj --locked-mode
dotnet test tests/OpenFantasy.Core.Tests/OpenFantasy.Core.Tests.csproj --no-restore
```

To open the development harness, build Debug and open `client/project.godot` with the pinned .NET editor. Put the selected SDK directory on PATH and set DOTNET_ROOT to its runtime root before launching the editor; this avoids inadvertently selecting another project's SDK/runtime. The harness is explicitly labeled development work and is not an approved art or gameplay sample.

## Project boundaries

- `src/OpenFantasy.Core`: portable deterministic primitives, immutable command/receipt contracts, pinned package metadata, finite counted custody candidates, calendar queues and exact historical request boundaries, anatomy/condition definitions, material recipe validation, experienced-time arithmetic, scoped resource-knowledge projection and actor-owned comprehension profiles, finite source/control admission and seeded stacked terrain with retained excavation. It has no engine or third-party package dependency. See its [byte and arithmetic contract](src/OpenFantasy.Core/CONTRACTS.md).
- `src/OpenFantasy.Hosting`: shared net8.0 SQLite adapter, durable counted command stream, identity history and strict journal recovery. The same implementation runs embedded and dedicated, with exclusive world ownership and no IPC. It has no Godot dependency.
- `src/OpenFantasy.Host`: the retained headless entry point. `--inspect-foundation` exercises the same core. `--inspect-command <command.bin>` validates a bounded canonical envelope, `--inspect-package <manifest.json> <payload-file>` validates metadata and payload bytes, and `--inspect-custody <snapshot.bin>` checks counted snapshot invariants. Those inspection diagnostics issue no gameplay receipts. Local world operations below run the real durable counted authority; network authentication, session grants and gameplay projections remain pending.
- `client`: the independent Godot project and C# presentation adapter. Godot owns rendering, not authoritative outcomes.
- `tests/OpenFantasy.Core.Tests`: independently generated reference vectors and behavior/edge tests.
- `tests/OpenFantasy.Hosting.Tests`: actual temporary SQLite worlds, player/NPC transfers, concurrency, restart, corruption and independent critic regressions.
- `content`: the first retained source dependency packet and its versioned manifest. Original records/assets remain preservation inputs; playable rule conversion and native presentation are pending.
- `tools`: reproducible local verification. Build artifacts stay untracked.

Only the client references the engine. The solution's Debug configuration builds all development artifacts. Release maps the client to Godot's **ExportRelease** and the ordinary .NET projects to Release. Building either configuration does not create a distributable Godot export; actual templates, export presets and supported-device execution remain required. In particular, a macOS build does not establish Windows/Linux support.

## Candidate toolchain and portability

The current SDK is 10.0.401. Core, Hosting, tests and client target net8.0; the headless entry point targets net10.0. These are distinct from the managed runtime Godot actually loads. The local candidate installation contains runtimes 8.0.31 and 10.0.12, and the smoke report records which was selected. Godot.NET.Sdk is pinned to 4.6.0 with locked NuGet dependencies. Desktop framework/export selection must be qualified in P01; net8.0's approaching support end makes that an early decision, not a production support promise.

`toolchain.json` retains verified official download metadata for the initial macOS ARM64 machine and candidate export templates. Install tools side by side. When adding an older runtime to a newer SDK directory, retain the SDK's root launcher and copy only the runtime's versioned `shared/Microsoft.NETCore.App` and `host/fxr` directories. Download checksums prove archive identity; actual builds/runs prove only their recorded exercised paths. Other OS/architecture packages and mobile workloads require their own qualification.

Names, persisted tags and namespaces are stable product/domain identifiers. Temporary planning names do not belong in code or project metadata. Keep future domain models in a few clear assemblies; do not generate an assembly or empty interface for each design concept.

## Local durable authority

These commands require local operator access to a database and canonical accepted input files. Principal/session arguments describe that operator's trusted local delivery context; they do not authenticate remote clients.

```sh
dotnet run --project src/OpenFantasy.Host -- --create-world /absolute/world.sqlite /absolute/genesis.bin
dotnet run --project src/OpenFantasy.Host -- --execute-local-command /absolute/world.sqlite /absolute/command.bin PRINCIPAL_ID SESSION_ID
dotnet run --project src/OpenFantasy.Host -- --inspect-world /absolute/world.sqlite
```

Creation refuses every existing file. Open rejects unknown/modified schema, corrupt snapshots, discontinuous or unreproducible command journals and incomplete historical identity reservations. Each fresh valid delivery atomically records its command, receipt, resulting state and reserved IDs before returning a receipt. Rejected gameplay decisions consume a commit sequence while leaving custody revision unchanged. Exact retries return the original result; changed content under the same key refuses. IO or uncertain commit failures require closing/reopening and retrying the exact command.

The pinned SQLite adapter uses Microsoft.Data.Sqlite.Core10.0.12, SQLitePCLRaw.bundle_e_sqlite3 3.0.5 and native SQLite3.53.4. Verify actual `sqlite_version()`/`sqlite_source_id()` on every supported exported target. A local full journal replay is the initial correctness path; checkpoint cadence, storage growth and recovery budgets still need measurement. The counted schema is version1 and rejects schema changes without an explicit migration. It does not yet persist terrain or body states, scheduled work, events/outbox, control rebinding or network sessions. Avoid invoking synchronous database work on a Godot frame callback; the retained native diagnostic owns its entire lifecycle in a background task.
