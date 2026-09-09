# P00 independent build and toolchain review

Review date: 8 September 2026. Reviewer: `p00_toolchain`, a subagent of the central implementation task. The reviewer did not author the runtime, host, client, build configuration or verification script. This review owns only this file. Toolchain inventory and primary-source lookups were read-only; implementation and installations were performed by the central task.

## Scope and current disposition

This is a bounded review of project isolation, naming, toolchain reproducibility, clean-checkout instructions and the development verification harness. It does not certify gameplay, exported applications, performance, rendering quality, complete P00 exit or the nine production qualification gates. The central task's final executed evidence must remain distinct from this review.

The inspected project graph is coherent: one engine-independent `OpenFantasy.Core` library targets net8.0, the plain `OpenFantasy.Host` executable targets net10.0, and the Godot C# client targets net8.0. The exact SDK candidate is 10.0.401; the exact editor candidate is 4.6.stable.mono.official.89cea1439 with Godot.NET.Sdk/4.6.0. These are candidate pins, not an accepted production export pairing. Runtime observations must come from execution, not target-framework inference.

## Findings and corrections

| ID | Finding | Disposition verified by inspection |
|---|---|---|
| BUILD-01 | The generated solution mapped client Release builds to Debug, potentially mislabeling a mixed configuration as a release build. | Corrected: solution Release now maps the client to ExportRelease; unused x86/x64 solution configurations were removed. This mapping does not itself prove a release export. |
| BUILD-02 | A zero exit code from `dotnet test` could falsely pass when no tests executed or stale results were inspected. | Corrected: each run has a unique TRX directory; a fresh report must contain nonzero execution, total = executed = passed, and no failed/error/timeout/aborted counts. |
| BUILD-03 | Godot editor import output was not checked for errors when the process returned zero. A later narrow bootstrap need not expose every failed import. | Corrected: both import and client outputs are inspected for Godot error markers before the verification report may pass. |
| BUILD-04 | Client net8.0 targeting and installation of an 8.x runtime did not prove which runtime Godot loaded; its plugin loader permits major-version roll-forward. | Corrected: both host and client emit `RuntimeInformation.FrameworkDescription`; verification retains each observation separately. |
| BUILD-05 | Source hashes captured before execution could become stale while other work or editor import changed authored inputs. | Corrected for original inputs: verification checks their hashes again before passing. Finalization also records all final input hashes and newly generated UID files, while unexpected new authored files cause failure. |
| BUILD-06 | The naming scan is an optional caller argument, so an ordinary verification command must not imply it enforced the owner's naming policy. | The authored source scan performed by this reviewer found no forbidden planning codename in the runtime tree. The verifier now records namingScanRequested and namingScanPassed explicitly; the inspected executed report has both true. |

## Isolation and repository checks

- The predecessor project now sets `EnableDefaultCompileItems=false` and explicitly includes `src/**/*.cs`. Every currently tracked predecessor C# source was under that tree; the changed boundary does not omit an existing tracked C# file.
- The planning directory's `.gdignore` prevents the predecessor Godot import from traversing the nested runtime. The new client has its own resource root and explicit project references; core and host source do not live under the client resource tree.
- The solution, assemblies, project metadata, namespaces and authored runtime content use stable OpenFantasy names. Historical specification paths retain their provenance outside the runtime.
- NuGet sources are explicitly configured and project lock files are present. The verification command uses locked restore, exact SDK/editor checks, a private `DOTNET_ROOT`, and an explicit editor executable rather than the machine's older standard `godot` command.
- The repository's existing global ignore rule for `export_presets.cfg` was reported. A scoped exception or explicit tracked add is needed when presets are introduced during export qualification. No export preset or template completion is claimed here.

## Executed independently versus inspected

Independently executed: read-only installed SDK/runtime/editor version commands; machine/OS inventory; source/name and compile-location searches; parsing current JSON, project XML and verifier Python syntax without generating output files. Official Microsoft release metadata and Godot release asset metadata were queried to verify archive URLs and published hashes. No game build, test run, export or native rendering pass was executed by this reviewer; central-task outputs can be inspected subsequently without changing their authorship.

