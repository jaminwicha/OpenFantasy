# OpenFantasy runtime

This is the retained development foundation: a portable C# library, headless host entry point, Godot C# adapter and executable tests. It does not yet run a world, host multiplayer sessions, import the original game or implement gameplay. The [implementation packet](../implementation/p00/README.md) records the complete scope and outstanding phase exits.

## Build and verify

Use Python 3, the exact .NET SDK in `global.json`, and the .NET edition of Godot pinned in `toolchain.json`. The SDK executable must have access to the required managed runtimes. A standard non-.NET Godot editor cannot run this project.

From this directory:

```sh
python3 tools/verify.py --dotnet /absolute/path/to/dotnet --godot /absolute/path/to/Godot
```

The verifier checks the actual tool versions, independently regenerates frozen numeric/identity/RNG fixtures, restores locked dependencies, builds Debug, requires every discovered core test to execute and pass, imports the client, and runs a headless Godot smoke check. It compares host/client primitive results and records the managed runtime observed inside each process. A zero process exit does not excuse missing tests or Godot error output. Results and raw process output appear in `artifacts/verification.json`, with a fresh TRX report for each run. Optional `--forbidden-token TEXT` checks authored file contents for a prohibited name; the report states whether that scan was requested. This remains local binding evidence, not a performance, rendering, networking or desktop-export qualification.

The portable test suite can run independently:

```sh
dotnet restore tests/OpenFantasy.Core.Tests/OpenFantasy.Core.Tests.csproj --locked-mode
dotnet test tests/OpenFantasy.Core.Tests/OpenFantasy.Core.Tests.csproj --no-restore
```

To open the development harness, build Debug and open `client/project.godot` with the pinned .NET editor. Put the selected SDK directory on PATH and set DOTNET_ROOT to its runtime root before launching the editor; this avoids inadvertently selecting another project's SDK/runtime. The harness is explicitly labeled development work and is not an approved art or gameplay sample.

## Project boundaries

- `src/OpenFantasy.Core`: portable deterministic primitives, with no engine or third-party package dependency. See its [byte and arithmetic contract](src/OpenFantasy.Core/CONTRACTS.md).
- `src/OpenFantasy.Host`: the retained headless entry point. `--inspect-foundation` exercises the same core; durable authority and networking follow in subsequent increments.
- `client`: the independent Godot project and C# presentation adapter. Godot owns rendering, not authoritative outcomes.
- `tests/OpenFantasy.Core.Tests`: independently generated reference vectors and behavior/edge tests.
- `tools`: reproducible local verification. Build artifacts stay untracked.

Only the client references the engine. The solution's Debug configuration builds all development artifacts. Release maps the client to Godot's **ExportRelease** and the ordinary .NET projects to Release. Building either configuration does not create a distributable Godot export; actual templates, export presets and supported-device execution remain required. In particular, a macOS build does not establish Windows/Linux support.

## Candidate toolchain and portability

The current SDK is 10.0.401. Core, tests and client target net8.0; the headless entry point targets net10.0. These are distinct from the managed runtime Godot actually loads. The local candidate installation contains runtimes 8.0.31 and 10.0.12, and the smoke report records which was selected. Godot.NET.Sdk is pinned to 4.6.0 with locked NuGet dependencies. Desktop framework/export selection must be qualified in P01; net8.0's approaching support end makes that an early decision, not a production support promise.

`toolchain.json` retains verified official download metadata for the initial macOS ARM64 machine and candidate export templates. Install tools side by side. When adding an older runtime to a newer SDK directory, retain the SDK's root launcher and copy only the runtime's versioned `shared/Microsoft.NETCore.App` and `host/fxr` directories. Download checksums prove archive identity; actual builds/runs prove only their recorded exercised paths. Other OS/architecture packages and mobile workloads require their own qualification.

Names, persisted tags and namespaces are stable product/domain identifiers. Temporary planning names do not belong in code or project metadata. Keep future domain models in a few clear assemblies; do not generate an assembly or empty interface for each design concept.
