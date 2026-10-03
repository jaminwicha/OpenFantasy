"""Verify the bounded native-control evidence against its actual current dependencies."""
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
for mode in ("build", "native-selection", "client-managed", "native-roster", "native-compatibility", "core"):
    path = Path(json.loads((PACKET / (".latest-" + mode + ".json")).read_text())["path"])
    report = json.loads(path.read_text())
    same = report.get("source") == source and report.get("binaries") == binaries
    reports[mode] = {
        "path": str(path.relative_to(PACKET)), "sha256": check.base.sha(path),
        "accepted": report["accepted"], "matchesCurrentSourceAndBinaries": same,
        "seconds": report["seconds"], "outcomes": report.get("outcomes"),
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

native = PACKET / "qualification/native-selection-20261003T071106315647Z/native/selected-control"
journey = json.loads((native / "journey.json").read_text())
native_images = [native / "5795dc4890da7573cfc5b3948d0296df-first" / name
                 for name in ("0002.png", "0302.png", "0308.png", "0329.png", "0334.png")]
legacy = PACKET / "qualification/native-roster-20261003T071242417431Z/native/roster/5795dc4890da7573cfc5b3948d0296df-first"
images = native_images + [legacy / name for name in ("0026.png", "0037.png", "0143.png")]
images.append(PACKET / "qualification/native-compatibility-20261003T071411536146Z/native/rendered/native-player.png")
image_evidence = [{"path": str(path.relative_to(PACKET)), "sha256": check.base.sha(path)} for path in images]
artifacts = [{"path": str(path.relative_to(PACKET)), "sha256": check.base.sha(path)}
             for path in sorted(native.iterdir()) if path.is_file()]
preserved = json.loads((PACKET / "PRESERVATION.json").read_text())
all_passed = all(r["accepted"] and r["matchesCurrentSourceAndBinaries"] for r in reports.values())
result = {
    "allSelectedQualificationsPassed": all_passed,
    "selectedNativeJourneyQualified": all_passed and journey["Status"] == "passed",
    "fullCoPlayerFeatureAccepted": False, "fullPhaseAccepted": False,
    "movementTimingGateClosed": False, "productionHistoryLatencyGateClosed": False,
    "profile40ProcessKillQualified": False, "physicalDeviceAcceptance": False,
    "humanUsabilityAcceptance": False, "artAcceptance": False,
    "sourceHead": preserved["sourceHead"], "preChangeCheckpoint": preserved["checkpoint"],
    "milestone": "codex/milestone-native-selected-control-20261003",
    "protectedCounts": {name: len(paths) for name, paths in preserved["protected"].items()},
    "predecessorIndexSha256": preserved["predecessorIndexSha256"],
    "checkerSha256": check.base.sha(PACKET / "check.py"),
    "finalizerSha256": check.base.sha(PACKET / "finalize.py"),
    "distinctCasesPassingOnVerifiedDependencies": len(passing),
    "reports": reports, "unresolvedSelectedFailures": failed, "retainedFailedAttempts": retained,
    "earlierFunctionalPassWithRejectedImages": "qualification/native-selection-20261003T070617382448Z/report.json",
    "journey": {"path": str((native / "journey.json").relative_to(PACKET)),
                "status": journey["Status"], "elapsedMilliseconds": journey["ElapsedMilliseconds"],
                "observedAllowanceSeconds": journey["ExpirySeconds"],
                "preparation": journey["Preparation"], "movement": journey["Movement"]},
    "authenticatedNativeArtifacts": artifacts,
    "leadInspectedImages": image_evidence,
    "review": "REVIEW.md", "externalClaudeRequestExecuted": False,
    "limits": [
        "Four native TLS peers; only the grantee is rendered, with controlled input and no-focus configuration.",
        "Core evidence is fresh because revision-stamped binaries differed; no inherited Core pass is claimed.",
        "Only the listed images received manual inspection; other per-control captures have executable bounds checks.",
        "This journey does not perform a profile40 archive cut or process kill.",
        "Fresh passes do not explain earlier retained latency failures or establish production reliability.",
        "Independent source review covers the UI/adapter; root integrates and reviews the separately authored probe/coordinator."
    ]
}
(PACKET / "FINAL_CHECKS.json").write_text(json.dumps(result, indent=2) + "\n")
print(json.dumps({key: result[key] for key in ("allSelectedQualificationsPassed", "selectedNativeJourneyQualified", "distinctCasesPassingOnVerifiedDependencies", "unresolvedSelectedFailures")}, indent=2))
if not result["selectedNativeJourneyQualified"]:
    sys.exit(1)
