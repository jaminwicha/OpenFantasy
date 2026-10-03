"""Reconcile the human-construction evidence with its actual dependency families and retained failures."""
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
pins = json.loads((PACKET / "PRESERVATION.json").read_text())
project = "Beta/Runtime/tests/OpenFantasy.Hosting.Tests/OpenFantasy.Hosting.Tests.csproj"
original = check.ROOT / pins["permittedTestRefactors"][project]["original"]
project_bytes = (check.ROOT / project).read_bytes()
for name in ("ConstructionActionOffers",):
    addition = ('    <Compile Include="../../client/scripts/' + name + '.cs" Link="Client/' + name + '.cs" />\n').encode()
    assert project_bytes.count(addition) == 1
    project_bytes = project_bytes.replace(addition, b"")
assert project_bytes == original.read_bytes()

counts = {"build": None, "hosting-build": None, "client-managed": 247, "codec": 171, "new-managed": 13, "admission": 6, "core": 1885,
          "native-construction": 1,
          "native-recipes": 1, "native-production": 2, "native-gifts": 1, "native-selection": 1,
          "native-roster": 1, "native-compatibility": 2}
reports = {}
passing = set()
for mode, count in counts.items():
    path = Path(json.loads((PACKET / (".latest-" + mode + ".json")).read_text())["path"])
    report = json.loads(path.read_text())
    source = check.sources(mode)
    binaries = check.binaries(mode)
    full_match = report["source"] == source and report["binaries"] == binaries
    same = full_match
    # Later fixture edits affect only the native Server tests, excluded from this managed assembly.
    # Compare the complete current Hosting dependency family, not just a list of changed filenames.
    if mode in ("client-managed", "new-managed"):
        source = check.sources("codec")
        binaries = check.binaries("codec")
        same = all(report["source"].get(name) == digest for name, digest in source.items()) and all(
            report["binaries"].get(name) == digest for name, digest in binaries.items())
    same_checker = report["checkerSha256"] == check.base.sha(PACKET / "check.py") and report["baseCheckerSha256"] == check.base.sha(PACKET.parent / "shared-player-sessions/check.py")
    tool = report.get("nativeTool")
    same_tool = not tool or check.base.sha(Path(tool["path"])) == tool["sha256"]
    outcomes = count is None or report.get("outcomes") == {"Passed": count}
    valid = report["accepted"] and same and same_checker and same_tool and outcomes
    reports[mode] = {"path": str(path.relative_to(PACKET)), "sha256": check.base.sha(path),
        "accepted": report["accepted"], "matchesRelevantSourceAndBinaries": same, "matchesFullRecordedSnapshot": full_match,
        "matchesCheckerAndTool": bool(same_checker and same_tool), "expectedOutcomes": outcomes,
        "verified": bool(valid), "seconds": report["seconds"], "outcomes": report.get("outcomes"),
        "dependencyFamily": "Hosting and exact linked client files" if mode in ("hosting-build", "codec", "new-managed", "client-managed") else "Full solution",
        "unrunCases": report.get("unrunCases", [])}
    if valid:
        passing.update(case["name"] for case in report.get("cases", []) if case["outcome"] == "Passed")

retained = []
for path in sorted((PACKET / "qualification").glob("*/report.json")):
    report = json.loads(path.read_text())
    if not report["accepted"]:
        retained.append({"path": str(path.relative_to(PACKET)), "sha256": check.base.sha(path),
                         "mode": report["mode"], "outcomes": report.get("outcomes"), "error": report.get("error")})
for path in sorted((PACKET / "qualification").glob("*/INTERRUPTED.json")):
    retained.append({"path": str(path.relative_to(PACKET)), "sha256": check.base.sha(path),
                     "mode": "native-construction", "interrupted": True, "accepted": False})
native = (PACKET / reports["native-construction"]["path"]).parent / "native/human-construction"
journey = json.loads((native / "journey.json").read_text())
images = json.loads((PACKET / "INSPECTED_IMAGES.json").read_text())["images"]
for item in images:
    assert check.base.sha(PACKET / item["path"]) == item["sha256"]
