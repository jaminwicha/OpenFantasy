"""Pinned scheduled-offer qualification; prepared checks do not establish acceptance."""
from collections import Counter
from check_preservation import verify as verify_preservation
from datetime import datetime, timezone
import hashlib
import os
import tempfile
import json
from pathlib import Path
import subprocess
import sys
import time
import xml.etree.ElementTree as ET


HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[4]
RUNTIME = ROOT / "Beta/Runtime"
DOTNET = "/Users/alucinor/.local/share/openfantasy/toolchains/dotnet-10.0.401/dotnet"
NAMESPACE = "{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}"


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def inputs():
    return {
        str(path.relative_to(ROOT)): digest(path)
        for directory in ("src", "tests", "client/scripts")
        for path in (RUNTIME / directory).rglob("*")
        if path.is_file() and path.suffix in (".cs", ".csproj", ".props", ".targets", ".json")
        and not any(part in ("bin", "obj", ".godot") for part in path.parts)
    }


def assemblies():
    return {
        str(path.relative_to(ROOT)): digest(path)
        for directory in ("src", "tests")
        for path in (RUNTIME / directory).glob("*/bin/Debug/net8.0/OpenFantasy*.dll")
    }


def run():
    kind = sys.argv[1]
    if kind not in ("build", "focused", "regression"):
        raise ValueError("Select build, focused or regression.")
    label = "session-" + kind + "-" + datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    report = {"kind": kind, "sourceSha256": inputs(), "limitSeconds": 240 if kind == "build" else 600}
    start = time.monotonic()
    try:
        report["preservation"] = verify_preservation()
        if kind == "build":
            command = [DOTNET, "build", "Beta/Runtime/OpenFantasy.sln", "--no-restore", "-m:1",
                       "-p:UseSharedCompilation=false", "--verbosity", "quiet"]
        else:
            build_path = Path(sys.argv[2]).resolve()
            build = json.loads(build_path.read_text())
            assert build["kind"] == "build" and build["accepted"], "Require a successful pinned build."
            assert build["sourceSha256"] == report["sourceSha256"], "Sources changed since the supplied build."
            report["assemblySha256"] = assemblies()
            assert report["assemblySha256"] and build["assemblySha256"] == report["assemblySha256"], "Built assemblies changed."
            report["buildEvidence"] = str(build_path.relative_to(ROOT))
            report["buildEvidenceSha256"] = digest(build_path)
            command = [DOTNET, "test", "Beta/Runtime/tests/OpenFantasy.Hosting.Tests/OpenFantasy.Hosting.Tests.csproj",
                       "--no-build", "--no-restore", "--logger", "trx;LogFileName=" + label + ".trx",
                       "--results-directory", str(HERE)]
            selection = "FullyQualifiedName~ScheduledMaterial" if kind == "focused" else (
                "FullyQualifiedName~OpenFantasy.Hosting.Tests.Simulation|FullyQualifiedName~MaterialProposal|"
                "FullyQualifiedName~RestorativeActionOfferTests|FullyQualifiedName~CareActionOfferTests")
            command += ["--filter", selection]
        report["command"] = command
        environment = dict(os.environ)
        with (HERE / (label + ".log")).open("x") as log:
            result = subprocess.run(command, cwd=ROOT, stdout=log, stderr=subprocess.STDOUT,
                                    timeout=report["limitSeconds"], env=environment)
        report["exitCode"] = result.returncode
        assert result.returncode == 0, "Command failed; preserve its log."
        if kind == "build":
            report["assemblySha256"] = assemblies()
            assert report["assemblySha256"], "No compiled runtime/test assemblies."
        else:
            rows = ET.parse(HERE / (label + ".trx")).findall(".//" + NAMESPACE + "UnitTestResult")
            report["outcomes"] = dict(Counter(row.attrib["outcome"] for row in rows))
            report["distinctTestIds"] = len({row.attrib["testId"] for row in rows})
            assert rows and all(row.attrib["outcome"] == "Passed" for row in rows), "Empty, failed or skipped selection."
            assert report["assemblySha256"] == assemblies(), "Assemblies changed during tests."
    except Exception as error:
        report["error"] = repr(error)
    report["elapsedSeconds"] = round(time.monotonic() - start, 3)
    report["sourceUnchanged"] = report["sourceSha256"] == inputs()
    report["accepted"] = not report.get("error") and report.get("exitCode") == 0 and report["sourceUnchanged"]
    output = HERE / (label + ".json")
    output.write_text(json.dumps(report, indent=2) + "\n")
    print(json.dumps({key: value for key, value in report.items() if key not in ("sourceSha256", "assemblySha256", "command")}, indent=2))
    print(output)
    return 0 if report["accepted"] else 1


if __name__ == "__main__":
    sys.exit(run())
