"""Instrument a disposable source copy; never change the retained runtime or relax its gates.

Run only after task-owned builds/tests settle. This diagnostic has its own 90-second collection
bound and does not qualify the 30-second recovery gate. Inclusive timings overlap by design.
"""
from pathlib import Path
import argparse
import hashlib
import json
import os
import re
import shutil
import subprocess
import tempfile
import time

PROBE = r'''
using System.Diagnostics;
using System.Text.Json;
namespace OpenFantasy.Core;
public static class RecoveryCostProbe
{
    private sealed class Total { public long Calls; public long Ticks; public long Bytes; }
    private static readonly Dictionary<string, Total> Totals = [];
    static RecoveryCostProbe() => AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        File.WriteAllText(Environment.GetEnvironmentVariable("OPENFANTASY_COST_REPORT")!,
            JsonSerializer.Serialize(Totals.OrderByDescending(pair => pair.Value.Ticks).Select(pair => new
            { stage = pair.Key, calls = pair.Value.Calls,
              inclusiveMilliseconds = pair.Value.Ticks * 1000.0 / Stopwatch.Frequency,
              inclusiveThreadAllocatedBytes = pair.Value.Bytes }), new JsonSerializerOptions { WriteIndented = true }));
    public static Scope Begin(string stage) => new(stage);
    public readonly struct Scope : IDisposable
    {
        private readonly string _stage;
        private readonly long _started;
        private readonly long _allocated;
        public Scope(string stage)
        {
            _stage = stage;
            _started = Stopwatch.GetTimestamp();
            _allocated = GC.GetAllocatedBytesForCurrentThread();
        }
        public void Dispose()
        {
            long ticks = Stopwatch.GetTimestamp() - _started;
            long bytes = GC.GetAllocatedBytesForCurrentThread() - _allocated;
            if (!Totals.TryGetValue(_stage, out var total)) Totals.Add(_stage, total = new());
            total.Calls++;
            total.Ticks += ticks;
            total.Bytes += bytes;
        }
    }
}
'''


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("runtime", "dotnet", "database", "output"):
        parser.add_argument("--" + name, type=Path, required=True)
    parser.add_argument("--revision", required=True)
    args = parser.parse_args()
    runtime, dotnet, database, output = (getattr(args, name).resolve()
        for name in ("runtime", "dotnet", "database", "output"))
    if output.exists():
        raise ValueError("Use a new output directory; retain every previous measurement.")
    output.mkdir(parents=True)
    copy = Path(tempfile.mkdtemp(prefix="openfantasy-recovery-profile-"))
    report = {"status": "preparing", "source": str(runtime), "disposableCopy": str(copy),
              "limitation": "Instrumented diagnostic, not deadline or production acceptance; inclusive stages overlap.",
              "commands": []}
    inputs = []
    try:
        for folder in ("src", "content"):
            shutil.copytree(runtime / folder, copy / folder,
                            ignore=shutil.ignore_patterns("bin", "obj"))
        for name in ("Directory.Build.props", "global.json", "NuGet.Config"):
            shutil.copy2(runtime / name, copy / name)
        inputs = sorted(path.relative_to(copy) for path in copy.rglob("*") if path.is_file())
        report["sourceHashes"] = {str(path): digest(runtime / path) for path in inputs}
        points = [
            ("Core/Worlds/WorldSnapshot.cs", r"private WorldSnapshot\([\s\S]*?\n    \{", "world-root"),
            ("Core/Worlds/WorldAdmissionValidator.cs", r"public static WorldIdentityIndex Validate\([^\n]*\)\n    \{", "world-identities"),
            ("Core/Worlds/WorldResourceAdmission.cs", r"internal static void Validate\(WorldSnapshot world\)\n    \{", "resource-causes"),
            ("Core/Worlds/WorldCareAdmission.cs", r"internal static void Validate\(WorldSnapshot world\)\n    \{", "care-admission"),
            ("Core/Worlds/WorldRestorativeAdmission.cs", r"internal static void Validate\(WorldSnapshot world\)\n    \{", "ordinary-care"),
            ("Core/Worlds/WorldSourceIssuanceBaseline.cs", r"internal void ValidatePartition\(WorldSnapshot world\)\n    \{", "source-partition"),
            ("Core/Worlds/WorldSnapshotCodec.cs", r"public static byte\[\] Encode\(WorldSnapshot state\)\n    \{", "world-encode"),
            ("Core/Production/Continuation/ProductionContinuationValidation.cs", r"internal static void Validate\(ProductionSnapshot state\)\n    \{", "production-continuation"),
            ("Hosting/Worlds/WorldJournalRecovery.cs", r"private static ReplayedWorld Replay\([^\n]*\)\n    \{", "full-replay"),
            ("Hosting/Worlds/WorldCommitPreparation.cs", r"internal PreparedWorldCommit Prepare\([\s\S]*?\n    \{", "commit-preparation"),
            ("Host/RecordedWorldOperator.cs", r"internal string Inspect\(string databasePath, string commitText\)\n    \{", "inspection-command"),
        ]
        for relative, pattern, label in points:
            path = copy / ("src/OpenFantasy." + relative)
            text, count = re.subn(pattern,
                lambda match: match.group() + '\n        using var recoveryCost = OpenFantasy.Core.RecoveryCostProbe.Begin("' + label + '");',
                path.read_text())
            if count != 1:
                raise ValueError(f"Expected one insertion for {relative}, found {count}")
            path.write_text(text)
        (copy / "src/OpenFantasy.Core/RecoveryCostProbe.cs").write_text(PROBE)
        report["instrumentation"] = [{"source": source, "stage": stage} for source, _, stage in points]
        # Work on a complete copy of the failed recovery ledger, including any actual WAL.
        for suffix in ("", "-wal", "-shm"):
            path = Path(str(database) + suffix)
            if path.exists():
                shutil.copy2(path, output / ("diagnostic.sqlite3" + suffix))
        report["databaseHashes"] = {suffix: digest(Path(str(database) + suffix))
            for suffix in ("", "-wal", "-shm") if Path(str(database) + suffix).exists()}
        env = dict(os.environ, DOTNET_ROOT=str(dotnet.parent), OPENFANTASY_COST_REPORT=str(output / "costs.json"))
        def run(label, command, bound):
            entry = {"label": label, "command": list(map(str, command)), "boundSeconds": bound}
            report["commands"].append(entry)
            started = time.monotonic()
            try:
                with (output / (label + ".stdout")).open("w") as stdout, (output / (label + ".stderr")).open("w") as stderr:
                    result = subprocess.run(command, cwd=copy, env=env, stdout=stdout, stderr=stderr, timeout=bound)
                entry["exitCode"] = result.returncode
                if result.returncode:
                    raise RuntimeError(label + " failed; inspect retained output")
            finally:
                entry["elapsedSeconds"] = time.monotonic() - started
        run("build", [dotnet, "build", "src/OpenFantasy.Host/OpenFantasy.Host.csproj", "-p:RestoreLockedMode=true",
            "-m:1", "-p:UseSharedCompilation=false", "--verbosity", "quiet"], 180)
        host = copy / "src/OpenFantasy.Host/bin/Debug/net10.0/OpenFantasy.Host.dll"
        run("profile", [dotnet, host, "--inspect-recorded-world", output / "diagnostic.sqlite3", args.revision], 90)
        report["status"] = "diagnostic-complete"
    except Exception as error:
        report["status"] = "failed"
        report["error"] = repr(error)
        raise
    finally:
        if inputs:
            report["sourceUnchanged"] = report.get("sourceHashes") == {str(path): digest(runtime / path) for path in inputs}
        if "databaseHashes" in report:
            report["originalDatabaseUnchanged"] = report["databaseHashes"] == {suffix: digest(Path(str(database) + suffix))
                for suffix in ("", "-wal", "-shm") if Path(str(database) + suffix).exists()}
        (output / "result.json").write_text(json.dumps(report, indent=2) + "\n")


if __name__ == "__main__":
    main()