Inspected: project/solution configuration, lock files, host and client bootstraps, toolchain manifest and verifier implementation. Root-owned code was not modified by this reviewer.

## Official toolchain provenance

The [Microsoft .NET 10 release metadata](https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/10.0/releases.json) identifies SDK 10.0.401 and bundled runtime 10.0.12. The [.NET 8 metadata](https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/8.0/releases.json) identifies runtime 8.0.31; both current releases are dated 8 September 2026. The archive hashes in the runtime toolchain manifest match these sources and the [Godot 4.6 release asset metadata](https://api.github.com/repos/godotengine/godot-builds/releases/tags/4.6-stable).

The [Godot 4.6 project generator](https://github.com/godotengine/godot/blob/4.6-stable/modules/mono/editor/GodotTools/GodotTools.ProjectEditor/ProjectGenerator.cs) defaults to net8.0 for desktop and net9.0 for Android. [Platform documentation](https://docs.godotengine.org/en/4.6/tutorials/scripting/c_sharp/index.html) documents desktop C# support while retaining experimental mobile qualifications. These documents do not establish a passing application pairing. The [macOS launch documentation](https://docs.godotengine.org/en/4.6/tutorials/export/running_on_macos.html) notes quarantine/path randomization can break app-relative dependencies; no security bypass or system-wide setting change was performed or recommended by this review.

When adding the older runtime archive to a private SDK 10 installation, preserve the SDK 10 launcher instead of overwriting it with non-versioned files from the runtime archive. Verify the final private executable and installed runtime list after extraction. This installation detail was reported to the central task; installation was not performed by the reviewer.

## Final disposition and inspected execution evidence

No unresolved finding in this bounded review blocks the retained local foundation increment. All six observations above have explicit inspected dispositions. This is not a declaration that all P00 exits or production gates passed.

The runtime README now supplies clean-checkout verification commands using exact executable paths, explains the separate SDK/framework/runtime roles, requires the .NET editor, distinguishes Debug/ExportRelease from distributable export, documents optional naming enforcement and identifies the local macOS archive coverage. Its commands were inspected for consistency with the script and project graph. A fresh clone on another machine was not created or executed by this reviewer.

The central task's verification report, independently read after completion, records a run from **2026-09-09 03:11:56 UTC to 03:12:09 UTC** (8 September local time). All ten recorded steps returned zero; the fresh TRX independently read by this reviewer contains **46 total, 46 executed, 46 passed**, with no failing or unexecuted counters. Host and client both observed **.NET 10.0.12**, although the client targets net8.0. They agreed on the foundation identity, RNG sample and midnight crossing. The report records both naming flags true and the generated `client/scripts/Bootstrap.cs.uid`; its final input hashes matched the files when independently checked. The reviewer did not execute those build/tests or the Godot process.

The inspected report's SHA256 is `710ded9ab6c681f811efefe7ac048e19728df7f19513ed51a3829a7c0cefb8fe`. Its working path is `../../Runtime/artifacts/verification.json` relative to this review directory; artifacts are intentionally ignored, so retain any delivery copy with this hash in the implementation evidence package. A later run can supersede that working report and must carry its own timestamp/hash. The central task separately reported a zero-warning, zero-error Release solution build with client ExportRelease output; this review verified the mapping but does not attribute that build execution to the reviewer.

Independent final read-only checks parsed verifier Python syntax, matched the execution report's final input hashes, reread the fresh TRX counters and scanned the current authored runtime tree for the prohibited planning codename. None was found.

## Exact reviewed build input snapshot

The following hashes bind the reviewed build/toolchain/host/client inputs. They exclude this review, independent core internals and other reviewers' work. A later relevant edit requires an affected-input review; these hashes do not confer approval on unseen changes.


| Reviewed input | SHA256 |
|---|---|
| [OpenFantasy.csproj](../../../OpenFantasy.csproj) | `cd1778beae10b76321ff89cc14fffd64fe490cbf5c5f96ff95d121fd0b86f1df` |
| [.gdignore](../../.gdignore) | `01ba4719c80b6fe911b091a7c05124b64eeece964e09c058ef8f9805daca546b` |
| [README.md](../../Runtime/README.md) | `b9adf111c3e5b34426587b15193db0e77d067a39bd855e3913bb941a7d1889a3` |
| [global.json](../../Runtime/global.json) | `4e7d5022b2a5a8d02bb951bb264a4c8613eabc332572a0af5f9743d23f23d84e` |
| [toolchain.json](../../Runtime/toolchain.json) | `1bdc42ca35759ef8ce2f474cbed9eb2c8105c717dfb2a7a75a7cb0dc5ee76fae` |
| [Directory.Build.props](../../Runtime/Directory.Build.props) | `745a005e017d4849ed4d4a0f247adc057ed5593e50d9b0d6254f8dc32bfed680` |
| [NuGet.Config](../../Runtime/NuGet.Config) | `cb93c65e28718aa9075fce221e9f9cea9e72d3fe1b30462383448a591f5b3aa1` |
| [.gitignore](../../Runtime/.gitignore) | `20f6bd059c6941a2fc061e11c31d71e5582259be0712a91675ba3bd314d677f6` |
| [OpenFantasy.sln](../../Runtime/OpenFantasy.sln) | `d418815bfc121f4cecc598ae3bdc8c757819dbf04ac5b7c43dad87bd7bff3c87` |
| [verify.py](../../Runtime/tools/verify.py) | `0d5fa9ab69702999a8a11cba035a5ec0964e125f207bf59eea696e3cbe1d8f80` |
| [OpenFantasy.Core.csproj](../../Runtime/src/OpenFantasy.Core/OpenFantasy.Core.csproj) | `470b8bbe4ee9f686e1144d857fa279e6409a3312b4f4fd6b522fc9dd0aa7e96a` |
| [OpenFantasy.Host.csproj](../../Runtime/src/OpenFantasy.Host/OpenFantasy.Host.csproj) | `aa484c2f4377e3c3cd54accb6ff49161b05067a5c567dda0dc1ed7a1dc703f46` |
| [packages.lock.json](../../Runtime/src/OpenFantasy.Host/packages.lock.json) | `3c60d8a6cfb69eef72a11091651f3ac3d25f330f704a9494bf9921cfabe9532b` |
| [Program.cs](../../Runtime/src/OpenFantasy.Host/Program.cs) | `a438159a750be2f53ef519ed2a059b9d4eb5b5fa1e89ecfef8488e1cf40d83ef` |
| [OpenFantasy.Client.csproj](../../Runtime/client/OpenFantasy.Client.csproj) | `599eb21261e622ec3022df815f8b90b0418fcbc72dee88893c6ebdffa8012daf` |
| [packages.lock.json](../../Runtime/client/packages.lock.json) | `91eadbd1f92674f511963740fc9b412eaaf9ae8f48665b1b11b90fe50622f5b7` |
| [project.godot](../../Runtime/client/project.godot) | `a170d5c6b9e7fde1e8d68c23fe1c3b579096e1f37e27c2d0416d28fabcd9539a` |
| [Bootstrap.cs](../../Runtime/client/scripts/Bootstrap.cs) | `e65ae7aa5becd81c41124599bb2efc70e4f4ce473c3f86afbc7fbe5022af7555` |
| [Bootstrap.cs.uid](../../Runtime/client/scripts/Bootstrap.cs.uid) | `be9037dcd46b86baa00c217a9274da9124cbf4d577bbd1b3ebacfcfdc1ba65b4` |
| [Bootstrap.tscn](../../Runtime/client/scenes/Bootstrap.tscn) | `fe64d7438c50c73ecd288d2be37b687ef29aedce586357967d6a2d3771ffedef` |