artifacts = [{"path": str(path.relative_to(PACKET)), "sha256": check.base.sha(path)}
             for path in sorted(native.rglob("*")) if path.is_file() and
             (path.suffix in (".ofc", ".sqlite3") or path.name in
              ("journey.json", "receipts.json", "identities.json", "journal.json", "preparation.txt"))]
all_passed = all(report["verified"] for report in reports.values())
new_cases = sorted(name for name in passing if any(part in name for part in
    (".HumanConstruction.", ".HumanConstructionNative.")))
result = {
    "allCurrentSelectedReportsPassed": all_passed,
    "humanConstructionNativeQualified": all_passed and journey["Status"] == "passed",
    "fullRecipeLearningSystemAccepted": False, "fullRegressionAccepted": False,
    "originalNativeGiftOpeningFailureExplained": False, "fullPhaseAccepted": False,
    "fullCoPlayerFeatureAccepted": False, "movementTimingGateClosed": False,
    "productionHistoryLatencyGateClosed": False, "profile40ProcessKillQualified": False,
    "selectedHumanConstructionQualified": all_passed, "completedNonphysicalGeometryQualified": all_passed,
    "nativeSelectedObserverWithProductionWithheldQualified": False, "sustainedWorkUsabilityAccepted": False,
    "unobscuredCompletedGuardLegibilityAccepted": False,
    "nativeSelectedHistoryLifecycleQualified": False, "physicalDeviceAcceptance": False,
    "humanUsabilityAcceptance": False, "artAcceptance": False,
    "sourceHead": pins["sourceHead"], "preChangeCheckpoint": pins["checkpoint"],
    "milestone": "codex/milestone-human-construction-20261003",
    "protectedCounts": {name: len(paths) for name, paths in pins["protected"].items()},
    "onlyExistingTestProjectChange": "One additive construction offer source link; exact archived original compared byte for byte.",
    "predecessorIndexSha256": pins["predecessorIndexSha256"],
    "checkerSha256": check.base.sha(PACKET / "check.py"), "finalizerSha256": check.base.sha(PACKET / "finalize.py"),
    "distinctCasesPassingOnVerifiedDependencies": len(passing), "newCases": new_cases,
    "reports": reports, "retainedFailedAttempts": retained,
    "journey": {"path": str((native / "journey.json").relative_to(PACKET)), **journey},
    "authenticatedNativeArtifacts": artifacts, "leadInspectedImages": images,
    "review": "REVIEW.md", "externalClaudeRequestExecuted": False,
    "limits": [
        "Explicit fresh Wren-as-human qualification scenario; ordinary population and lived saves are unchanged.",
        "All200 native work confirmations, including198 delegated steps, original funding, partial stop/reopen, owner return/revoke and original-genesis replay.",
        "Ordinary home observer shows completed geometry; withheld-Production selected observer is a managed projection proof, not a native journey.",
        "Actual installed collision and required access are domain-query proofs, not a native movement-collision gesture.",
        "Unit confirmations qualify authority/input composition, not a polished sustained-work activity or production balance.",
        "Hosting managed evidence is reused only after matching its actual complete source, linked client files and binary dependency family.",
        "Prior raw-gift opening and movement/history timing failures remain unexplained; later regressions do not close those reliability gates.",
        "Controlled background rendering and injected controls do not prove physical input, OS focus, human usability or final art. The completion guard footprint overlaps its builder, so unobscured guard legibility is not accepted from that frame.",
        "Full selected history, profile40 process kills, composite maximums, production scale and the complete campaign remain open."

    ]
}
(PACKET / "FINAL_CHECKS.json").write_text(json.dumps(result, indent=2) + "\n")
print(json.dumps({key: result[key] for key in ("allCurrentSelectedReportsPassed", "humanConstructionNativeQualified",
                                              "distinctCasesPassingOnVerifiedDependencies")}, indent=2))
if not all_passed or len(new_cases) != 20:
    print("New cases:", len(new_cases))
    sys.exit(1)
