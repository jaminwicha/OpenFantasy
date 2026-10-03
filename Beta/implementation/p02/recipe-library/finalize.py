"""Reconcile the recipe-library evidence with its actual dependency families and retained failures."""
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
for name in ("FarmRecipeLibraryModel", "FarmClientSession.Recipes"):
    addition = ('    <Compile Include="../../client/scripts/' + name + '.cs" Link="Client/' + name + '.cs" />\n').encode()
    assert project_bytes.count(addition) == 1
    project_bytes = project_bytes.replace(addition, b"")
assert project_bytes == original.read_bytes()

counts = {"build": None, "hosting-build": None, "client-managed": 244, "codec": 161, "new-managed": 27, "core": 1885,
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
    # The later edits affect only native Godot panel/probe files, excluded from this managed assembly.
    # Compare the complete current Hosting dependency family, not just a list of changed filenames.
    if mode == "new-managed":
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
        "dependencyFamily": "Hosting and exact linked client files" if mode in ("hosting-build", "codec", "new-managed") else "Full solution",
        "unrunCases": report.get("unrunCases", [])}
    if valid:
        passing.update(case["name"] for case in report.get("cases", []) if case["outcome"] == "Passed")

retained = []
for path in sorted((PACKET / "qualification").glob("*/report.json")):
    report = json.loads(path.read_text())
    if not report["accepted"]:
        retained.append({"path": str(path.relative_to(PACKET)), "sha256": check.base.sha(path),
                         "mode": report["mode"], "outcomes": report.get("outcomes"), "error": report.get("error")})
native = (PACKET / reports["native-recipes"]["path"]).parent / "native/recipe-library"
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
    (".Client.RecipeLibrary.", ".PublicMaterialDescriptions.", ".RecipeLibraryNative.")))
result = {
    "allCurrentSelectedReportsPassed": all_passed,
    "admittedRecipeLibraryNativeQualified": all_passed and journey["Status"] == "passed",
    "fullRecipeLearningSystemAccepted": False, "fullRegressionAccepted": False,
    "originalNativeGiftOpeningFailureExplained": False, "fullPhaseAccepted": False,
    "fullCoPlayerFeatureAccepted": False, "movementTimingGateClosed": False,
    "productionHistoryLatencyGateClosed": False, "profile40ProcessKillQualified": False,
    "selectedHumanConstructionQualified": False, "completedNonphysicalGeometryQualified": False,
    "nativeSelectedHistoryLifecycleQualified": False, "physicalDeviceAcceptance": False,
    "humanUsabilityAcceptance": False, "artAcceptance": False,
    "sourceHead": pins["sourceHead"], "preChangeCheckpoint": pins["checkpoint"],
    "milestone": "codex/milestone-recipe-library-20261003",
    "protectedCounts": {name: len(paths) for name, paths in pins["protected"].items()},
    "onlyExistingTestProjectChange": "Two additive recipe model/session source links; exact archived original compared byte for byte.",
    "predecessorIndexSha256": pins["predecessorIndexSha256"],
    "checkerSha256": check.base.sha(PACKET / "check.py"), "finalizerSha256": check.base.sha(PACKET / "finalize.py"),
    "distinctCasesPassingOnVerifiedDependencies": len(passing), "newCases": new_cases,
    "reports": reports, "retainedFailedAttempts": retained,
    "retainedFrame": "RETAINED_FRAME.json",
    "journey": {"path": str((native / "journey.json").relative_to(PACKET)), **journey},
    "authenticatedNativeArtifacts": artifacts, "leadInspectedImages": images,
    "review": "REVIEW.md", "externalClaudeRequestExecuted": False,
    "limits": [
        "The admitted catalog has two nonconstruction recipes; recipe learning, arbitrary recipe breadth and construction are separate obligations.",
        "Four actual TLS peers, controlled background rendering, seven lawful harvests, ordinary biological regrowth, two crafted batches and exact original-genesis replay.",
        "Public descriptions follow exact content pins and disclosed facets; a functional tool kind alone cannot identify a particular material.",
        "The actual retained home version4 frame remains byte-exact. Selected legacy versions are covered by unchanged codec/protocol tests, not an exported retained selected frame.",
        "All report dependency families match current source and actual binaries; no result is reused solely by filename or test-only classification.",
        "The earlier raw-gift opening and movement/history timing failures remain unexplained. Later passes do not close those reliability gates.",
        "Only the named images received lead visual inspection. Injected controls do not qualify physical input, OS focus, human usability or final art.",
        "The native journey does not exercise profile40 process kills, complete selected history, production scale or full campaign acceptance."
    ]
}
(PACKET / "FINAL_CHECKS.json").write_text(json.dumps(result, indent=2) + "\n")
print(json.dumps({key: result[key] for key in ("allCurrentSelectedReportsPassed", "admittedRecipeLibraryNativeQualified",
                                              "distinctCasesPassingOnVerifiedDependencies")}, indent=2))
if not all_passed or len(new_cases) != 28:
    print("New cases:", len(new_cases))
    sys.exit(1)
