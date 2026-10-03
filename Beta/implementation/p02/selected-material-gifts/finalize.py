"""Verify selected-gift evidence against the actual source, binaries and preserved inputs."""
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
preserved = json.loads((PACKET / "PRESERVATION.json").read_text())
project = "Beta/Runtime/tests/OpenFantasy.Hosting.Tests/OpenFantasy.Hosting.Tests.csproj"
original = check.ROOT / preserved["permittedTestRefactors"][project]["original"]
addition = '    <Compile Include="../../client/scripts/FarmInventoryPresentation.cs" Link="Client/FarmInventoryPresentation.cs" />\n'
current_project = (check.ROOT / project).read_text()
assert current_project.count(addition) == 1
assert (check.ROOT / project).read_bytes().replace(addition.encode(), b"") == original.read_bytes()

reports = {}
passing = set()
failed = []
expected_counts = {"build": None, "client-managed": 221, "native-gifts": 1,
                   "native-selection": 1, "native-roster": 1, "native-compatibility": 2,
                   "core": 1885}
for mode, count in expected_counts.items():
    path = Path(json.loads((PACKET / (".latest-" + mode + ".json")).read_text())["path"])
    report = json.loads(path.read_text())
    same = report.get("source") == source and report.get("binaries") == binaries
    same_checker = (report["checkerSha256"] == check.base.sha(PACKET / "check.py")
                    and report["baseCheckerSha256"] == check.base.sha(PACKET.parent / "shared-player-sessions/check.py"))
    tool = report.get("nativeTool")
    same_tool = not tool or check.base.sha(Path(tool["path"])) == tool["sha256"]
    correct_count = count is None or report.get("outcomes") == {"Passed": count}
    reports[mode] = {
        "path": str(path.relative_to(PACKET)), "sha256": check.base.sha(path),
        "accepted": report["accepted"], "matchesCurrentSourceAndBinaries": same,
        "matchesCurrentCheckerAndTool": same_checker and bool(same_tool),
        "expectedOutcomes": correct_count, "seconds": report["seconds"],
        "tracing": report["tracing"], "outcomes": report.get("outcomes"),
        "unrunCases": report.get("unrunCases", [])
    }
    for case in report.get("cases", []):
        if case["outcome"] == "Passed" and same:
            passing.add(case["name"])
        elif case["outcome"] == "Failed":
            failed.append({"mode": mode, "case": case["name"]})

retained = []
for path in sorted((PACKET / "qualification").glob("*/report.json")):
    report = json.loads(path.read_text())
    if not report["accepted"]:
        retained.append({"path": str(path.relative_to(PACKET)), "sha256": check.base.sha(path),
                         "mode": report["mode"], "outcomes": report.get("outcomes"), "error": report.get("error")})

native = (PACKET / reports["native-gifts"]["path"]).parent / "native/selected-gifts"
journey = json.loads((native / "journey.json").read_text())
images = json.loads((PACKET / "INSPECTED_IMAGES.json").read_text())["images"]
for item in images:
    assert check.base.sha(PACKET / item["path"]) == item["sha256"], item["path"]
artifacts = [{"path": str(path.relative_to(PACKET)), "sha256": check.base.sha(path)}
             for path in sorted(native.iterdir()) if path.is_file()]
all_passed = all(r["accepted"] and r["matchesCurrentSourceAndBinaries"]
                 and r["matchesCurrentCheckerAndTool"] and r["expectedOutcomes"]
                 for r in reports.values())
new_cases = sorted(name for name in passing if ".Client.SelectedGifts." in name or ".SelectedGiftsNative." in name)
assert len(new_cases) == 8, new_cases
result = {
    "allSelectedQualificationsPassed": all_passed,
    "selectedNativeGiftJourneyQualified": all_passed and journey["Status"] == "passed",
    "fullCoPlayerFeatureAccepted": False, "fullPhaseAccepted": False,
    "movementTimingGateClosed": False, "productionHistoryLatencyGateClosed": False,
    "profile40ProcessKillQualified": False, "nativeProducedGiftQualified": False,
    "nativeSelectedHistoryLifecycleQualified": False, "mouseDropdownQualified": False,
    "physicalDeviceAcceptance": False, "humanUsabilityAcceptance": False, "artAcceptance": False,
    "sourceHead": preserved["sourceHead"], "preChangeCheckpoint": preserved["checkpoint"],
    "milestone": "codex/milestone-selected-material-gifts-20261003",
    "protectedCounts": {name: len(paths) for name, paths in preserved["protected"].items()},
    "onlyExistingTestProjectChange": "One additive FarmInventoryPresentation.cs source link; exact original archived and compared.",
    "predecessorIndexSha256": preserved["predecessorIndexSha256"],
    "checkerSha256": check.base.sha(PACKET / "check.py"),
    "finalizerSha256": check.base.sha(PACKET / "finalize.py"),
    "distinctCasesPassingOnVerifiedDependencies": len(passing), "newCases": new_cases,
    "reports": reports, "unresolvedSelectedFailures": failed, "retainedFailedAttempts": retained,
    "journey": {"path": str((native / "journey.json").relative_to(PACKET)),
                "status": journey["Status"], "elapsedMilliseconds": journey["ElapsedMilliseconds"],
                "observedAllowanceSeconds": journey["ExpirySeconds"],
                "preparation": journey["Preparation"], "gameplaySubmissions": 13,
                "permissionSubmissions": 3, "actualTransfers": 2},
    "authenticatedNativeArtifacts": artifacts, "leadInspectedImages": images,
    "review": "REVIEW.md", "externalClaudeRequestExecuted": False,
    "limits": [
        "Four native TLS peers; grantee and recipient render with controlled input and no-focus configuration.",
        "Native raw/source-lot gifts use real finite stock and actual human choices; preparation travel is journaled, not native-qualified.",
        "The successful new native journey has tracing enabled; it is not a production latency measurement.",
        "Core evidence is fresh because actual dependency binaries differed; no inherited Core pass is claimed.",
        "Only the listed images received manual inspection; other per-control captures have executable bounds checks.",
        "Mouse dropdown behavior retains an earlier failed input attempt; keyboard navigation does not qualify that route.",
        "This journey does not perform a profile40 archive cut or process kill or exercise the complete selected history lifecycle.",
        "Readable public item descriptions for Inventory-only control and polished icon/tooltip presentation remain follow-up work.",
        "Fresh selected passes do not explain earlier retained latency failures or establish production reliability.",
        "Independent source review covers UI/adapter/shared presentation; root integrates and reviews the separately authored probe/coordinator."
    ]
}
(PACKET / "FINAL_CHECKS.json").write_text(json.dumps(result, indent=2) + "\n")
print(json.dumps({key: result[key] for key in ("allSelectedQualificationsPassed", "selectedNativeGiftJourneyQualified",
                                             "distinctCasesPassingOnVerifiedDependencies", "unresolvedSelectedFailures")}, indent=2))
if not result["selectedNativeGiftJourneyQualified"]:
    sys.exit(1)
