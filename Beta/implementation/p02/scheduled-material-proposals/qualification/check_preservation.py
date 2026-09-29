"""Read-only preservation guard; this does not qualify runtime behavior."""
import hashlib
import json
from pathlib import Path
import subprocess

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[4]


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def verify():
    preserved = json.loads((HERE.parent / "PRESERVATION.json").read_text())
    old_tests = json.loads((HERE / "EXISTING_TEST_PINS.json").read_text())
    assert old_tests["sourceHead"] == preserved["sourceHead"]
    changed = [path for path, expected in old_tests["sha256"].items()
               if not (ROOT / path).is_file() or digest(ROOT / path) != expected]
    assert not changed, "Existing tests or fixtures changed: " + repr(changed)
    frozen = json.loads((ROOT / "Beta/implementation/p02/native-startup-profile/PRESERVATION.json")
                        .read_text())["frozenSpecs"]
    assert len(frozen) == 23
    assert all(digest(ROOT / path) == expected for path, expected in frozen.items()), "Frozen spec drift."
    index = hashlib.sha256(subprocess.check_output(
        ["git", "diff", "--cached", "--binary", "--", ".", ":!Beta"], cwd=ROOT)).hexdigest()
    assert index == preserved["predecessorIndexSha256"], "Unrelated predecessor index changed."
    checkpoint = subprocess.check_output(
        ["git", "rev-parse", preserved["checkpoint"]], cwd=ROOT, text=True).strip()
    assert checkpoint == preserved["sourceHead"], "Pre-change checkpoint moved."
    return {"existingTestFilesUnchanged": len(old_tests["sha256"]),
            "frozenSpecsUnchanged": len(frozen), "predecessorIndexSha256": index,
            "checkpoint": preserved["checkpoint"], "checkpointVerified": True}


if __name__ == "__main__":
    print(json.dumps(verify(), indent=2))
