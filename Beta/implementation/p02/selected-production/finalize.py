"""Verify selected-production evidence against the actual source, binaries and preserved inputs."""
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
additions = ['    <Compile Include="../../client/scripts/' + name + '.cs" Link="Client/' + name + '.cs" />\n'
             for name in ("FarmProductionPresentation", "FarmProductionActionOffers")]
current_project = (check.ROOT / project).read_text()
project_bytes = (check.ROOT / project).read_bytes()
for addition in additions:
    assert current_project.count(addition) == 1
    project_bytes = project_bytes.replace(addition.encode(), b"")
assert project_bytes == original.read_bytes()

reports = {}
passing = set()
failed = []
expected_counts = {"build": None, "client-managed": 231, "native-production": 2, "native-gifts": 1,
                   "native-selection": 1, "native-roster": 1, "native-compatibility": 2,
                   "core": 1885}
for mode, count in expected_counts.items():
    path = Path(json.loads((PACKET / (".latest-" + mode + ".json")).read_text())["path"])
    report = json.loads(path.read_text())
    same = report.get("source") == source and report.get("binaries") == binaries
    relevant = same
    inherited = None
    if mode == "client-managed" and not same:
        comparison = json.loads((PACKET / "MANAGED_DEPENDENCY_COMPARISON.json").read_text())
        changed_source = sorted(k for k in report["source"].keys() | source.keys()
                                if report["source"].get(k) != source.get(k))
        changed_binaries = sorted(k for k in report["binaries"].keys() | binaries.keys()
                                  if report["binaries"].get(k) != binaries.get(k))
        relevant = (check.base.sha(path) == comparison["reportSha256"]
                    and changed_source == comparison["changedSourceOutsideManagedCompilation"]
                    and changed_binaries == comparison["changedBinariesOutsideManagedExecution"]
                    and check.base.sha(check.ROOT / project) == comparison["hostingTestProjectSha256"])
        inherited = comparison["retainedClaim"] if relevant else None
    same_checker = (report["checkerSha256"] == check.base.sha(PACKET / "check.py")
                    and report["baseCheckerSha256"] == check.base.sha(PACKET.parent / "shared-player-sessions/check.py"))
    tool = report.get("nativeTool")
    same_tool = not tool or check.base.sha(Path(tool["path"])) == tool["sha256"]
    correct_count = count is None or report.get("outcomes") == {"Passed": count}
    reports[mode] = {
        "path": str(path.relative_to(PACKET)), "sha256": check.base.sha(path),
        "accepted": report["accepted"], "matchesCurrentSourceAndBinaries": same,
        "matchesRelevantDependencies": relevant, "inheritedClaim": inherited,
        "matchesCurrentCheckerAndTool": same_checker and bool(same_tool),
        "expectedOutcomes": correct_count, "seconds": report["seconds"],
        "tracing": report["tracing"], "outcomes": report.get("outcomes"),
        "unrunCases": report.get("unrunCases", [])
    }
    for case in report.get("cases", []):
        if case["outcome"] == "Passed" and relevant:
            passing.add(case["name"])
        elif case["outcome"] == "Failed":
            failed.append({"mode": mode, "case": case["name"]})

retained = []
for path in sorted((PACKET / "qualification").glob("*/report.json")):
    report = json.loads(path.read_text())
    if not report["accepted"]:
        retained.append({"path": str(path.relative_to(PACKET)), "sha256": check.base.sha(path),
                         "mode": report["mode"], "outcomes": report.get("outcomes"), "error": report.get("error")})

native = (PACKET / reports["native-production"]["path"]).parent / "native/selected-production"
journeys = {name: json.loads((native / name / "journey.json").read_text())
            for name in ("gather-craft-gift", "excavation")}
images = json.loads((PACKET / "INSPECTED_IMAGES.json").read_text())["images"]
for item in images:
    assert check.base.sha(PACKET / item["path"]) == item["sha256"], item["path"]
visual = json.loads((PACKET / "VISUAL_DEPENDENCY_COMPARISON.json").read_text())
visual_path = PACKET / visual["inspectedJourneyReport"]
visual_report = json.loads(visual_path.read_text())
assert check.base.sha(visual_path) == visual["reportSha256"]
assert sorted(k for k in visual_report["source"].keys() | source.keys()
              if visual_report["source"].get(k) != source.get(k)) == visual["changedSource"]
assert sorted(k for k in visual_report["binaries"].keys() | binaries.keys()
              if visual_report["binaries"].get(k) != binaries.get(k)) == visual["changedBinaries"]
artifacts = [{"path": str(path.relative_to(PACKET)), "sha256": check.base.sha(path)}
             for path in sorted(native.rglob("*")) if path.is_file() and path.suffix in (".ofc", ".sqlite3")
             or path.is_file() and path.name in ("journey.json", "receipts.json", "identities.json", "journal.json", "preparation.txt")]
