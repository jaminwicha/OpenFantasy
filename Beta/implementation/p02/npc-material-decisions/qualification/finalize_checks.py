"""Verify the accepted NPC-decision checkpoint without rerunning unchanged test suites."""
import json
from pathlib import Path
import re
import subprocess
import xml.etree.ElementTree as ET

from check_preservation import verify
from run_checks import HERE, ROOT, NAMESPACE, assemblies, digest, inputs


def main():
    names = {
        "build": "session-build-20260929T131700Z.json",
        "focused": "session-focused-20260929T131733Z.json",
        "regression": "session-regression-20260929T131805Z.json",
    }
    reports = {kind: json.loads((HERE / name).read_text()) for kind, name in names.items()}
    for kind, report in reports.items():
        assert report["accepted"] and report["kind"] == kind
        assert report["sourceSha256"] == inputs(), "Runtime/test source drift."
        assert report["assemblySha256"] == assemblies(), "Built assembly drift."
        if kind != "build":
            assert report["buildEvidenceSha256"] == digest(HERE / names["build"])
    identities = {}
    for kind in ("focused", "regression"):
        rows = ET.parse(HERE / names[kind].replace(".json", ".trx")).findall(".//" + NAMESPACE + "UnitTestResult")
        assert rows and all(row.attrib["outcome"] == "Passed" for row in rows)
        identities[kind] = {row.attrib["testId"] for row in rows}
        assert sum("ScheduledMaterialNpc" in row.attrib["testName"] for row in rows) == 12, "NPC cases missing."
    assert len(identities["focused"]) == 23
    assert len(identities["regression"]) == 154
    assert identities["focused"] <= identities["regression"]

    documents = [ROOT / "Beta/Runtime/docs/architecture" / (name + ".md") for name in
                 ("material-proposals", "ordinary-care", "runtime-and-authority")]
    documents += [HERE.parent / (name + ".md") for name in ("INTEGRATION_REVIEW",)]
    documents += [ROOT / "Beta/implementation" / (name + ".md") for name in ("AGENT_HANDOFF", "GIT_MILESTONES")]
    checked_links = 0
    final = HERE / "FINAL_CHECKS.json"
    for document in documents:
        for target in re.findall(r"\]\(([^)]+)\)", document.read_text()):
            if "://" in target or target.startswith("#"):
                continue
            path = (document.parent / target.split("#", 1)[0]).resolve()
            assert path == final or path.exists(), (document, target)
            checked_links += 1
    subprocess.run(["git", "diff", "--check", "--", "Beta"], cwd=ROOT, check=True)
    unchanged = subprocess.check_output(["git", "diff", "HEAD", "--", "Beta/Runtime/src/OpenFantasy.Core",
                                         "Beta/Runtime/client"], cwd=ROOT)
    assert not unchanged, "This checkpoint must not change Core or client."
    result = {
        "acceptedScope": "Explicit local NPC gift decisions; full campaign and remaining scheduled joins stay open",
        "sourceHead": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip(),
        "evidenceSha256": {name: digest(HERE / name) for name in names.values()},
        "distinctManagedPasses": 154,
        "focusedPassesIncluded": 23,
        "newNpcCasesIncluded": 12,
        "managedAutomaticCuts": 1,
        "coreAndClientUnchangedFromSourceHead": True,
        "preservation": verify(),
        "documentSha256": {str(path.relative_to(ROOT)): digest(path) for path in documents},
        "relativeLinksResolved": checked_links,
        "review": "Independent native Astra source review; lead executed checks",
        "milestone": "codex/milestone-npc-material-decisions-20260929",
        "limits": ["No full-suite, native, device, human or performance acceptance",
                   "General NPC policy, recurring need episodes and remaining scheduled joins incomplete",
                   "Pending excavation, control rollover, networking and retained-save transition unqualified"],
    }
    final.write_text(json.dumps(result, indent=2) + "\n")
    print(json.dumps({key: value for key, value in result.items() if key not in ("evidenceSha256", "documentSha256")}, indent=2))


if __name__ == "__main__":
    main()
