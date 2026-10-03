"""Collect exact qualification outcomes without turning partial or failed checks into acceptance."""
import hashlib
import importlib.util
import json
from pathlib import Path
import sys

sys.dont_write_bytecode = True
PACKET = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location("qualification", PACKET / "check.py")
check = importlib.util.module_from_spec(spec)
spec.loader.exec_module(check)
check.preserve()
source = check.sources()
binaries = check.base.assemblies()
reports = {}
passing = set()
failed = []
core_baseline = json.loads((PACKET / "CORE_INHERITANCE_BASELINE.json").read_text())
core_family = ("Beta/Runtime/src/OpenFantasy.Core/", "Beta/Runtime/tests/OpenFantasy.Core.Tests/")
core_reuse = {
    "report": core_baseline["report"],
    "reportMatchesRecordedHash": check.base.sha(PACKET / core_baseline["report"]) == core_baseline["reportSha256"],
    "sourcesMatch": {k: v for k, v in source.items() if k.startswith(core_family)} == core_baseline["sourceFamily"],
    "dependencyFilesMatch": all(Path(k).is_file() and check.base.sha(k) == v for k, v in core_baseline["dependencyFiles"].items()),
    "rationale": core_baseline["rationale"]
}
core_reuse["accepted"] = all(core_reuse[k] for k in ("reportMatchesRecordedHash", "sourcesMatch", "dependencyFilesMatch"))
for mode in ("build", "new-hosting", "connected-server", "hosting-compatibility", "server-compatibility", "core", "native-compatibility"):
    path = Path(json.loads((PACKET / (".latest-" + mode + ".json")).read_text())["path"])
    report = json.loads(path.read_text())
    same = report.get("source") == source and report.get("binaries") == binaries
    dependencies_match = same or (mode == "core" and core_reuse["accepted"] and path == PACKET / core_baseline["report"])
    reports[mode] = {
        "path": str(path.relative_to(PACKET)), "sha256": check.base.sha(path),
        "accepted": report["accepted"], "matchesCurrentSourceAndBinaries": same,
        "matchesActualDependencies": dependencies_match,
        "seconds": report["seconds"], "outcomes": report.get("outcomes"),
        "expectedSkips": report.get("expectedSkips"), "unrunCases": report.get("unrunCases", [])
    }
    for case in report.get("cases", []):
        if case["outcome"] == "Passed" and dependencies_match:
            passing.add(case["name"])
        elif case["outcome"] == "Failed":
            failed.append({"mode": mode, "case": case["name"]})

retained = []
for path in sorted((PACKET / "qualification").glob("*/report.json")):
    report = json.loads(path.read_text())
    if not report["accepted"]:
        retained.append({"path": str(path.relative_to(PACKET)), "sha256": check.base.sha(path),
                         "mode": report["mode"], "outcomes": report.get("outcomes"), "error": report.get("error")})

preserved = json.loads((PACKET / "PRESERVATION.json").read_text())
result = {
    "allSelectedQualificationsPassed": all(r["accepted"] and r["matchesActualDependencies"] for r in reports.values()),
    "connectedManagedJourneyQualified": reports["connected-server"]["accepted"] and reports["connected-server"]["matchesCurrentSourceAndBinaries"],
    "selectedNativeJourneyQualified": False,
    "fullCoPlayerFeatureAccepted": False, "movementTimingGateClosed": False,
    "productionHistoryLatencyGateClosed": False,
    "sourceHead": preserved["sourceHead"], "preChangeCheckpoint": preserved["checkpoint"],
    "deliveryProofWipCheckpoint": json.loads((PACKET / "WIP_CHECKPOINT.json").read_text()),
    "milestone": "codex/milestone-connected-player-control-20261003",
    "protectedCounts": {name: len(paths) for name, paths in preserved["protected"].items()},
    "permittedTestRefactors": preserved.get("permittedTestRefactors", {}),
    "predecessorIndexSha256": preserved["predecessorIndexSha256"],
    "checkerSha256": check.base.sha(PACKET / "check.py"),
    "distinctCasesPassingOnVerifiedDependencies": len(passing), "coreDependencyInheritance": core_reuse,
    "reports": reports, "unresolvedSelectedFailures": failed, "retainedFailedAttempts": retained,
    "externalClaudeRequestExecuted": False,
    "limits": ["Native compatibility covers original home-player journeys, not selected character UI.",
               "Scripted malformed-peer tests prove protocol validation, not live restoration gameplay.",
               "Fresh passes do not explain earlier retained latency failures or establish production reliability."]
}
(PACKET / "FINAL_CHECKS.json").write_text(json.dumps(result, indent=2) + "\n")
print(json.dumps({key: result[key] for key in ("allSelectedQualificationsPassed", "connectedManagedJourneyQualified", "distinctCasesPassingOnVerifiedDependencies", "unresolvedSelectedFailures")}, indent=2))