all_passed = all(r["accepted"] and r["matchesRelevantDependencies"]
                 and r["matchesCurrentCheckerAndTool"] and r["expectedOutcomes"]
                 for r in reports.values())
new_cases = sorted(name for name in passing if ".Client.SelectedProduction." in name or ".SelectedProductionNative." in name)
assert len(new_cases) == 12, new_cases
result = {
    "allCurrentSelectedReportsPassed": all_passed,
    "fullRegressionAccepted": False, "originalNativeGiftOpeningFailureExplained": False,
    "selectedNativeProductionJourneysQualified": all_passed and all(j["Status"] == "passed" for j in journeys.values()),
    "fullCoPlayerFeatureAccepted": False, "fullPhaseAccepted": False,
    "movementTimingGateClosed": False, "productionHistoryLatencyGateClosed": False,
    "profile40ProcessKillQualified": False, "fullRecipeLibraryQualified": False,
    "selectedHumanConstructionQualified": False, "completedNonphysicalGeometryQualified": False, "nativeProducedGiftQualified": all_passed and journeys["gather-craft-gift"]["Status"] == "passed",
    "nativeSelectedHistoryLifecycleQualified": False, "mouseDropdownQualified": False,
    "physicalDeviceAcceptance": False, "humanUsabilityAcceptance": False, "artAcceptance": False,
    "sourceHead": preserved["sourceHead"], "preChangeCheckpoint": preserved["checkpoint"],
    "milestone": "codex/milestone-selected-production-20261003",
    "protectedCounts": {name: len(paths) for name, paths in preserved["protected"].items()},
    "onlyExistingTestProjectChange": "Two additive FarmProductionPresentation/ActionOffers source links; exact original archived and compared.",
    "predecessorIndexSha256": preserved["predecessorIndexSha256"],
    "checkerSha256": check.base.sha(PACKET / "check.py"),
    "finalizerSha256": check.base.sha(PACKET / "finalize.py"),
    "distinctCasesPassingOnVerifiedDependencies": len(passing), "newCases": new_cases,
    "reports": reports, "unresolvedCurrentReportFailures": failed, "retainedFailedAttempts": retained,
    "unexplainedEarlierFailure": {
        "path": "qualification/native-gifts-20261003T084836722332Z/report.json",
        "claim": "Existing raw-gift pack opening did not settle within5s. The instrumented diagnostic passed; no cause or reliability repair is established."
    },
    "managedDependencyComparison": "MANAGED_DEPENDENCY_COMPARISON.json",
    "visualDependencyComparison": "VISUAL_DEPENDENCY_COMPARISON.json",
    "journeys": {name: {"path": str((native / name / "journey.json").relative_to(PACKET)),
                         "status": journey["Status"], "elapsedMilliseconds": journey["ElapsedMilliseconds"],
                         "observedAllowanceSeconds": journey["ExpirySeconds"],
                         "preparation": journey["Preparation"]}
                 for name, journey in journeys.items()},
    "authenticatedNativeArtifacts": artifacts, "leadInspectedImages": images,
    "review": "REVIEW.md", "externalClaudeRequestExecuted": False,
    "limits": [
        "Four native TLS peers per world; grantee and gift recipient render with controlled input and no-focus configuration.",
        "Gather/craft/gift uses actual original stock and finite source/batch intervals; excavation uses owned tools and ordinary work/flow.",
        "Preparation walking/readiness is journaled but not native travel qualification; native production confirmations are distinct real commands.",
        "New native journeys have tracing enabled and do not qualify production latency or explain older retained timeouts.",
        "One uninstrumented existing native-gift opening failed. The bounded diagnostic passed without reproducing or explaining it; reliability stays open.",
        "Managed231 evidence is retained only after exact non-client source/binary comparison; two appended native-probe diagnostics are excluded from its compilation.",
        "Fresh Core qualification is required because actual stamped dependency binaries differed despite identical Core source.",
        "Only named images received lead inspection; per-control bounds supplement rather than replace image inspection.",
        "Mouse gift dropdowns, physical input, human usability and final art remain unqualified.",
        "No complete recipe library, selected human construction, assisted climb or public completed nonphysical structure projection is claimed.",
        "The native worlds do not exercise profile40 archive cuts/process kills or full selected-history lifecycle.",
        "Managed Production-only evidence is separate from native permission combinations; native mining withholds Inventory.",
        "The source review is independent of UI/shared-offer authorship; root integrates and reviews probe, tests and all terminal evidence."

    ]
}
(PACKET / "FINAL_CHECKS.json").write_text(json.dumps(result, indent=2) + "\n")
print(json.dumps({key: result[key] for key in ("allCurrentSelectedReportsPassed", "selectedNativeProductionJourneysQualified",
                                             "distinctCasesPassingOnVerifiedDependencies", "unresolvedCurrentReportFailures")}, indent=2))
if not result["selectedNativeProductionJourneysQualified"]:
    sys.exit(1)
